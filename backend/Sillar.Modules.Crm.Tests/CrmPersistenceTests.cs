using System.Text;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Sillar.Modules.Crm.Data;
using Sillar.Modules.Crm.Domain;

namespace Sillar.Modules.Crm.Tests;

/// <summary>
/// Pruebas de persistencia de M04 Paso 2 · DATOS.
/// </summary>
/// <remarks>
/// Todas cruzan la frontera con PostgreSQL real. Ninguna usa EF InMemory,
/// metadata de EF, ni inspección de código. Cada verificación ejecuta
/// INSERT/UPDATE/DELETE contra la base de datos.
///
/// Las pruebas se serializan con [Collection] para no interferir entre sí.
/// </remarks>
[Collection("CrmDb")]
public sealed class CrmPersistenceTests(CrmDbFixture fixture) : IClassFixture<CrmDbFixture>
{
    private CrmDbContext NewDb() => fixture.CreateContext();

    private async Task<NpgsqlConnection> OpenConnectionAsync()
    {
        var conn = new NpgsqlConnection(fixture.ConnectionString);
        await conn.OpenAsync();
        return conn;
    }

    private static Customer NewCustomer(string fullName = "Cliente Prueba", string email = "cliente@ejemplo.pe")
        => new() { FullName = fullName, Email = email, IsActive = true };

    // ================================================================
    // 1. CrmInitial aplica desde cero.
    // ================================================================
    [Fact]
    public async Task Test01_CrmInitial_aplica_desde_cero()
    {
        await using var db = NewDb();
        var migrations = await db.Database.SqlQueryRaw<string>(
            "SELECT \"MigrationId\" AS Value FROM crm.__migrations ORDER BY \"MigrationId\"").ToListAsync();
        Assert.Contains("20260824190200_CrmInitial", migrations);
    }

    // ================================================================
    // 2. customer y customer_address generan UUID versión 7.
    // ================================================================
    [Fact]
    public async Task Test02_customer_y_address_generan_uuid_v7()
    {
        await fixture.CleanAllTablesAsync();
        await using var db = NewDb();
        var customer = NewCustomer();
        var address = new CustomerAddress
        {
            CustomerId = customer.CustomerId,
            AddressLine = "Av. Prueba 123",
            IsActive = true
        };
        db.Customers.Add(customer);
        db.CustomerAddresses.Add(address);
        await db.SaveChangesAsync();

        Assert.Equal(7, customer.CustomerId.Version);
        Assert.Equal(7, address.CustomerAddressId.Version);

        // Verificamos que se persistieron como uuid v7 en la base.
        await using var conn = await OpenConnectionAsync();
        await using var guidCmd = conn.CreateCommand();
        guidCmd.CommandText = "SELECT customer_id FROM crm.customers WHERE customer_id = @id;";
        guidCmd.Parameters.AddWithValue("id", customer.CustomerId);
        var dbCustomerGuid = (Guid)(await guidCmd.ExecuteScalarAsync())!;
        Assert.Equal(7, dbCustomerGuid.Version);
    }

    // ================================================================
    // 3. Una segunda customer_account para el mismo customer_id falla.
    // ================================================================
    [Fact]
    public async Task Test03_segunda_account_para_mismo_customer_falla()
    {
        await fixture.CleanAllTablesAsync();
        await using var db = NewDb();
        var customer = NewCustomer();
        db.Customers.Add(customer);
        await db.SaveChangesAsync();

        db.CustomerAccounts.Add(new CustomerAccount
        {
            CustomerId = customer.CustomerId,
            PasswordHash = "hash1"
        });
        await db.SaveChangesAsync();

        db.CustomerAccounts.Add(new CustomerAccount
        {
            CustomerId = customer.CustomerId,
            PasswordHash = "hash2"
        });

        var ex = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        Assert.NotNull(ex.InnerException);
    }

    // ================================================================
    // 4. Email duplicado falla incluso si la ficha existente está de baja o bloqueada.
    // ================================================================
    [Theory]
    [InlineData("baja")]
    [InlineData("bloqueada")]
    public async Task Test04_email_duplicado_falla_con_ficha_no_activa(string label)
    {
        await fixture.CleanAllTablesAsync();
        await using var db = NewDb();
        var existing = NewCustomer(email: "dup@ejemplo.pe");
        existing.IsActive = false;
        if (label == "baja")
            existing.DeactivatedAt = DateTimeOffset.UtcNow;
        else
            existing.BlockedAt = DateTimeOffset.UtcNow;
        db.Customers.Add(existing);
        await db.SaveChangesAsync();

        db.Customers.Add(NewCustomer(email: "DUP@ejemplo.pe"));
        var ex = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        Assert.NotNull(ex.InnerException);
    }

    // ================================================================
    // 5. Documento duplicado falla.
    // ================================================================
    [Fact]
    public async Task Test05_documento_duplicado_falla()
    {
        await fixture.CleanAllTablesAsync();
        await using var db = NewDb();
        var c1 = NewCustomer(email: "doc1@ejemplo.pe");
        c1.DocumentType = "dni";
        c1.DocumentNumber = "12345678";
        db.Customers.Add(c1);
        await db.SaveChangesAsync();

        var c2 = NewCustomer(email: "doc2@ejemplo.pe");
        c2.DocumentType = "dni";
        c2.DocumentNumber = "12345678";
        db.Customers.Add(c2);

        var ex = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        Assert.NotNull(ex.InnerException);
    }

