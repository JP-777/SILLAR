using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using Sillar.Core.Endpoints;
using Sillar.Core.Services;
using Sillar.Core.Setup;
using Sillar.Shared.Configuration;
using Sillar.Shared.Data.Modularity;
using Sillar.Shared.Modularity;
using static Sillar.Core.Tests.BaseDePrueba;

namespace Sillar.Core.Tests;

/// <summary>
/// El instalador: deja preparados los schemas de todos los módulos desplegados,
/// y se niega a tocar una base que no sea de SILLAR.
/// </summary>
/// <remarks>
/// <para>
/// <b>La regla ratificada es «el instalador aplica; la activación comprueba».</b>
/// Estas pruebas son la mitad que aplica. La mitad que comprueba está en
/// <c>ActivacionConSchemaTests</c>.
/// </para>
/// <para>
/// Todas corren contra una base creada vacía de verdad y destruida al terminar.
/// </para>
/// </remarks>
public sealed class InstalacionDeModulosTests
{
    private static readonly string[] SchemasReales = ["catalog", "cms", "core", "crm"];

    // ==================================================================
    // El orden, que no puede depender de cómo llegan los módulos.
    // ==================================================================

    [Fact]
    public void CORE_se_instala_el_primero_y_no_por_casualidad()
    {
        var orden = Instalador().Orden().Select(modulo => modulo.Code).ToList();

        Assert.Equal("core", orden[0]);

        // Solo los que tienen migraciones: los de demostración no tienen schema.
        Assert.Equal(SchemasReales.Order().ToList(), orden.Order().ToList());
    }

    [Fact]
    public void El_orden_es_el_mismo_llegue_como_llegue_el_catalogo_de_modulos()
    {
        var desordenado = Instalador().Orden().Select(modulo => modulo.Code).ToList();
        var alReves = Instalador(new(Desplegados().Modules.Reverse().ToList()))
            .Orden().Select(modulo => modulo.Code).ToList();

        Assert.Equal(desordenado, alReves);
    }

    // ==================================================================
    // Estado 1 — base prístina.
    // ==================================================================

    [Fact]
    public async Task Base_pristina_la_instalacion_deja_todos_los_schemas_desplegados_y_termina_operativa()
    {
        var ct = TestContext.Current.CancellationToken;

        await ConBaseVaciaAsync(async cadena =>
        {
            await using var contexto = Contexto(cadena);

            var resultado = await Servicio(contexto).CompleteAsync(PeticionValida(), ct);

            Assert.Equal(SetupOutcome.Completed, resultado.Outcome);

            // Los cuatro schemas reales, y ninguno de los módulos de demostración.
            Assert.Equal(SchemasReales, (await SchemasAsync(cadena, ct)).Where(s => s != "public").Order());

            // Operativa: el estado ya es «completada», no «faltan migraciones».
            await using var otro = Contexto(cadena);
            Assert.Equal(SetupState.Completed, await Servicio(otro).GetStateAsync(ct));

            // Y ningún módulo tiene migraciones pendientes.
            foreach (var modulo in Instalador().Orden().Cast<IModuleMigrations>())
            {
                Assert.Equal(
                    modulo.KnownMigrations(cadena),
                    await modulo.AppliedMigrationsAsync(cadena, ct));
            }
        }, ct);
    }

    [Fact]
    public async Task POST_setup_sobre_base_pristina_responde_201()
    {
        var ct = TestContext.Current.CancellationToken;

        await ConBaseVaciaAsync(async cadena =>
        {
            await using var contexto = Contexto(cadena);

            var respuesta = await SetupEndpoints.Complete(
                PeticionValida(), Servicio(contexto), Reiniciador(), new DefaultHttpContext(), ct);

            Assert.IsType<Created<Sillar.Core.Dtos.SetupResponse>>(respuesta);
        }, ct);
    }

