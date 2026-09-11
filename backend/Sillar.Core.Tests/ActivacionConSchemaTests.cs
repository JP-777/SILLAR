using Microsoft.EntityFrameworkCore;
using Npgsql;
using Sillar.Core.Services;
using Sillar.Shared.Data.Modularity;
using static Sillar.Core.Tests.BaseDePrueba;

namespace Sillar.Core.Tests;

/// <summary>
/// La activación de módulos: comprueba que su schema existe, y si no, se niega
/// sin cambiar nada.
/// </summary>
/// <remarks>
/// <para>
/// <b>De dónde sale.</b> El panel podía activar un módulo cuyo schema no
/// existía, confirmaba el éxito, y el fallo aparecía después, cuando alguien
/// entraba en el módulo. La regla ratificada: <i>antes de activar, comprobar;
/// si no está, no cambiar el estado y decir qué hacer</i>.
/// </para>
/// <para>
/// <b>La activación nunca migra.</b> Aplicar migraciones es del instalador. Ver
/// <c>InstalacionDeModulosTests</c>.
/// </para>
/// </remarks>
public sealed class ActivacionConSchemaTests
{
    private const int Operador = 1;
    private const string Correo = "operador@ejemplo.test";

    /// <summary>
    /// Deja la base con CORE migrado, las filas de módulos creadas, y además
    /// migrados los módulos que se pidan.
    /// </summary>
    private static async Task PrepararAsync(string cadena, CancellationToken ct, params string[] ademas)
    {
        foreach (var modulo in Instalador().Orden().Where(m => m.Code == "core" || ademas.Contains(m.Code)))
        {
            await ((IModuleMigrations)modulo).ApplyMigrationsAsync(cadena, ct);
        }

        await using var contexto = Contexto(cadena);
        await SincronizarAsync(contexto, ct);
    }

    private static async Task<(bool Activo, DateTimeOffset? ActivadoEn)> EstadoAsync(string cadena, string codigo, CancellationToken ct)
    {
        await using var contexto = Contexto(cadena);
        var fila = await contexto.ModuleActivations
            .Include(a => a.Module)
            .SingleAsync(a => a.Module!.Code == codigo, ct);

        return (fila.IsActive, fila.ActivatedAt);
    }

    // ------------------------------------------------------------------

    [Fact]
    public async Task Con_su_schema_presente_el_modulo_se_activa()
    {
        var ct = TestContext.Current.CancellationToken;

        await ConBaseVaciaAsync(async cadena =>
        {
            await PrepararAsync(cadena, ct, "catalog");

            await using var contexto = Contexto(cadena);
            var resultado = await Activacion(contexto).SetActiveAsync("catalog", true, Operador, Correo, ct);

            Assert.Equal(ActivationOutcome.Changed, resultado.Outcome);
            Assert.True((await EstadoAsync(cadena, "catalog", ct)).Activo);
        }, ct);
    }

    [Fact]
    public async Task Sin_su_schema_el_modulo_no_se_activa()
    {
        var ct = TestContext.Current.CancellationToken;

        await ConBaseVaciaAsync(async cadena =>
        {
            await PrepararAsync(cadena, ct);

            await using var contexto = Contexto(cadena);
            var resultado = await Activacion(contexto).SetActiveAsync("catalog", true, Operador, Correo, ct);

            Assert.Equal(ActivationOutcome.Conflict, resultado.Outcome);
        }, ct);
    }

    [Fact]
    public async Task Al_negarse_el_estado_previo_queda_intacto()
    {
        var ct = TestContext.Current.CancellationToken;

        await ConBaseVaciaAsync(async cadena =>
        {
            await PrepararAsync(cadena, ct);
            var antes = await EstadoAsync(cadena, "catalog", ct);

            await using (var contexto = Contexto(cadena))
            {
                await Activacion(contexto).SetActiveAsync("catalog", true, Operador, Correo, ct);
            }

            // Ni activo, ni con fecha de activación: fail-closed, sin rastro.
            Assert.Equal(antes, await EstadoAsync(cadena, "catalog", ct));
            Assert.False(antes.Activo);
        }, ct);
    }