    // ================================================================
    // 6. Combinaciones inválidas de is_active/deactivated_at/blocked_at fallan.
    // ================================================================
    [Theory]
    [InlineData(true, true, false, "activa pero con deactivated_at")]
    [InlineData(true, false, true, "activa pero con blocked_at")]
    [InlineData(false, true, true, "de baja y bloqueada a la vez")]
    [InlineData(false, false, false, "inactiva sin deactivated_at ni blocked_at")]
    public async Task Test06_combinaciones_invalidas_lifecycle_fallan(bool isActive, bool hasDeact, bool hasBlocked, string label)
    {
        await fixture.CleanAllTablesAsync();
        await using var db = NewDb();
        var customer = NewCustomer(email: $"lc_{Guid.NewGuid():n}@ejemplo.pe");
        customer.IsActive = isActive;
        customer.DeactivatedAt = hasDeact ? DateTimeOffset.UtcNow : null;
        customer.BlockedAt = hasBlocked ? DateTimeOffset.UtcNow : null;
        db.Customers.Add(customer);

        var ex = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        Assert.NotNull(ex.InnerException);
    }

    // ================================================================
    // 7. reactivation_resolved_at sin reactivation_requested_at falla.
    // ================================================================
    [Fact]
    public async Task Test07_reactivation_resolved_sin_requested_falla()
    {
        await fixture.CleanAllTablesAsync();
        await using var db = NewDb();
        var customer = NewCustomer(email: "react7@ejemplo.pe");
        customer.IsActive = false;
        customer.BlockedAt = DateTimeOffset.UtcNow;
        customer.ReactivationResolvedAt = DateTimeOffset.UtcNow;
        db.Customers.Add(customer);

        var ex = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        Assert.NotNull(ex.InnerException);
    }

    // ================================================================
    // 8. reactivation_resolved_at anterior a reactivation_requested_at falla.
    // ================================================================
    [Fact]
    public async Task Test08_reactivation_resolved_anterior_a_requested_falla()
    {
        await fixture.CleanAllTablesAsync();
        await using var db = NewDb();
        var customer = NewCustomer(email: "react8@ejemplo.pe");
        customer.IsActive = false;
        customer.BlockedAt = DateTimeOffset.UtcNow;
        customer.ReactivationRequestedAt = DateTimeOffset.UtcNow;
        customer.ReactivationResolvedAt = DateTimeOffset.UtcNow.AddSeconds(-1);
        db.Customers.Add(customer);

        var ex = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        Assert.NotNull(ex.InnerException);
    }

    // ================================================================
    // 9. Dos direcciones preferidas activas del mismo cliente fallan.
    // ================================================================
    [Fact]
    public async Task Test09_dos_preferidas_activas_del_mismo_cliente_falla()
    {
        await fixture.CleanAllTablesAsync();
        await using var db = NewDb();
        var customer = NewCustomer(email: "pref9@ejemplo.pe");
        db.Customers.Add(customer);
        await db.SaveChangesAsync();

        var a1 = new CustomerAddress
        {
            CustomerId = customer.CustomerId,
            AddressLine = "Calle A 123",
            IsPreferred = true,
            IsActive = true
        };
        var a2 = new CustomerAddress
        {
            CustomerId = customer.CustomerId,
            AddressLine = "Calle B 456",
            IsPreferred = true,
            IsActive = true
        };
        db.CustomerAddresses.AddRange(a1, a2);

        var ex = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        Assert.NotNull(ex.InnerException);
    }

    // ================================================================
    // 10. purpose distinto de invitation/email_verification/password_reset falla.
    // ================================================================
    [Theory]
    [InlineData("login")]
    [InlineData("reset")]
    [InlineData("")]
    public async Task Test10_purpose_invalido_falla(string purpose)
    {
        await fixture.CleanAllTablesAsync();
        await using var db = NewDb();
        var customer = NewCustomer(email: "tok10@ejemplo.pe");
        db.Customers.Add(customer);
        await db.SaveChangesAsync();

        db.CustomerTokens.Add(new CustomerToken
        {
            CustomerId = customer.CustomerId,
            Purpose = purpose,
            TokenHash = $"hash_{purpose}_{Guid.NewGuid():n}",
            ExpiresAt = DateTimeOffset.UtcNow.AddHours(1)
        });

        var ex = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        Assert.NotNull(ex.InnerException);
    }