    [Fact]
    public async Task Las_extensiones_en_public_no_convierten_la_base_en_ajena()
    {
        var ct = TestContext.Current.CancellationToken;

        // **La dirección que hace posible reanudar.** Catálogo y CRM crean
        // pg_trgm y unaccent sin schema, así que caen en public. Una base con
        // solo eso tiene que seguir siendo instalable: sus objetos pertenecen a
        // una extensión, y eso lo dice PostgreSQL, no una lista nuestra.
        await ConBaseVaciaAsync(async cadena =>
        {
            await EjecutarAsync(cadena, "CREATE EXTENSION IF NOT EXISTS pg_trgm", ct);

            var diagnostico = await Inspeccionar(cadena, ct);

            Assert.Equal(EstadoDelDestino.Pristino, diagnostico.Estado);
            Assert.Empty(diagnostico.Evidencias);
        }, ct);
    }

    // ==================================================================
    // Estado 2 — instalación SILLAR a medias: se continúa.
    // ==================================================================

    [Fact]
    public async Task Repetir_la_instalacion_no_reaplica_nada_y_sigue_operativa()
    {
        var ct = TestContext.Current.CancellationToken;

        await ConBaseVaciaAsync(async cadena =>
        {
            await using (var primero = Contexto(cadena))
            {
                Assert.Equal(SetupOutcome.Completed, (await Servicio(primero).CompleteAsync(PeticionValida(), ct)).Outcome);
            }

            var aplicadasAntes = await AplicadasPorModulo(cadena, ct);

            await using var segundo = Contexto(cadena);
            var resultado = await Servicio(segundo).CompleteAsync(PeticionValida(), ct);

            // La segunda no instala otra vez: la protección de siempre contra la
            // doble instalación sigue en su sitio.
            Assert.Equal(SetupOutcome.AlreadyInstalled, resultado.Outcome);

            // Y no ha reaplicado nada: el historial es exactamente el mismo.
            Assert.Equal(aplicadasAntes, await AplicadasPorModulo(cadena, ct));

            await using var tercero = Contexto(cadena);
            Assert.Equal(SetupState.Completed, await Servicio(tercero).GetStateAsync(ct));
        }, ct);
    }

    [Fact]
    public async Task Una_instalacion_a_medias_se_reanuda_y_se_completa()
    {
        var ct = TestContext.Current.CancellationToken;

        // El caso que la corrección del líder exige: se migraron algunos
        // módulos, otro falló, el POST terminó en rojo. Se simula dejando
        // aplicados solo CORE y Catálogo, que es lo que quedaría.
        await ConBaseVaciaAsync(async cadena =>
        {
            var orden = Instalador().Orden();
            foreach (var modulo in orden.Where(m => m.Code is "core" or "catalog").Cast<IModuleMigrations>())
            {
                await modulo.ApplyMigrationsAsync(cadena, ct);
            }

            Assert.Equal(["catalog", "core"], (await SchemasAsync(cadena, ct)).Where(s => s != "public").Order());

            var antes = await Inspeccionar(cadena, ct);
            Assert.Equal(EstadoDelDestino.Reanudable, antes.Estado);

            await using var contexto = Contexto(cadena);
            var resultado = await Servicio(contexto).CompleteAsync(PeticionValida(), ct);

            Assert.Equal(SetupOutcome.Completed, resultado.Outcome);
            Assert.Equal(SchemasReales, (await SchemasAsync(cadena, ct)).Where(s => s != "public").Order());
        }, ct);
    }

    [Fact]
    public async Task Un_schema_de_SILLAR_cuya_primera_migracion_fallo_sigue_siendo_reanudable()
    {
        var ct = TestContext.Current.CancellationToken;

        // Si la primera migración de un módulo falla, EF ya dejó creados su
        // schema y su historial vacío. Eso es SILLAR a medias, no algo ajeno.
        await ConBaseVaciaAsync(async cadena =>
        {
            await EjecutarAsync(cadena,
                "CREATE SCHEMA cms; " +
                "CREATE TABLE cms.__migrations (\"MigrationId\" varchar(150) PRIMARY KEY, \"ProductVersion\" varchar(32) NOT NULL)",
                ct);

            Assert.Equal(EstadoDelDestino.Reanudable, (await Inspeccionar(cadena, ct)).Estado);
        }, ct);
    }