    [Fact]
    public async Task El_error_nombra_el_modulo_el_schema_la_base_y_que_hacer()
    {
        var ct = TestContext.Current.CancellationToken;

        await ConBaseVaciaAsync(async cadena =>
        {
            await PrepararAsync(cadena, ct);
            var esperado = new NpgsqlConnectionStringBuilder(cadena);

            await using var contexto = Contexto(cadena);
            var error = (await Activacion(contexto).SetActiveAsync("crm", true, Operador, Correo, ct)).Error!;

            Assert.Contains("'crm'", error);
            Assert.Contains("schema 'crm'", error);
            Assert.Contains(esperado.Database!, error);
            Assert.Contains(esperado.Host!, error);

            // Accionable, y sin mandar a migrar desde aquí.
            Assert.Contains("repite la instalación", error);
            Assert.Contains("no se aplican desde este interruptor", error);
            Assert.Contains("comprueba antes la conexión", error);
        }, ct);
    }

    [Fact]
    public async Task Una_averia_distinta_no_se_convierte_en_falta_el_schema()
    {
        var ct = TestContext.Current.CancellationToken;
        var cadena = Cadena();

        if (cadena is null)
        {
            Assert.Skip("Sin ConnectionStrings__Default.");
            return;
        }

        // Una base que no existe: preguntar por el schema falla por otra cosa.
        // Si la comprobación se tragara el error, respondería «no existe» y
        // mandaría a migrar a quien tiene un problema de conexión.
        var inexistente = new NpgsqlConnectionStringBuilder(cadena) { Database = $"no_existe_{Guid.NewGuid():N}" }.ConnectionString;

        await using var contexto = Contexto(inexistente);

        var error = await Assert.ThrowsAsync<PostgresException>(
            () => Activacion(contexto).SchemaExisteAsync("catalog", ct));

        Assert.Equal(PostgresErrorCodes.InvalidCatalogName, error.SqlState);
    }

    // ------------------------------------------------------------------

    [Fact]
    public async Task Los_modulos_de_demostracion_sin_schema_se_siguen_activando()
    {
        var ct = TestContext.Current.CancellationToken;

        await ConBaseVaciaAsync(async cadena =>
        {
            await PrepararAsync(cadena, ct);

            await using var contexto = Contexto(cadena);
            var resultado = await Activacion(contexto).SetActiveAsync("demo_catalog", true, Operador, Correo, ct);

            Assert.Equal(ActivationOutcome.Changed, resultado.Outcome);
        }, ct);
    }

    [Fact]
    public async Task Un_modulo_real_sin_su_schema_no_se_activa_aunque_otros_si_lo_tengan()
    {
        var ct = TestContext.Current.CancellationToken;

        // CORE y Catálogo migrados; CMS no. CMS depende de Catálogo, que se
        // activa antes para que lo único que lo impida sea el schema.
        await ConBaseVaciaAsync(async cadena =>
        {
            await PrepararAsync(cadena, ct, "catalog");

            await using (var contexto = Contexto(cadena))
            {
                Assert.Equal(ActivationOutcome.Changed,
                    (await Activacion(contexto).SetActiveAsync("catalog", true, Operador, Correo, ct)).Outcome);
            }

            await using var otro = Contexto(cadena);
            var resultado = await Activacion(otro).SetActiveAsync("cms", true, Operador, Correo, ct);

            Assert.Equal(ActivationOutcome.Conflict, resultado.Outcome);
            Assert.Contains("schema 'cms'", resultado.Error);
        }, ct);
    }

    [Fact]
    public async Task Tras_una_instalacion_correcta_los_modulos_reales_tienen_su_schema_y_se_activan()
    {
        var ct = TestContext.Current.CancellationToken;

        await ConBaseVaciaAsync(async cadena =>
        {
            await using (var contexto = Contexto(cadena))
            {
                Assert.Equal(SetupOutcome.Completed,
                    (await Servicio(contexto).CompleteAsync(PeticionValida(), ct)).Outcome);
            }

            await using (var contexto = Contexto(cadena))
            {
                await SincronizarAsync(contexto, ct);
            }

            // En orden de dependencias, que es el del instalador.
            foreach (var modulo in Instalador().Orden().Where(m => m.Code != "core"))
            {
                await using var contexto = Contexto(cadena);
                var resultado = await Activacion(contexto).SetActiveAsync(modulo.Code, true, Operador, Correo, ct);

                Assert.True(
                    resultado.Outcome == ActivationOutcome.Changed,
                    $"'{modulo.Code}' no se activó: {resultado.Outcome} — {resultado.Error}");
            }
        }, ct);
    }
}