    // ================================================================
    // 11. Dos consumos concurrentes del mismo token: exactamente uno afecta una fila.
    // ================================================================
    [Fact]
    public async Task Test11_consumo_concurrente_del_mismo_token_produce_un_ganador()
    {
        await fixture.CleanAllTablesAsync();
        await using var db = NewDb();
        var customer = NewCustomer(email: "conc11@ejemplo.pe");
        db.Customers.Add(customer);
        await db.SaveChangesAsync();

        // Insertar el token por SQL directo para tener el id.
        await using var conn = await OpenConnectionAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO crm.customer_tokens (customer_id, purpose, token_hash, created_at, expires_at)
            VALUES (@cid, 'email_verification', @hash, now(), now() + interval '1 hour')
            RETURNING customer_token_id;
            """;
        cmd.Parameters.AddWithValue("cid", customer.CustomerId);
        cmd.Parameters.AddWithValue("hash", "conc_token_hash_11");
        var tokenId = (int)(await cmd.ExecuteScalarAsync())!;

        // Lanzar dos consumos en paralelo con conexiones separadas.
        var tasks = new Task<int>[2];
        for (var i = 0; i < 2; i++)
        {
            tasks[i] = Task.Run(async () =>
            {
                await using var c = new NpgsqlConnection(fixture.ConnectionString);
                await c.OpenAsync();
                await using var consumeCmd = c.CreateCommand();
                consumeCmd.CommandText = """
                    UPDATE crm.customer_tokens
                       SET used_at = now()
                     WHERE customer_token_id = @id
                       AND used_at IS NULL
                       AND expires_at > now();
                    """;
                consumeCmd.Parameters.AddWithValue("id", tokenId);
                return await consumeCmd.ExecuteNonQueryAsync();
            });
        }

        var results = await Task.WhenAll(tasks);
        var total = results.Sum();
        Assert.Equal(1, total);

        // Verificar que el token quedó marcado como usado.
        await using var verifyConn = await OpenConnectionAsync();
        await using var verifyCmd = verifyConn.CreateCommand();
        verifyCmd.CommandText = "SELECT used_at FROM crm.customer_tokens WHERE customer_token_id = @id;";
        verifyCmd.Parameters.AddWithValue("id", tokenId);
        var usedAt = await verifyCmd.ExecuteScalarAsync();
        Assert.NotNull(usedAt);
    }

    // ================================================================
    // 12. Eliminar schema CRM no toca CORE ni Catalog; extensiones permanecen.
    // ================================================================
    [Fact]
    public async Task Test12_eliminar_schema_crm_no_toca_core_ni_catalog()
    {
        await using var conn = await OpenConnectionAsync();
        await using var beforeCmd = conn.CreateCommand();
        beforeCmd.CommandText = """
            SELECT
              (SELECT count(*) FROM pg_tables WHERE schemaname = 'core') AS core_tables,
              (SELECT count(*) FROM pg_tables WHERE schemaname = 'catalog') AS catalog_tables,
              (SELECT count(*) FROM pg_tables WHERE schemaname = 'crm') AS crm_tables;
            """;
        await using var reader = await beforeCmd.ExecuteReaderAsync();
        await reader.ReadAsync();
        var coreBefore = reader.GetInt64(0);
        var catalogBefore = reader.GetInt64(1);
        var crmBefore = reader.GetInt64(2);
        await reader.CloseAsync();

        Assert.True(coreBefore > 0);
        Assert.True(catalogBefore > 0);
        Assert.True(crmBefore > 0);

        await using var dropCmd = conn.CreateCommand();
        dropCmd.CommandText = "DROP SCHEMA IF EXISTS crm CASCADE;";
        await dropCmd.ExecuteNonQueryAsync();

        await using var afterCmd = conn.CreateCommand();
        afterCmd.CommandText = """
            SELECT
              (SELECT count(*) FROM pg_tables WHERE schemaname = 'core') AS core_tables,
              (SELECT count(*) FROM pg_tables WHERE schemaname = 'catalog') AS catalog_tables,
              (SELECT count(*) FROM pg_tables WHERE schemaname = 'crm') AS crm_tables;
            """;
        await using var afterReader = await afterCmd.ExecuteReaderAsync();
        await afterReader.ReadAsync();
        var coreAfter = afterReader.GetInt64(0);
        var catalogAfter = afterReader.GetInt64(1);
        var crmAfter = afterReader.GetInt64(2);
        await afterReader.CloseAsync();

        Assert.Equal(coreBefore, coreAfter);
        Assert.Equal(catalogBefore, catalogAfter);
        Assert.Equal(0, crmAfter);

        // core.es_ci sigue existiendo.
        await using var collCmd = conn.CreateCommand();
        collCmd.CommandText = "SELECT 1 FROM pg_collation WHERE collname = 'es_ci' AND collnamespace = 'core'::regnamespace;";
        var collExists = await collCmd.ExecuteScalarAsync();
        Assert.NotNull(collExists);

        // crm.spanish_unaccent ya no existe (pertenecía al schema crm).
        // JOIN sobre pg_namespace, no cast 'crm'::regnamespace: después del
        // DROP el schema no existe y el cast lanza 'schema "crm" does not
        // exist' (3F000). El JOIN devuelve 0 sin error.
        await using var cfgCmd = conn.CreateCommand();
        cfgCmd.CommandText = """
            SELECT count(*)
              FROM pg_ts_config c
              JOIN pg_namespace n ON c.cfgnamespace = n.oid
             WHERE c.cfgname = 'spanish_unaccent'
               AND n.nspname = 'crm';
            """;
        var cfgAfter = (long)(await cfgCmd.ExecuteScalarAsync())!;
        Assert.Equal(0, cfgAfter);

        // Las extensiones unaccent y pg_trgm siguen existiendo (son compartidas).
        await using var extCmd = conn.CreateCommand();
        extCmd.CommandText = """
            SELECT count(*) FROM pg_extension
             WHERE extname IN ('unaccent', 'pg_trgm');
            """;
        var extCount = (long)(await extCmd.ExecuteScalarAsync())!;
        Assert.Equal(2, extCount);

        // Restaurar para las siguientes pruebas.
        await fixture.EnsureMigratedAsync();
    }

    // ================================================================
    // 13. Reinstalar CrmInitial sobre schema limpio: tablas y spanish_unaccent.
    // ================================================================
    [Fact]
    public async Task Test13_reinstalar_crminitial_sobre_schema_limpio_funciona()
    {
        await using var conn = await OpenConnectionAsync();
        await using var dropCmd = conn.CreateCommand();
        dropCmd.CommandText = "DROP SCHEMA IF EXISTS crm CASCADE;";
        await dropCmd.ExecuteNonQueryAsync();

        await fixture.EnsureMigratedAsync();

        // Las seis tablas de M04 existen.
        await using var verifyCmd = conn.CreateCommand();
        verifyCmd.CommandText = "SELECT count(*) FROM pg_tables WHERE schemaname = 'crm';";
        var tableCount = (long)(await verifyCmd.ExecuteScalarAsync())!;
        Assert.True(tableCount >= 6);

        // crm.spanish_unaccent vuelve a existir.
        // JOIN sobre pg_namespace: tras reinstalar el schema existe, pero
        // usamos el mismo patrón seguro que Test12 para consistencia.
        await using var cfgCmd = conn.CreateCommand();
        cfgCmd.CommandText = """
            SELECT count(*)
              FROM pg_ts_config c
              JOIN pg_namespace n ON c.cfgnamespace = n.oid
             WHERE c.cfgname = 'spanish_unaccent'
               AND n.nspname = 'crm';
            """;
        var cfgExists = (long)(await cfgCmd.ExecuteScalarAsync())!;
        Assert.Equal(1, cfgExists);

        // Y la extensión unaccent sigue existiendo (idempotente).
        await using var extCmd = conn.CreateCommand();
        extCmd.CommandText = "SELECT count(*) FROM pg_extension WHERE extname = 'unaccent';";
        var extExists = (long)(await extCmd.ExecuteScalarAsync())!;
        Assert.Equal(1, extExists);
    }

    // ================================================================
    // 14. NFC y NFD chocan en uq_customers_email (garantía de la base).
    // ================================================================
    [Fact]
    public async Task Test14_nfc_y_nfd_chocan_en_uq_customers_email()
    {
        await fixture.CleanAllTablesAsync();
        await using var conn = await OpenConnectionAsync();

        var nfcEmail = "josé@ejemplo.pe".Normalize(NormalizationForm.FormC);
        var nfdEmail = "josé@ejemplo.pe".Normalize(NormalizationForm.FormD);

        // Insertar NFC por SQL directo.
        var id = Guid.CreateVersion7();
        await using var insertCmd = conn.CreateCommand();
        insertCmd.CommandText = """
            INSERT INTO crm.customers (customer_id, full_name, email, origin_node)
            VALUES (@id, 'Cliente NFC', @email, 'principal');
            """;
        insertCmd.Parameters.AddWithValue("id", id);
        insertCmd.Parameters.AddWithValue("email", nfcEmail);
        await insertCmd.ExecuteNonQueryAsync();

        // Intentar insertar el mismo correo en NFD: el índice único debe rechazarlo.
        await using var dupCmd = conn.CreateCommand();
        dupCmd.CommandText = """
            INSERT INTO crm.customers (customer_id, full_name, email, origin_node)
            VALUES (@id, 'Cliente NFD', @email, 'principal');
            """;
        dupCmd.Parameters.AddWithValue("id", Guid.CreateVersion7());
        dupCmd.Parameters.AddWithValue("email", nfdEmail);

        var ex = await Assert.ThrowsAsync<PostgresException>(() => dupCmd.ExecuteNonQueryAsync());
        // 23505 = unique_violation
        Assert.Equal("23505", ex.SqlState);

        // Confirmar que solo hay una ficha.
        await using var countCmd = conn.CreateCommand();
        countCmd.CommandText = "SELECT count(*) FROM crm.customers;";
        var count = (long)(await countCmd.ExecuteScalarAsync())!;
        Assert.Equal(1, count);
    }

    // ================================================================
    // 15. Cambiar realmente customers.email limpia customer_accounts.email_verified_at.
    // ================================================================
    [Fact]
    public async Task Test15_cambiar_email_limpia_email_verified_at()
    {
        await fixture.CleanAllTablesAsync();
        await using var db = NewDb();
        var customer = NewCustomer(email: "verify15@ejemplo.pe");
        db.Customers.Add(customer);
        await db.SaveChangesAsync();

        db.CustomerAccounts.Add(new CustomerAccount
        {
            CustomerId = customer.CustomerId,
            PasswordHash = "hash15",
            EmailVerifiedAt = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync();

        var tracked = await db.Customers.SingleAsync(c => c.CustomerId == customer.CustomerId);
        tracked.Email = "nuevo15@ejemplo.pe";
        await db.SaveChangesAsync();

        // Recargar desde la base: el trigger modificó email_verified_at por SQL,
        // EF no lo sabe.
        await db.Entry(await db.CustomerAccounts.SingleAsync(a => a.CustomerId == customer.CustomerId)).ReloadAsync();
        var account = await db.CustomerAccounts.AsNoTracking().SingleAsync(a => a.CustomerId == customer.CustomerId);
        Assert.Null(account.EmailVerifiedAt);
    }

    // ================================================================
    // 16. Cambiar solamente mayúsculas del correo NO invalida email_verified_at (core.es_ci).
    // ================================================================
    [Fact]
    public async Task Test16_cambiar_solo_mayusculas_no_invalida_email_verified_at()
    {
        await fixture.CleanAllTablesAsync();
        await using var db = NewDb();
        var customer = NewCustomer(email: "verify16@ejemplo.pe");
        db.Customers.Add(customer);
        await db.SaveChangesAsync();

        db.CustomerAccounts.Add(new CustomerAccount
        {
            CustomerId = customer.CustomerId,
            PasswordHash = "hash16",
            EmailVerifiedAt = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync();

        // Cambiar solo mayúsculas por SQL directo: bajo es_ci es la misma fila.
        await using var conn = await OpenConnectionAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE crm.customers SET email = 'VERIFY16@EJEMPLO.PE' WHERE customer_id = @id;";
        cmd.Parameters.AddWithValue("id", customer.CustomerId);
        await cmd.ExecuteNonQueryAsync();

        await using var verifyCmd = conn.CreateCommand();
        verifyCmd.CommandText = "SELECT email_verified_at FROM crm.customer_accounts WHERE customer_id = @id;";
        verifyCmd.Parameters.AddWithValue("id", customer.CustomerId);
        var emailVerifiedAt = await verifyCmd.ExecuteScalarAsync();
        Assert.NotNull(emailVerifiedAt);
    }

    // ================================================================
    // 17. Actualización SQL directa NFC↔NFD: observar IS DISTINCT FROM.
    //
    // Comportamiento observado y confirmado:
    // - Sin colación: NFC y NFD son bytes distintos → IS DISTINCT FROM = true.
    // - Con core.es_ci: ICU normaliza al comparar → IS DISTINCT FROM = false.
    //   El trigger (WHEN OLD.email IS DISTINCT FROM NEW.email) NO dispara.
    //   email_verified_at queda intacto.
    //
    // Esto NO contradice el SPEC corregido: core.es_ci colapsa NFC/NFD.
    // La colación decide que son el mismo correo.
    // ================================================================
    [Fact]
    public async Task Test17_sql_directo_nfc_nfd_is_distinct_from()
    {
        await fixture.CleanAllTablesAsync();
        await using var db = NewDb();
        var customer = NewCustomer(email: "nfc17@ejemplo.pe");
        db.Customers.Add(customer);
        await db.SaveChangesAsync();

        db.CustomerAccounts.Add(new CustomerAccount
        {
            CustomerId = customer.CustomerId,
            PasswordHash = "hash17",
            EmailVerifiedAt = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync();

        await using var conn = await OpenConnectionAsync();

        var nfcEmail = "josé17@ejemplo.pe".Normalize(NormalizationForm.FormC);
        var nfdEmail = "josé17@ejemplo.pe".Normalize(NormalizationForm.FormD);

        // Cambiar a NFC con tilde.
        await using var setCmd = conn.CreateCommand();
        setCmd.CommandText = "UPDATE crm.customers SET email = @email WHERE customer_id = @id;";
        setCmd.Parameters.AddWithValue("email", nfcEmail);
        setCmd.Parameters.AddWithValue("id", customer.CustomerId);
        await setCmd.ExecuteNonQueryAsync();

        // Marcar email_verified_at de nuevo.
        await using var markCmd = conn.CreateCommand();
        markCmd.CommandText = "UPDATE crm.customer_accounts SET email_verified_at = now() WHERE customer_id = @id;";
        markCmd.Parameters.AddWithValue("id", customer.CustomerId);
        await markCmd.ExecuteNonQueryAsync();

        // Actualizar a NFD del mismo correo.
        await using var nfdCmd = conn.CreateCommand();
        nfdCmd.CommandText = "UPDATE crm.customers SET email = @email WHERE customer_id = @id;";
        nfdCmd.Parameters.AddWithValue("email", nfdEmail);
        nfdCmd.Parameters.AddWithValue("id", customer.CustomerId);
        await nfdCmd.ExecuteNonQueryAsync();

        // Observar qué pasó con email_verified_at.
        await using var checkCmd = conn.CreateCommand();
        checkCmd.CommandText = "SELECT email_verified_at FROM crm.customer_accounts WHERE customer_id = @id;";
        checkCmd.Parameters.AddWithValue("id", customer.CustomerId);
        var result = await checkCmd.ExecuteScalarAsync();

        // Comprobar el comportamiento real de IS DISTINCT FROM.
        await using var probeCmd = conn.CreateCommand();
        probeCmd.CommandText = """
            SELECT
              (@nfc::text IS DISTINCT FROM @nfd::text) AS raw_distinct,
              (@nfc::text COLLATE core.es_ci IS DISTINCT FROM @nfd::text COLLATE core.es_ci) AS ci_distinct;
            """;
        probeCmd.Parameters.AddWithValue("nfc", nfcEmail);
        probeCmd.Parameters.AddWithValue("nfd", nfdEmail);
        await using var probeReader = await probeCmd.ExecuteReaderAsync();
        await probeReader.ReadAsync();
        var rawDistinct = probeReader.GetBoolean(0);
        var ciDistinct = probeReader.GetBoolean(1);
        await probeReader.CloseAsync();

        // Comportamiento observado y documentado:
        // - Sin colación: NFC y NFD son bytes distintos → IS DISTINCT FROM = true.
        // - Con core.es_ci: ICU normaliza → IS DISTINCT FROM = false.
        //   El trigger no dispara → email_verified_at queda intacto.
        Assert.True(rawDistinct, "Sin colación, NFC y NFD son bytes distintos.");
        Assert.False(ciDistinct, "Con core.es_ci, ICU normaliza NFC y NFD: IS DISTINCT FROM = false.");

        // El trigger no disparó: email_verified_at sigue presente.
        Assert.NotNull(result);
    }

    // ================================================================
    // 18. customer_accounts, customer_sessions, customer_tokens NO llevan origin_node ni row_version.
    // ================================================================
    [Fact]
    public async Task Test18_tablas_no_replicadas_no_tienen_origin_node_ni_row_version()
    {
        await using var conn = await OpenConnectionAsync();
        foreach (var table in new[] { "customer_accounts", "customer_sessions", "customer_tokens" })
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = $"""
                SELECT count(*)
                FROM information_schema.columns
                WHERE table_schema = 'crm' AND table_name = '{table}'
                  AND column_name IN ('origin_node', 'row_version');
                """;
            var count = (long)(await cmd.ExecuteScalarAsync())!;
            Assert.Equal(0, count);
        }
    }

    // ================================================================
    // 19. customers y customer_addresses SÍ llevan origin_node y row_version, y modificar incrementa row_version.
    // ================================================================
    [Fact]
    public async Task Test19_tablas_replicadas_tienen_origin_node_y_row_version_se_incrementa()
    {
        await fixture.CleanAllTablesAsync();
        await using var conn = await OpenConnectionAsync();

        // Verificar que las columnas existen.
        foreach (var table in new[] { "customers", "customer_addresses" })
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = $"""
                SELECT count(*)
                FROM information_schema.columns
                WHERE table_schema = 'crm' AND table_name = '{table}'
                  AND column_name IN ('origin_node', 'row_version');
                """;
            var count = (long)(await cmd.ExecuteScalarAsync())!;
            Assert.Equal(2, count);
        }

        // Crear y modificar un customer: row_version sube.
        await using var db = NewDb();
        var customer = NewCustomer(email: "rv19@ejemplo.pe");
        db.Customers.Add(customer);
        await db.SaveChangesAsync();
        Assert.Equal(1, customer.RowVersion);
        Assert.Equal("principal", customer.OriginNode);

        customer.FullName = "Cliente Modificado";
        await db.SaveChangesAsync();
        Assert.Equal(2, customer.RowVersion);

        // Verificar en la base.
        await using var verifyCmd = conn.CreateCommand();
        verifyCmd.CommandText = "SELECT origin_node, row_version FROM crm.customers WHERE customer_id = @id;";
        verifyCmd.Parameters.AddWithValue("id", customer.CustomerId);
        await using var reader = await verifyCmd.ExecuteReaderAsync();
        await reader.ReadAsync();
        Assert.Equal("principal", reader.GetString(0));
        Assert.Equal(2, reader.GetInt64(1));
        await reader.CloseAsync();
    }

    // ================================================================
    // 20. crm.set_updated_at() modifica realmente updated_at mediante UPDATE SQL.
    // ================================================================
    [Fact]
    public async Task Test20_set_updated_at_modifica_updated_at()
    {
        await fixture.CleanAllTablesAsync();
        await using var db = NewDb();
        var customer = NewCustomer(email: "upd20@ejemplo.pe");
        db.Customers.Add(customer);
        await db.SaveChangesAsync();

        await using var conn = await OpenConnectionAsync();

        await using var beforeCmd = conn.CreateCommand();
        beforeCmd.CommandText = "SELECT updated_at FROM crm.customers WHERE customer_id = @id;";
        beforeCmd.Parameters.AddWithValue("id", customer.CustomerId);
        var beforeUpdated = (DateTime)(await beforeCmd.ExecuteScalarAsync())!;

        await Task.Delay(100);

        await using var updateCmd = conn.CreateCommand();
        updateCmd.CommandText = "UPDATE crm.customers SET phone = '999999999' WHERE customer_id = @id;";
        updateCmd.Parameters.AddWithValue("id", customer.CustomerId);
        await updateCmd.ExecuteNonQueryAsync();

        await using var afterCmd = conn.CreateCommand();
        afterCmd.CommandText = "SELECT updated_at FROM crm.customers WHERE customer_id = @id;";
        afterCmd.Parameters.AddWithValue("id", customer.CustomerId);
        var afterUpdated = (DateTime)(await afterCmd.ExecuteScalarAsync())!;

        Assert.True(afterUpdated > beforeUpdated,
            $"updated_at debería haber subido: before={beforeUpdated}, after={afterUpdated}");
    }

    // ================================================================
    // 21. Búsqueda parcial de email con trigramas.
    // ================================================================
    [Fact]
    public async Task Test21_busqueda_parcial_email_trgm()
    {
        await fixture.CleanAllTablesAsync();
        await using var conn = await OpenConnectionAsync();

        var id = Guid.CreateVersion7();
        await using var insertCmd = conn.CreateCommand();
        insertCmd.CommandText = """
            INSERT INTO crm.customers (customer_id, full_name, email, origin_node)
            VALUES (@id, 'Cliente Trgm', 'cliente.prueba@ejemplo.com', 'principal');
            """;
        insertCmd.Parameters.AddWithValue("id", id);
        await insertCmd.ExecuteNonQueryAsync();

        // La consulta debe usar COLLATE "C" + ILIKE: si se usa ILIKE directo
        // sobre core.es_ci, PostgreSQL devuelve 0A000.
        await using var searchCmd = conn.CreateCommand();
        searchCmd.CommandText = """
            SELECT count(*) FROM crm.customers
             WHERE email COLLATE "C" ILIKE '%prueba@ejemplo%';
            """;
        var found = (long)(await searchCmd.ExecuteScalarAsync())!;
        Assert.Equal(1, found);

        // Verificar que el índice existe.
        await using var idxCmd = conn.CreateCommand();
        idxCmd.CommandText = """
            SELECT count(*) FROM pg_indexes
             WHERE schemaname = 'crm' AND indexname = 'idx_customers_email_trgm';
            """;
        var idxExists = (long)(await idxCmd.ExecuteScalarAsync())!;
        Assert.Equal(1, idxExists);

        // Verificar que pg_trgm está instalada.
        await using var extCmd = conn.CreateCommand();
        extCmd.CommandText = "SELECT count(*) FROM pg_extension WHERE extname = 'pg_trgm';";
        var extExists = (long)(await extCmd.ExecuteScalarAsync())!;
        Assert.Equal(1, extExists);
    }

    // ================================================================
    // 22. Búsqueda de full_name: Peña → pena, Álvarez → alvarez, José → jose.
    // ================================================================
    [Fact]
    public async Task Test22_busqueda_full_name_tsvector_pena()
    {
        await fixture.CleanAllTablesAsync();
        await using var conn = await OpenConnectionAsync();

        // Verificar que la extensión unaccent existe.
        await using var extCmd = conn.CreateCommand();
        extCmd.CommandText = "SELECT count(*) FROM pg_extension WHERE extname = 'unaccent';";
        var extExists = (long)(await extCmd.ExecuteScalarAsync())!;
        Assert.Equal(1, extExists);

        // Verificar que la configuración crm.spanish_unaccent existe.
        // JOIN sobre pg_namespace: mismo patrón seguro que Test12/Test13.
        await using var cfgCmd = conn.CreateCommand();
        cfgCmd.CommandText = """
            SELECT count(*)
              FROM pg_ts_config c
              JOIN pg_namespace n ON c.cfgnamespace = n.oid
             WHERE c.cfgname = 'spanish_unaccent'
               AND n.nspname = 'crm';
            """;
        var cfgExists = (long)(await cfgCmd.ExecuteScalarAsync())!;
        Assert.Equal(1, cfgExists);

        // Diagnóstico: ver qué produce to_tsvector y plainto_tsquery con
        // crm.spanish_unaccent.
        await using var diagCmd = conn.CreateCommand();
        diagCmd.CommandText = """
            SELECT
              to_tsvector('crm.spanish_unaccent', 'Peña')::text AS vec,
              plainto_tsquery('crm.spanish_unaccent', 'pena')::text AS query,
              (to_tsvector('crm.spanish_unaccent', 'Peña')
               @@ plainto_tsquery('crm.spanish_unaccent', 'pena')) AS matches;
            """;
        await using var diagReader = await diagCmd.ExecuteReaderAsync();
        await diagReader.ReadAsync();
        var vec = diagReader.GetString(0);
        var query = diagReader.GetString(1);
        var matches = diagReader.GetBoolean(2);
        await diagReader.CloseAsync();

        Assert.True(matches,
            $"to_tsvector('crm.spanish_unaccent', 'Peña') = '{vec}', "
            + $"plainto_tsquery('crm.spanish_unaccent', 'pena') = '{query}', @@ = {matches}");

        // Insertar tres clientes con tildes.
        var ids = new[] { Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7() };
        var names = new[] { "Peña", "Álvarez", "José" };
        var emails = new[] { "pena22@ejemplo.pe", "alvarez22@ejemplo.pe", "jose22@ejemplo.pe" };
        for (var i = 0; i < 3; i++)
        {
            await using var insertCmd = conn.CreateCommand();
            insertCmd.CommandText = """
                INSERT INTO crm.customers (customer_id, full_name, email, origin_node)
                VALUES (@id, @name, @email, 'principal');
                """;
            insertCmd.Parameters.AddWithValue("id", ids[i]);
            insertCmd.Parameters.AddWithValue("name", names[i]);
            insertCmd.Parameters.AddWithValue("email", emails[i]);
            await insertCmd.ExecuteNonQueryAsync();
        }

        // Peña → pena = true
        await using var penaCmd = conn.CreateCommand();
        penaCmd.CommandText = """
            SELECT count(*) FROM crm.customers
             WHERE to_tsvector('crm.spanish_unaccent', full_name)
                   @@ plainto_tsquery('crm.spanish_unaccent', @texto);
            """;
        penaCmd.Parameters.AddWithValue("texto", "pena");
        var penaFound = (long)(await penaCmd.ExecuteScalarAsync())!;
        Assert.Equal(1, penaFound);

        // Álvarez → alvarez = true
        await using var alvarezCmd = conn.CreateCommand();
        alvarezCmd.CommandText = """
            SELECT count(*) FROM crm.customers
             WHERE to_tsvector('crm.spanish_unaccent', full_name)
                   @@ plainto_tsquery('crm.spanish_unaccent', @texto);
            """;
        alvarezCmd.Parameters.AddWithValue("texto", "alvarez");
        var alvarezFound = (long)(await alvarezCmd.ExecuteScalarAsync())!;
        Assert.Equal(1, alvarezFound);

        // José → jose = true
        await using var joseCmd = conn.CreateCommand();
        joseCmd.CommandText = """
            SELECT count(*) FROM crm.customers
             WHERE to_tsvector('crm.spanish_unaccent', full_name)
                   @@ plainto_tsquery('crm.spanish_unaccent', @texto);
            """;
        joseCmd.Parameters.AddWithValue("texto", "jose");
        var joseFound = (long)(await joseCmd.ExecuteScalarAsync())!;
        Assert.Equal(1, joseFound);

        // Verificar que el índice existe.
        await using var idxCmd = conn.CreateCommand();
        idxCmd.CommandText = """
            SELECT count(*) FROM pg_indexes
             WHERE schemaname = 'crm' AND indexname = 'idx_customers_full_name_search';
            """;
        var idxExists = (long)(await idxCmd.ExecuteScalarAsync())!;
        Assert.Equal(1, idxExists);
    }

    // ================================================================
    // 23. Se puede insertar un contact_message sin customer_id.
    // ================================================================
    [Fact]
    public async Task Test23_contact_message_sin_customer_id()
    {
        await fixture.CleanAllTablesAsync();
        await using var conn = await OpenConnectionAsync();

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO crm.contact_messages (full_name, email, phone, subject, message)
            VALUES ('Visitante Anónimo', 'visitante@ejemplo.pe', null, 'Consulta', 'Hola');
            """;
        await cmd.ExecuteNonQueryAsync();

        await using var verifyCmd = conn.CreateCommand();
        verifyCmd.CommandText = "SELECT count(*) FROM crm.contact_messages WHERE customer_id IS NULL;";
        var count = (long)(await verifyCmd.ExecuteScalarAsync())!;
        Assert.Equal(1, count);
    }

