using Npgsql;
using Sillar.Core;
using Sillar.Modules.Catalog;
using Sillar.Modules.Crm;

namespace Sillar.Modules.B2B.Tests;

/// <summary>
/// C6 por SQL: las dos direcciones de las dependencias duras, con negativo y
/// retorno, contra una base de verdad y ejecutando los scripts del árbol.
/// </summary>
public sealed class InstalacionYDesinstalacionTests
{
    private static readonly string[] FksEsperadas =
    [
        "fk_institution_requests_customer",
        "fk_quote_lines_item",
        "fk_quotes_customer",
        "fk_special_order_leads_customer",
        "fk_special_order_leads_product",
    ];

    // --- Dirección 1: instalar M07 sin sus dependencias ----------------------

    [Fact]
    public Task Instalar_M07_sin_M01_falla_diciendo_que_falta_y_no_deja_tablas_de_M07()
        => BaseEfimera.ConBaseAsync(async cadena =>
        {
            await BaseEfimera.InstalarAsync(cadena, new CoreModule(), new CrmModule());

            var error = await Assert.ThrowsAnyAsync<PostgresException>(
                () => new B2BModule().ApplyMigrationsAsync(cadena, CancellationToken.None));

            // El código y el principio exacto, no una subcadena: una versión rota
            // de esta guarda fallaba con «malformed array literal: "M01 Catálogo…"»
            // y pasaba igual (sabotaje S4).
            Assert.Equal(PostgresErrorCodes.UndefinedTable, error.SqlState);
            Assert.Equal("M07 Solicitudes B2B no se puede instalar porque falta: M01 Catálogo (schema catalog). Instala eso antes y repite la instalación de M07.", error.MessageText);
            Assert.DoesNotContain("M04", error.MessageText);
            await NoQuedaNadaDeM07Async(cadena);

            // Y se puede reanudar: con M01 instalado, la misma instalación termina.
            await BaseEfimera.InstalarAsync(cadena, new CatalogModule(), new B2BModule());
            Assert.Equal(FksEsperadas, await BaseEfimera.FkCruzadasDeB2bAsync(cadena));
        });

    [Fact]
    public Task Instalar_M07_sin_M04_falla_diciendo_que_falta_y_no_deja_tablas_de_M07()
        => BaseEfimera.ConBaseAsync(async cadena =>
        {
            await BaseEfimera.InstalarAsync(cadena, new CoreModule(), new CatalogModule());

            var error = await Assert.ThrowsAnyAsync<PostgresException>(
                () => new B2BModule().ApplyMigrationsAsync(cadena, CancellationToken.None));

            // El código y el principio exacto, no una subcadena: una versión rota
            // de esta guarda fallaba con «malformed array literal: "M01 Catálogo…"»
            // y pasaba igual (sabotaje S4).
            Assert.Equal(PostgresErrorCodes.UndefinedTable, error.SqlState);
            Assert.Equal("M07 Solicitudes B2B no se puede instalar porque falta: M04 Clientes (schema crm). Instala eso antes y repite la instalación de M07.", error.MessageText);
            await NoQuedaNadaDeM07Async(cadena);
        });

    /// <summary>
    /// Lo que de verdad se garantiza tras una instalación rechazada: ninguna
    /// tabla de M07 y ninguna migración registrada.
    /// </summary>
    /// <remarks>
    /// <b>No</b> se garantiza que falte el schema: EF crea <c>b2b</c> y su
    /// <c>__migrations</c> vacía ANTES de abrir la transacción de la migración,
    /// y la guarda vive dentro de ella. Quitarlo exigiría tocar
    /// <c>Sillar.Shared.Data</c>. Queda escrito como límite en
    /// <c>C6-AUDITORIA-M07.md</c>, y esta prueba falla si el residuo crece.
    /// </remarks>
    private static async Task NoQuedaNadaDeM07Async(string cadena)
    {
        var tablas = await BaseEfimera.EscalarAsync<string>(cadena, """
            SELECT coalesce(string_agg(tablename, ',' ORDER BY tablename), '')
              FROM pg_tables WHERE schemaname = 'b2b'
            """);
        Assert.True(tablas is "" or "__migrations", $"quedaron tablas de M07: {tablas}");

        if (tablas == "__migrations")
        {
            Assert.Equal(0L, await BaseEfimera.EscalarAsync<long>(cadena, "SELECT count(*) FROM b2b.__migrations"));
        }
    }

    [Fact]
    public Task Instalar_M07_con_M01_y_M04_crea_sus_cinco_claves_foraneas_duras()
        => BaseEfimera.ConBaseAsync(async cadena =>
        {
            await BaseEfimera.InstalarBaseDeM07Async(cadena);
            await BaseEfimera.InstalarAsync(cadena, new B2BModule());

            Assert.Equal(FksEsperadas, await BaseEfimera.FkCruzadasDeB2bAsync(cadena));
        });

