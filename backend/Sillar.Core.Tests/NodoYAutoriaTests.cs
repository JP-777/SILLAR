using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Npgsql;
using Sillar.Core.Authentication;
using Sillar.Core.Contracts;
using Sillar.Core.Data;
using Sillar.Core.Domain;
using Sillar.Core.Media;
using Sillar.Core.Services;
using Sillar.Shared.Replication;
using static Sillar.Core.Tests.BaseDePrueba;

namespace Sillar.Core.Tests;

/// <summary>
/// ENTREGA-05 de CORE: el nodo de pertenencia de las cuentas y la fotografía
/// del autor de los medios, contra PostgreSQL real.
/// </summary>
/// <remarks>
/// La migración se prueba en los dos escenarios que existen: una instalación
/// limpia y una base que ya tenía cuentas y medios antes de ella. Para la
/// segunda se migra hasta la anterior, se siembra con SQL lo que la versión
/// anterior permitía, y se aplica la nueva.
/// </remarks>
public sealed class NodoYAutoriaTests
{
    private const string MigracionAnterior = "20260829011000_CoreSmtpSettings";
    private const string NodoConfigurado = "nodo-configurado-prueba";
    private const string NodoDondeNacioElArchivo = "nodo-donde-nacio-el-archivo";

    // ==================================================================
    // Instalación limpia.
    // ==================================================================

    [Fact]
    public async Task En_una_instalacion_limpia_la_migracion_no_necesita_nodo_y_deja_el_esquema_nuevo()
    {
        var ct = TestContext.Current.CancellationToken;

        await ConBaseVaciaAsync(async cadena =>
        {
            // Sin nodo en la conexión: con admin_users vacía no hay nada que rellenar.
            await using (var contexto = Contexto(cadena))
            {
                await contexto.Database.MigrateAsync(ct);
            }

            Assert.Equal("NO", await EscalarAsync(cadena,
                "SELECT is_nullable FROM information_schema.columns WHERE table_schema = 'core' AND table_name = 'admin_users' AND column_name = 'home_node'"));
            Assert.Equal("0", await EscalarAsync(cadena,
                "SELECT count(*) FROM pg_constraint WHERE conname = 'fk_media_assets_created_by'"));
            Assert.Equal("0", await EscalarAsync(cadena,
                "SELECT count(*) FROM pg_constraint c JOIN pg_class t ON t.oid = c.conrelid JOIN pg_namespace n ON n.oid = t.relnamespace "
                + "WHERE n.nspname = 'core' AND t.relname = 'media_assets' AND c.contype = 'f'"));
            Assert.Equal("1", await EscalarAsync(cadena,
                "SELECT count(*) FROM pg_trigger WHERE tgname = 'trg_media_assets_autor_inmutable'"));
        }, ct);
    }

    // ==================================================================
    // Migración sobre datos preexistentes.
    // ==================================================================

    [Fact]
    public async Task Sobre_datos_preexistentes_rellena_el_nodo_de_las_cuentas_y_fotografia_al_autor_valido()
    {
        var ct = TestContext.Current.CancellationToken;

        await ConBaseVaciaAsync(async cadena =>
        {
            await MigrarHastaLaAnteriorAsync(cadena, ct);

            var autora = await CrearCuentaAnteriorAsync(cadena, "Ana Autora", "ana@ejemplo.test");
            var conAutor = await CrearMedioAnteriorAsync(cadena, autora);
            var sinAutor = await CrearMedioAnteriorAsync(cadena, autor: null);

            var cuentaAntes = await EscalarAsync(cadena, $"SELECT updated_at::text FROM core.admin_users WHERE admin_user_id = {autora}");
            var medioAntes = await EscalarAsync(cadena, $"SELECT updated_at::text FROM core.media_assets WHERE media_asset_id = '{conAutor}'");

            await MigrarConNodoAsync(cadena, NodoConfigurado, ct);

            // admin_users: rellenada desde el nodo configurado.
            Assert.Equal(NodoConfigurado, await EscalarAsync(cadena, $"SELECT home_node FROM core.admin_users WHERE admin_user_id = {autora}"));

            // Autor válido: los tres datos, y el nodo es el DE LA CUENTA, no el
            // origin_node del archivo.
            Assert.Equal(
                $"{autora}|Ana Autora|{NodoConfigurado}|{NodoDondeNacioElArchivo}",
                await EscalarAsync(cadena,
                    "SELECT concat_ws('|', created_by_admin_user_local_id, created_by_admin_user_name, created_by_admin_user_home_node, origin_node) "
                    + $"FROM core.media_assets WHERE media_asset_id = '{conAutor}'"));

            // Autor NULL: válido, los tres nulos.
            Assert.Equal("true", await EscalarAsync(cadena,
                "SELECT (created_by_admin_user_local_id IS NULL AND created_by_admin_user_name IS NULL AND created_by_admin_user_home_node IS NULL)::text "
                + $"FROM core.media_assets WHERE media_asset_id = '{sinAutor}'"));

            // Añadir una columna no es modificar la fila: updated_at intacto.
            Assert.Equal(cuentaAntes, await EscalarAsync(cadena, $"SELECT updated_at::text FROM core.admin_users WHERE admin_user_id = {autora}"));
            Assert.Equal(medioAntes, await EscalarAsync(cadena, $"SELECT updated_at::text FROM core.media_assets WHERE media_asset_id = '{conAutor}'"));
        }, ct);
    }