    // ================================================================
    // 24. contact_message vinculado a customer existente funciona; customer_id inexistente falla por FK.
    // ================================================================
    [Fact]
    public async Task Test24_contact_message_vinculado_y_fk_inexistente()
    {
        await fixture.CleanAllTablesAsync();
        await using var db = NewDb();
        var customer = NewCustomer();
        db.Customers.Add(customer);
        await db.SaveChangesAsync();

        await using var conn = await OpenConnectionAsync();

        // Vinculado a un customer existente.
        await using var linkedCmd = conn.CreateCommand();
        linkedCmd.CommandText = """
            INSERT INTO crm.contact_messages (customer_id, full_name, email, phone, subject, message)
            VALUES (@cid, 'Cliente Vinculado', 'vinc@ejemplo.pe', null, 'Asunto', 'Mensaje');
            """;
        linkedCmd.Parameters.AddWithValue("cid", customer.CustomerId);
        await linkedCmd.ExecuteNonQueryAsync();

        await using var verifyLinked = conn.CreateCommand();
        verifyLinked.CommandText = "SELECT count(*) FROM crm.contact_messages WHERE customer_id = @cid;";
        verifyLinked.Parameters.AddWithValue("cid", customer.CustomerId);
        var linkedCount = (long)(await verifyLinked.ExecuteScalarAsync())!;
        Assert.Equal(1, linkedCount);

        // Customer_id inexistente: la FK debe rechazarlo.
        await using var badCmd = conn.CreateCommand();
        badCmd.CommandText = """
            INSERT INTO crm.contact_messages (customer_id, full_name, email, phone, subject, message)
            VALUES (@cid, 'Falso', 'falso@ejemplo.pe', null, 'Asunto', 'Mensaje');
            """;
        badCmd.Parameters.AddWithValue("cid", Guid.CreateVersion7());
        var ex = await Assert.ThrowsAsync<PostgresException>(() => badCmd.ExecuteNonQueryAsync());
        // 23503 = foreign_key_violation
        Assert.Equal("23503", ex.SqlState);
    }