    // ==================================================================
    // Estado 3 — destino no seguro: se niega ANTES de migrar.
    // ==================================================================

    [Fact]
    public async Task Un_schema_ajeno_hace_que_se_niegue_antes_de_migrar_nada()
    {
        var ct = TestContext.Current.CancellationToken;

        await ConBaseVaciaAsync(async cadena =>
        {
            await EjecutarAsync(cadena, "CREATE SCHEMA contabilidad; CREATE TABLE contabilidad.asientos (id int)", ct);

            await using var contexto = Contexto(cadena);
            var resultado = await Servicio(contexto, new AuditoriaQueNoDeberiaLlamarse()).CompleteAsync(PeticionValida(), ct);

            Assert.Equal(SetupOutcome.UnsafeTarget, resultado.Outcome);
            Assert.Contains(resultado.Destino!.Evidencias, e => e.Contains("contabilidad"));

            // **Y la base quedó sin ninguna migración de SILLAR.** Ni un schema
            // nuestro: lo único que hay es lo que ya había.
            Assert.Equal(["contabilidad", "public"], await SchemasAsync(cadena, ct));
        }, ct);
    }

    [Fact]
    public async Task Una_tabla_ajena_en_public_hace_que_se_niegue()
    {
        var ct = TestContext.Current.CancellationToken;

        // La otra dirección del caso de las extensiones: un objeto de public que
        // NO pertenece a ninguna extensión sí cuenta, y basta.
        await ConBaseVaciaAsync(async cadena =>
        {
            await EjecutarAsync(cadena, "CREATE EXTENSION IF NOT EXISTS pg_trgm; CREATE TABLE public.facturas (id int)", ct);

            var diagnostico = await Inspeccionar(cadena, ct);

            Assert.Equal(EstadoDelDestino.NoSeguro, diagnostico.Estado);
            Assert.Contains(diagnostico.Evidencias, e => e.Contains("public.facturas"));
            Assert.DoesNotContain(diagnostico.Evidencias, e => e.Contains("gtrgm") || e.Contains("similarity"));

            await using var contexto = Contexto(cadena);
            await Servicio(contexto, new AuditoriaQueNoDeberiaLlamarse()).CompleteAsync(PeticionValida(), ct);

            Assert.Equal(["public"], await SchemasAsync(cadena, ct));
        }, ct);
    }

    [Fact]
    public async Task Un_schema_con_nuestro_nombre_pero_sin_nuestro_historial_no_se_toca()
    {
        var ct = TestContext.Current.CancellationToken;

        // El nombre no basta. Un «catalog» con tablas y sin historial de EF es
        // de otro, o se hizo a mano: migrar encima mezclaría lo suyo y lo nuestro.
        await ConBaseVaciaAsync(async cadena =>
        {
            await EjecutarAsync(cadena, "CREATE SCHEMA catalog; CREATE TABLE catalog.productos_viejos (id int)", ct);

            var diagnostico = await Inspeccionar(cadena, ct);

            Assert.Equal(EstadoDelDestino.NoSeguro, diagnostico.Estado);
            Assert.Contains(diagnostico.Evidencias, e => e.Contains("«catalog»") && e.Contains("historial"));
        }, ct);
    }

    [Fact]
    public async Task Un_historial_con_migraciones_que_este_binario_no_conoce_no_se_toca()
    {
        var ct = TestContext.Current.CancellationToken;

        await ConBaseVaciaAsync(async cadena =>
        {
            var core = (IModuleMigrations)Instalador().Orden()[0];
            await core.ApplyMigrationsAsync(cadena, ct);
            await EjecutarAsync(cadena,
                "INSERT INTO core.__migrations VALUES ('29991231000000_DeUnaVersionFutura', '99.0.0')", ct);

            var diagnostico = await Inspeccionar(cadena, ct);

            Assert.Equal(EstadoDelDestino.NoSeguro, diagnostico.Estado);
            Assert.Contains(diagnostico.Evidencias, e => e.Contains("29991231000000_DeUnaVersionFutura"));
        }, ct);
    }