    [Fact]
    public async Task Sin_nodo_configurado_y_con_cuentas_la_migracion_aborta_sin_cambiar_nada()
    {
        var ct = TestContext.Current.CancellationToken;

        await ConBaseVaciaAsync(async cadena =>
        {
            await MigrarHastaLaAnteriorAsync(cadena, ct);
            await CrearCuentaAnteriorAsync(cadena, "Sin Nodo", "sin-nodo@ejemplo.test");

            // Ni literal ni valor por defecto: sin nodo, no se adivina.
            var error = await Assert.ThrowsAsync<PostgresException>(() => MigrarConNodoAsync(cadena, nodo: null, ct));

            Assert.Equal(PostgresErrorCodes.InvalidParameterValue, error.SqlState);
            Assert.Contains("hay 1 cuenta(s)", error.MessageText, StringComparison.Ordinal);
            Assert.Contains("Sillar__Node__Code", error.Hint ?? "", StringComparison.Ordinal);

            await NadaCambioAsync(cadena);
        }, ct);
    }

    [Fact]
    public async Task Un_autor_que_no_existe_es_corrupcion_la_migracion_aborta_dice_cuantas_y_como_localizarlas()
    {
        var ct = TestContext.Current.CancellationToken;

        await ConBaseVaciaAsync(async cadena =>
        {
            await MigrarHastaLaAnteriorAsync(cadena, ct);
            var autora = await CrearCuentaAnteriorAsync(cadena, "Autora Real", "real@ejemplo.test");
            await CrearMedioAnteriorAsync(cadena, autora);

            // Con la FK puesta no se puede fabricar: alguien tuvo que retirarla.
            await EjecutarAsync(cadena, "ALTER TABLE core.media_assets DROP CONSTRAINT fk_media_assets_created_by");
            await CrearMedioAnteriorAsync(cadena, autor: 9001);
            await CrearMedioAnteriorAsync(cadena, autor: 9002);

            var error = await Assert.ThrowsAsync<PostgresException>(() => MigrarConNodoAsync(cadena, NodoConfigurado, ct));

            Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, error.SqlState);
            Assert.Contains("tiene 2 fila(s)", error.MessageText, StringComparison.Ordinal);
            Assert.Contains("SELECT m.media_asset_id, m.created_by FROM core.media_assets m", error.Hint ?? "", StringComparison.Ordinal);

            // La consulta del HINT localiza exactamente las dos.
            Assert.Equal("9001,9002", await EscalarAsync(cadena,
                "SELECT string_agg(m.created_by::text, ',' ORDER BY m.created_by) FROM core.media_assets m WHERE m.created_by IS NOT NULL "
                + "AND NOT EXISTS (SELECT 1 FROM core.admin_users a WHERE a.admin_user_id = m.created_by)"));

            // Y admin_users, que va antes en la misma migración, tampoco cambió.
            await NadaCambioAsync(cadena);
        }, ct);
    }

    // ==================================================================
    // La fotografía después de la migración.
    // ==================================================================

    [Fact]
    public async Task La_fotografia_del_autor_no_se_modifica_una_vez_escrita()
    {
        var ct = TestContext.Current.CancellationToken;

        await ConBaseVaciaAsync(async cadena =>
        {
            await MigrarHastaLaAnteriorAsync(cadena, ct);
            var autora = await CrearCuentaAnteriorAsync(cadena, "Inmutable", "inmutable@ejemplo.test");
            var medio = await CrearMedioAnteriorAsync(cadena, autora);
            await MigrarConNodoAsync(cadena, NodoConfigurado, ct);

            var error = await Assert.ThrowsAsync<PostgresException>(() => EjecutarAsync(cadena,
                $"UPDATE core.media_assets SET created_by_admin_user_name = 'Otro Nombre' WHERE media_asset_id = '{medio}'"));
            Assert.Equal(PostgresErrorCodes.CheckViolation, error.SqlState);

            // Lo demás de la fila sí se puede tocar: la guarda es solo del autor.
            await EjecutarAsync(cadena, $"UPDATE core.media_assets SET alt_text = 'Texto' WHERE media_asset_id = '{medio}'");
        }, ct);
    }

    [Fact]
    public async Task Media_fotografia_del_autor_no_se_admite()
    {
        var ct = TestContext.Current.CancellationToken;

        await ConBaseVaciaAsync(async cadena =>
        {
            await using (var contexto = Contexto(cadena))
            {
                await contexto.Database.MigrateAsync(ct);
            }

            var error = await Assert.ThrowsAsync<PostgresException>(() => EjecutarAsync(cadena,
                "INSERT INTO core.media_assets (media_asset_id, origin_node, stored_name, relative_path, mime_type, size_bytes, created_by_admin_user_name) "
                + $"VALUES ('{Guid.CreateVersion7()}', 'n', 'a.png', '2026/10/a.png', 'image/png', 1, 'Solo Nombre')"));

            Assert.Equal(PostgresErrorCodes.CheckViolation, error.SqlState);
            Assert.Equal("ck_media_assets_autor_completo", error.ConstraintName);
        }, ct);
    }

    // ==================================================================
    // ICurrentAdmin y la subida, contra PostgreSQL real.
    // ==================================================================

    [Fact]
    public async Task ICurrentAdmin_da_lo_de_siempre_y_ademas_el_nombre_visible_y_el_nodo_de_la_cuenta()
    {
        var ct = TestContext.Current.CancellationToken;

        await ConBaseVaciaAsync(async cadena =>
        {
            var nodoDeLaCuenta = new NodeIdentity("nodo-de-la-cuenta");
            var token = await InstalarYAbrirSesionAsync(cadena, nodoDeLaCuenta, ct);

            await using var contexto = ContextoEn(cadena, nodoDeLaCuenta);
            var actual = ComoCurrentAdmin(await PrincipalDeLaSesionAsync(contexto, token));
            var esperado = PeticionValida().Admin!;
            var cuenta = await contexto.AdminUsers.AsNoTracking().SingleAsync(ct);

            // Lo que ya daba.
            Assert.Equal(cuenta.AdminUserId, actual.AdminUserId);
            Assert.Equal(esperado.Email, actual.Email);
            Assert.Equal(AdminRole.SuperAdmin, actual.Role);
            Assert.True(actual.IsInRole(AdminRole.Editor));

            // Lo nuevo.
            Assert.Equal(esperado.FullName, actual.DisplayName);
            Assert.Equal("nodo-de-la-cuenta", actual.HomeNode);
            Assert.Equal("nodo-de-la-cuenta", cuenta.HomeNode);
        }, ct);
    }

    [Fact]
    public void Sin_sesion_ICurrentAdmin_no_lanza_y_lo_nuevo_tambien_es_nulo()
    {
        var actual = new CurrentAdmin(new HttpContextAccessor { HttpContext = new DefaultHttpContext() });

        Assert.Null(actual.AdminUserId);
        Assert.Null(actual.DisplayName);
        Assert.Null(actual.HomeNode);
        Assert.False(actual.IsInRole(AdminRole.Editor));
    }

    [Fact]
    public async Task Al_subir_la_fotografia_lleva_el_nodo_de_la_cuenta_y_no_el_de_la_instalacion()
    {
        var ct = TestContext.Current.CancellationToken;

        await ConBaseVaciaAsync(async cadena =>
        {
            // La cuenta pertenece a un nodo; la instalación que atiende la
            // subida es otro. Hoy coinciden en producción; aquí no, a propósito.
            var token = await InstalarYAbrirSesionAsync(cadena, new NodeIdentity("nodo-de-la-cuenta"), ct);

            await using var contexto = ContextoEn(cadena, new NodeIdentity("nodo-de-la-instalacion"));
            var actual = ComoCurrentAdmin(await PrincipalDeLaSesionAsync(contexto, token));

            var carpeta = Directory.CreateTempSubdirectory("sillar-medios-");
            try
            {
                var almacen = new MediaStorage(
                    contexto,
                    Options.Create(new MediaOptions { RootPath = carpeta.FullName }),
                    TimeProvider.System,
                    NullLogger<MediaStorage>.Instance);

                await using var contenido = new MemoryStream(Imagenes.PngCabecera(4, 4));
                var guardado = await almacen.SaveAsync(
                    contenido,
                    "foto.png",
                    "core",
                    new AutorDelMedio(actual.AdminUserId!.Value, actual.DisplayName!, actual.HomeNode!),
                    ct);

                var fila = await contexto.MediaAssets.AsNoTracking()
                    .SingleAsync(m => m.MediaAssetId == guardado.MediaAssetId, ct);

                Assert.Equal("nodo-de-la-instalacion", fila.OriginNode);
                Assert.Equal("nodo-de-la-cuenta", fila.CreatedByAdminUserHomeNode);
                Assert.Equal(actual.AdminUserId, fila.CreatedByAdminUserLocalId);
                Assert.Equal(PeticionValida().Admin!.FullName, fila.CreatedByAdminUserName);

                // Por el contrato de los módulos, sin sesión: sin autor, y vale.
                await using var otro = new MemoryStream(Imagenes.PngCabecera(4, 4));
                var sinAutor = await almacen.SaveAsync(otro, "otra.png", "core", ct);
                var filaSinAutor = await contexto.MediaAssets.AsNoTracking()
                    .SingleAsync(m => m.MediaAssetId == sinAutor.MediaAssetId, ct);
                Assert.Null(filaSinAutor.CreatedByAdminUserLocalId);
                Assert.Null(filaSinAutor.CreatedByAdminUserName);
                Assert.Null(filaSinAutor.CreatedByAdminUserHomeNode);
            }
            finally
            {
                carpeta.Delete(recursive: true);
            }
        }, ct);
    }

    [Fact]
    public void El_nodo_viaja_en_la_conexion_solo_si_esta_configurado()
    {
        const string cadena = "Host=localhost;Database=x;Options=-c search_path=core";

        Assert.Equal(cadena, NodoParaMigrar.ConNodo(cadena, nodo: null));
        Assert.Null(NodoParaMigrar.Configurado(Configuracion()));
        Assert.Null(NodoParaMigrar.Configurado(Configuracion("   ")));

        var conNodo = new NpgsqlConnectionStringBuilder(NodoParaMigrar.ConNodo(cadena, "sucursal 2"));
        Assert.Equal(@"-c search_path=core -c sillar.node_code=sucursal\ 2", conNodo.Options);
        Assert.Equal("Sillar__Node__Code", NodoParaMigrar.VariableDeEntorno);
    }

    // ------------------------------------------------------------------
    // Ayudantes.
    // ------------------------------------------------------------------

    private static CoreDbContext ContextoEn(string cadena, NodeIdentity nodo)
        => new(CoreDataServiceExtensions.BuildOptions(cadena), nodo, TimeProvider.System);

    private static async Task MigrarHastaLaAnteriorAsync(string cadena, CancellationToken ct)
    {
        await using var contexto = Contexto(cadena);
        await contexto.GetService<IMigrator>().MigrateAsync(MigracionAnterior, ct);
    }

    /// <summary>Aplica lo pendiente por el mismo camino que la fábrica y el instalador.</summary>
    private static async Task MigrarConNodoAsync(string cadena, string? nodo, CancellationToken ct)
    {
        await using var contexto = Contexto(NodoParaMigrar.ConNodo(cadena, nodo));
        await contexto.Database.MigrateAsync(ct);
    }

    /// <summary>Una cuenta tal como la permitía el esquema anterior: sin home_node.</summary>
    private static async Task<int> CrearCuentaAnteriorAsync(string cadena, string nombre, string correo)
        => int.Parse(await EscalarAsync(cadena,
            "INSERT INTO core.admin_users (full_name, email, password_hash, role) "
            + $"VALUES ('{nombre}', '{correo}', 'hash-de-prueba', '{AdminRole.Admin}') RETURNING admin_user_id"));

    /// <summary>Un medio tal como lo permitía el esquema anterior: con created_by.</summary>
    private static async Task<Guid> CrearMedioAnteriorAsync(string cadena, int? autor)
    {
        var id = Guid.CreateVersion7();
        await EjecutarAsync(cadena,
            "INSERT INTO core.media_assets (media_asset_id, origin_node, stored_name, relative_path, mime_type, size_bytes, created_by) "
            + $"VALUES ('{id}', '{NodoDondeNacioElArchivo}', '{id}.png', '2026/10/{id}.png', 'image/png', 10, {(autor?.ToString() ?? "NULL")})");
        return id;
    }

    /// <summary>La migración nueva no dejó rastro: ni columna, ni historial.</summary>
    private static async Task NadaCambioAsync(string cadena)
    {
        Assert.Equal("0", await EscalarAsync(cadena,
            "SELECT count(*) FROM information_schema.columns WHERE table_schema = 'core' AND table_name = 'admin_users' AND column_name = 'home_node'"));
        Assert.Equal("1", await EscalarAsync(cadena,
            "SELECT count(*) FROM information_schema.columns WHERE table_schema = 'core' AND table_name = 'media_assets' AND column_name = 'created_by'"));
        Assert.Equal(MigracionAnterior, await EscalarAsync(cadena,
            "SELECT max(\"MigrationId\") FROM core.__migrations"));
    }

    private static async Task<string> InstalarYAbrirSesionAsync(string cadena, NodeIdentity nodo, CancellationToken ct)
    {
        await using var contexto = ContextoEn(cadena, nodo);
        Assert.Equal(SetupOutcome.Completed, (await Servicio(contexto).CompleteAsync(PeticionValida(), ct)).Outcome);

        var usuario = await contexto.AdminUsers.SingleAsync(ct);
        var token = SessionTokens.CreateSessionToken();
        var ahora = TimeProvider.System.GetUtcNow();

        contexto.AdminSessions.Add(new AdminSession
        {
            AdminSessionId = Guid.CreateVersion7(),
            AdminUserId = usuario.AdminUserId,
            TokenHash = SessionTokens.Hash(token),
            CsrfTokenHash = SessionTokens.Hash("csrf-de-prueba"),
            IssuedAt = ahora,
            LastSeenAt = ahora,
            ExpiresAt = SessionPolicy.ExpiresAt(ahora, ahora)
        });
        await contexto.SaveChangesAsync(ct);

        return token;
    }

    /// <summary>El handler real lee la sesión de la base y devuelve su principal.</summary>
    private static async Task<ClaimsPrincipal> PrincipalDeLaSesionAsync(CoreDbContext contexto, string token)
    {
        var handler = new AdminSessionAuthenticationHandler(
            new OpcionesDelEsquema(), NullLoggerFactory.Instance, UrlEncoder.Default, contexto, TimeProvider.System);

        var http = new DefaultHttpContext();
        http.Request.Headers.Cookie = $"{AdminSessionCookie.Name}={token}";

        await handler.InitializeAsync(
            new AuthenticationScheme(AdminSessionAuthenticationHandler.SchemeName, null, typeof(AdminSessionAuthenticationHandler)),
            http);

        var resultado = await handler.AuthenticateAsync();
        Assert.True(resultado.Succeeded, resultado.Failure?.Message);

        return resultado.Principal!;
    }

    /// <summary>
    /// CurrentAdmin sobre ese principal. Síncrono a propósito: HttpContextAccessor
    /// guarda el contexto en un AsyncLocal, y asignarlo dentro de un método async
    /// no vuelve a quien lo llamó.
    /// </summary>
    private static CurrentAdmin ComoCurrentAdmin(ClaimsPrincipal principal)
        => new(new HttpContextAccessor { HttpContext = new DefaultHttpContext { User = principal } });

    private static async Task<string> EscalarAsync(string cadena, string sql)
    {
        await using var conexion = new NpgsqlConnection(cadena);
        await conexion.OpenAsync();
        await using var comando = new NpgsqlCommand(sql, conexion);
        return Convert.ToString(await comando.ExecuteScalarAsync()) ?? "";
    }

    private static async Task EjecutarAsync(string cadena, string sql)
    {
        await using var conexion = new NpgsqlConnection(cadena);
        await conexion.OpenAsync();
        await using var comando = new NpgsqlCommand(sql, conexion);
        await comando.ExecuteNonQueryAsync();
    }

    private sealed class OpcionesDelEsquema : IOptionsMonitor<AuthenticationSchemeOptions>
    {
        public AuthenticationSchemeOptions CurrentValue { get; } = new();
        public AuthenticationSchemeOptions Get(string? name) => CurrentValue;
        public IDisposable? OnChange(Action<AuthenticationSchemeOptions, string?> listener) => null;
    }
}