    // ================================================================
    // 25. Sin email Y sin phone falla por ck_contact_messages_contact_channel.
    // ================================================================
    [Fact]
    public async Task Test25_contact_message_sin_email_ni_phone_falla()
    {
        await fixture.CleanAllTablesAsync();
        await using var conn = await OpenConnectionAsync();

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO crm.contact_messages (full_name, email, phone, subject, message)
            VALUES ('Sin Contacto', null, null, null, 'Mensaje');
            """;
        var ex = await Assert.ThrowsAsync<PostgresException>(() => cmd.ExecuteNonQueryAsync());
        // 23514 = check_violation
        Assert.Equal("23514", ex.SqlState);
    }

    // ================================================================
    // 26. contact_messages no contiene origin_node ni row_version.
    // ================================================================
    [Fact]
    public async Task Test26_contact_messages_no_tiene_origin_node_ni_row_version()
    {
        await using var conn = await OpenConnectionAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT count(*)
            FROM information_schema.columns
            WHERE table_schema = 'crm' AND table_name = 'contact_messages'
              AND column_name IN ('origin_node', 'row_version');
            """;
        var count = (long)(await cmd.ExecuteScalarAsync())!;
        Assert.Equal(0, count);
    }

    // ================================================================
    // 27. UPDATE real sobre contact_messages modifica updated_at mediante trigger.
    // ================================================================
    [Fact]
    public async Task Test27_contact_messages_set_updated_at_modifica_updated_at()
    {
        await fixture.CleanAllTablesAsync();
        await using var conn = await OpenConnectionAsync();

        await using var insertCmd = conn.CreateCommand();
        insertCmd.CommandText = """
            INSERT INTO crm.contact_messages (full_name, email, phone, subject, message)
            VALUES ('Trigger Test', 'trg27@ejemplo.pe', null, 'Asunto', 'Mensaje');
            """;
        await insertCmd.ExecuteNonQueryAsync();

        await using var beforeCmd = conn.CreateCommand();
        beforeCmd.CommandText = "SELECT updated_at FROM crm.contact_messages WHERE email = 'trg27@ejemplo.pe';";
        var beforeUpdated = (DateTime)(await beforeCmd.ExecuteScalarAsync())!;

        await Task.Delay(100);

        await using var updateCmd = conn.CreateCommand();
        updateCmd.CommandText = "UPDATE crm.contact_messages SET message = 'Mensaje modificado' WHERE email = 'trg27@ejemplo.pe';";
        await updateCmd.ExecuteNonQueryAsync();

        await using var afterCmd = conn.CreateCommand();
        afterCmd.CommandText = "SELECT updated_at FROM crm.contact_messages WHERE email = 'trg27@ejemplo.pe';";
        var afterUpdated = (DateTime)(await afterCmd.ExecuteScalarAsync())!;

        Assert.True(afterUpdated > beforeUpdated,
            $"updated_at debería haber subido: before={beforeUpdated}, after={afterUpdated}");
    }
}