    // --- Dirección 2: desinstalar una dependencia con M07 instalado ----------

    [Theory]
    [InlineData("catalog", "M01 Catálogo")]
    [InlineData("crm", "M04 Clientes")]
    public Task Con_M07_instalado_el_99_drop_de_una_dependencia_dura_se_rechaza_sin_borrar_nada(string modulo, string nombre)
        => BaseEfimera.ConBaseAsync(async cadena =>
        {
            await BaseEfimera.InstalarBaseDeM07Async(cadena);
            await BaseEfimera.InstalarAsync(cadena, new B2BModule());

            var error = await Assert.ThrowsAnyAsync<PostgresException>(
                () => BaseEfimera.EjecutarAsync(cadena, BaseEfimera.Script(modulo, "99_drop.sql")));

            Assert.Equal(PostgresErrorCodes.DependentObjectsStillExist, error.SqlState);
            Assert.Contains($"No se desinstala {nombre}", error.MessageText);
            Assert.Contains("b2b", error.MessageText);
            Assert.True(await BaseEfimera.ExisteSchemaAsync(cadena, modulo), $"se borró {modulo} pese a la guarda");
            Assert.Equal(FksEsperadas, await BaseEfimera.FkCruzadasDeB2bAsync(cadena));
        });

    /// <summary>
    /// Lanzado como psql sin <c>ON_ERROR_STOP</c> —sentencia a sentencia, sin
    /// pararse en errores—, el script tampoco borra nada. Es lo que obliga a que
    /// la comprobación y el DROP vivan en el mismo bloque.
    /// </summary>
    [Theory]
    [InlineData("catalog")]
    [InlineData("crm")]
    public Task Lanzado_como_psql_sin_detenerse_en_errores_el_99_drop_tampoco_borra_nada(string modulo)
        => BaseEfimera.ConBaseAsync(async cadena =>
        {
            await BaseEfimera.InstalarBaseDeM07Async(cadena);
            await BaseEfimera.InstalarAsync(cadena, new B2BModule());

            var errores = await ComoPsql.EjecutarAsync(cadena, BaseEfimera.Script(modulo, "99_drop.sql"));

            Assert.Contains(errores, e => e.SqlState == PostgresErrorCodes.DependentObjectsStillExist);
            Assert.True(await BaseEfimera.ExisteSchemaAsync(cadena, modulo), $"se borró {modulo}: la guarda solo paraba a quien usa ON_ERROR_STOP");
            Assert.Equal(FksEsperadas, await BaseEfimera.FkCruzadasDeB2bAsync(cadena));
        });

    /// <summary>
    /// La segunda señal: aunque las FK ya se hubieran perdido, un módulo
    /// registrado con dependencia dura y con su schema instalado sigue
    /// impidiendo la desinstalación.
    /// </summary>
    [Fact]
    public Task La_guarda_rechaza_por_el_registro_de_modulos_aunque_las_FK_ya_no_esten()
        => BaseEfimera.ConBaseAsync(async cadena =>
        {
            await BaseEfimera.InstalarBaseDeM07Async(cadena);
            await BaseEfimera.InstalarAsync(cadena, new B2BModule());
            await BaseEfimera.EjecutarAsync(cadena, """
                INSERT INTO core.modules (code, display_name, description, version)
                VALUES ('catalog', 'Catálogo', 'x', '1'), ('b2b', 'Solicitudes', 'x', '1');
                INSERT INTO core.module_dependencies (module_id, depends_on_module_id, kind)
                SELECT m.module_id, t.module_id, 'hard'
                  FROM core.modules m, core.modules t
                 WHERE m.code = 'b2b' AND t.code = 'catalog';
                ALTER TABLE b2b.quote_lines DROP CONSTRAINT fk_quote_lines_item;
                ALTER TABLE b2b.special_order_leads DROP CONSTRAINT fk_special_order_leads_product;
                """);

            var error = await Assert.ThrowsAnyAsync<PostgresException>(
                () => BaseEfimera.EjecutarAsync(cadena, BaseEfimera.Script("catalog", "99_drop.sql")));

            Assert.Contains("registrados como dependencia dura: b2b", error.MessageText);
            Assert.True(await BaseEfimera.ExisteSchemaAsync(cadena, "catalog"));
        });

    // --- Retorno: M07 primero, después sus dependencias ----------------------