    [Fact]
    public async Task El_503_nombra_base_servidor_puerto_y_la_evidencia_y_manda_comprobar_la_conexion()
    {
        var ct = TestContext.Current.CancellationToken;

        await ConBaseVaciaAsync(async cadena =>
        {
            await EjecutarAsync(cadena, "CREATE SCHEMA contabilidad", ct);
            var esperado = new NpgsqlConnectionStringBuilder(cadena);

            await using var contexto = Contexto(cadena);
            var respuesta = await SetupEndpoints.Complete(
                PeticionValida(), Servicio(contexto, new AuditoriaQueNoDeberiaLlamarse()),
                Reiniciador(), new DefaultHttpContext(), ct);

            // No culpa a los datos: no es un ValidationProblem.
            Assert.IsNotType<ValidationProblem>(respuesta);

            var problema = Assert.IsType<ProblemHttpResult>(respuesta);
            var texto = $"{problema.ProblemDetails.Title}\n{problema.ProblemDetails.Detail}";

            Assert.Equal(StatusCodes.Status503ServiceUnavailable, problema.StatusCode);
            Assert.Contains(esperado.Database!, texto);
            Assert.Contains(esperado.Host!, texto);
            Assert.Contains($"{esperado.Port}", texto);
            Assert.Contains("contabilidad", texto);
            Assert.Contains("No se ha aplicado ninguna migración", texto);
            Assert.Contains("Comprueba PRIMERO la conexión", texto);

            // Y no lo disfraza de «faltan migraciones»: el instalador podía
            // crearlas, y se ha negado.
            Assert.DoesNotContain("Faltan las migraciones", texto);
            Assert.DoesNotContain(esperado.Password ?? "sin-contrasena-en-la-cadena", texto);
        }, ct);
    }

    // ==================================================================
    // La decisión, sin base: para provocarla en un segundo.
    // ==================================================================

    private static readonly DestinoDeConexion Destino = new("servidor", "5432", "base");
    private static readonly HashSet<string> DeSillar = ["core", "catalog"];

    [Fact]
    public void Clasificar_sin_nada_es_pristina()
        => Assert.Equal(
            EstadoDelDestino.Pristino,
            DestinoDeInstalacion.Clasificar(new(["public"], [], new Dictionary<string, SchemaDeSillarObservado>()), DeSillar, Destino).Estado);

    [Fact]
    public void Clasificar_con_solo_lo_nuestro_es_reanudable()
        => Assert.Equal(
            EstadoDelDestino.Reanudable,
            DestinoDeInstalacion.Clasificar(
                new(["core", "public"], [], new Dictionary<string, SchemaDeSillarObservado>
                {
                    ["core"] = new(TieneHistorial: true, Objetos: ["installation (tabla)"], Aplicadas: ["a"], Conocidas: ["a", "b"]),
                }),
                DeSillar, Destino).Estado);

    [Fact]
    public void Clasificar_con_algo_ajeno_no_es_seguro()
        => Assert.Equal(
            EstadoDelDestino.NoSeguro,
            DestinoDeInstalacion.Clasificar(new(["otra", "public"], [], new Dictionary<string, SchemaDeSillarObservado>()), DeSillar, Destino).Estado);

    // ==================================================================

    private static Task<DiagnosticoDelDestino> Inspeccionar(string cadena, CancellationToken ct)
        => DestinoDeInstalacion.InspeccionarAsync(
            cadena,
            [.. Instalador().Orden().Select(modulo => (modulo.Schema!, (IModuleMigrations)modulo))],
            ct);

    private static async Task<Dictionary<string, List<string>>> AplicadasPorModulo(string cadena, CancellationToken ct)
    {
        var resultado = new Dictionary<string, List<string>>();
        foreach (var modulo in Instalador().Orden())
        {
            resultado[modulo.Code] = [.. await ((IModuleMigrations)modulo).AppliedMigrationsAsync(cadena, ct)];
        }

        return resultado;
    }
}