    [Fact]
    public Task Desinstalar_M07_primero_permite_despues_desinstalar_M01_y_M04()
        => BaseEfimera.ConBaseAsync(async cadena =>
        {
            await BaseEfimera.InstalarBaseDeM07Async(cadena);
            await BaseEfimera.InstalarAsync(cadena, new B2BModule());

            await BaseEfimera.EjecutarAsync(cadena, BaseEfimera.Script("b2b", "99_drop.sql"));
            await BaseEfimera.EjecutarAsync(cadena, BaseEfimera.Script("catalog", "99_drop.sql"));
            await BaseEfimera.EjecutarAsync(cadena, BaseEfimera.Script("crm", "99_drop.sql"));

            Assert.False(await BaseEfimera.ExisteSchemaAsync(cadena, "b2b"));
            Assert.False(await BaseEfimera.ExisteSchemaAsync(cadena, "catalog"));
            Assert.False(await BaseEfimera.ExisteSchemaAsync(cadena, "crm"));
            Assert.True(await BaseEfimera.ExisteSchemaAsync(cadena, "core"), "desinstalar módulos se llevó CORE");
        });

    [Fact]
    public Task Desinstalar_y_reinstalar_M07_deja_M01_M04_y_CORE_como_estaban_y_devuelve_sus_FK()
        => BaseEfimera.ConBaseAsync(async cadena =>
        {
            await BaseEfimera.InstalarBaseDeM07Async(cadena);
            await BaseEfimera.InstalarAsync(cadena, new B2BModule());
            const string objetos = """
                SELECT count(*) FROM pg_class c JOIN pg_namespace n ON n.oid = c.relnamespace
                 WHERE n.nspname IN ('core', 'catalog', 'crm')
                """;
            var antes = await BaseEfimera.EscalarAsync<long>(cadena, objetos);

            await BaseEfimera.EjecutarAsync(cadena, BaseEfimera.Script("b2b", "99_drop.sql"));
            await BaseEfimera.EjecutarAsync(cadena, BaseEfimera.Script("b2b", "99_drop.sql"));
            Assert.Equal(antes, await BaseEfimera.EscalarAsync<long>(cadena, objetos));

            await BaseEfimera.InstalarAsync(cadena, new B2BModule());
            await BaseEfimera.EjecutarAsync(cadena, BaseEfimera.Script("b2b", "02_seed.sql"));
            await BaseEfimera.EjecutarAsync(cadena, BaseEfimera.Script("b2b", "02_seed.sql"));

            Assert.Equal(FksEsperadas, await BaseEfimera.FkCruzadasDeB2bAsync(cadena));
            Assert.Equal(antes, await BaseEfimera.EscalarAsync<long>(cadena, objetos));
        });

    // --- Restricción de datos con INSERT real (SPEC §9) ----------------------

    [Fact]
    public Task Ck_quotes_origen_rechaza_dos_origenes_y_ninguno_y_acepta_uno()
        => BaseEfimera.ConBaseAsync(async cadena =>
        {
            await BaseEfimera.InstalarBaseDeM07Async(cadena);
            await BaseEfimera.InstalarAsync(cadena, new B2BModule());
            await BaseEfimera.EjecutarAsync(cadena, """
                INSERT INTO crm.customers (customer_id, full_name, email, origin_node)
                VALUES ('00000000-0000-7000-8000-000000000001', 'Cliente de prueba', 'cliente@ejemplo.test', 'central');
                INSERT INTO b2b.special_order_leads (customer_id, product_name, product_slug, description)
                VALUES ('00000000-0000-7000-8000-000000000001', 'Producto', 'producto', 'Con otro color');
                INSERT INTO b2b.institution_requests (customer_id, institution_name, description, quantity)
                VALUES ('00000000-0000-7000-8000-000000000001', 'Institución', 'Cordones', 100);
                """);
            const string Lead = "(SELECT min(special_order_lead_id) FROM b2b.special_order_leads)";
            const string Institucion = "(SELECT min(institution_request_id) FROM b2b.institution_requests)";
            static string Insert(string numero, string lead, string institucion) => $"""
                INSERT INTO b2b.quotes (quote_number, customer_id, special_order_lead_id, institution_request_id, total_amount)
                VALUES ('{numero}', '00000000-0000-7000-8000-000000000001', {lead}, {institucion}, 0)
                """;

            var dos = await Assert.ThrowsAnyAsync<PostgresException>(
                () => BaseEfimera.EjecutarAsync(cadena, Insert("PRUEBA-DOS", Lead, Institucion)));
            Assert.Equal("ck_quotes_origen", dos.ConstraintName);

            var ninguno = await Assert.ThrowsAnyAsync<PostgresException>(
                () => BaseEfimera.EjecutarAsync(cadena, Insert("PRUEBA-NINGUNO", "NULL", "NULL")));
            Assert.Equal("ck_quotes_origen", ninguno.ConstraintName);

            await BaseEfimera.EjecutarAsync(cadena, Insert("PRUEBA-UNO", Lead, "NULL"));
            Assert.Equal(1L, await BaseEfimera.EscalarAsync<long>(cadena, "SELECT count(*) FROM b2b.quotes"));
        });
}
