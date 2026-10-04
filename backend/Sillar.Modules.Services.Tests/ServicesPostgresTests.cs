using Npgsql;
using Sillar.Modules.Services.Domain;
using Sillar.Modules.Services.Dtos;
using Sillar.Modules.Services.Services;

namespace Sillar.Modules.Services.Tests;

/// <summary>
/// M05a contra PostgreSQL real: su schema, su contrato público y su
/// desinstalación. Las reglas puras están en <see cref="ServiceRulesTests"/>.
/// </summary>
[Collection("ServicesDb")]
public sealed class ServicesPostgresTests(ServicesDbFixture fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    // --- Esquema ---------------------------------------------------------------

    [Fact]
    public async Task La_migracion_crea_solo_el_schema_services_con_su_historial_dentro()
    {
        await fixture.PrepararAsync(Ct);

        Assert.Equal("1", await fixture.EscalarAsync(
            "SELECT count(*) FROM information_schema.tables WHERE table_schema = 'services' AND table_name = '__migrations'", Ct));
        Assert.Equal("service_entries", await fixture.EscalarAsync(
            "SELECT string_agg(table_name, ',') FROM information_schema.tables WHERE table_schema = 'services' AND table_name <> '__migrations'", Ct));
    }

    [Fact]
    public async Task La_unica_referencia_cruzada_va_de_una_tabla_local_a_una_replicada_como_permite_la_ADR_018()
    {
        await fixture.PrepararAsync(Ct);

        // services.service_entries no se replica (sin origin_node) y apunta a
        // core.media_assets, que sí: combinación local → replicada, permitida.
        Assert.Equal("core.media_assets", await fixture.EscalarAsync(
            "SELECT string_agg(rn.nspname || '.' || r.relname, ',') FROM pg_constraint c "
            + "JOIN pg_class d ON d.oid = c.conrelid JOIN pg_namespace dn ON dn.oid = d.relnamespace "
            + "JOIN pg_class r ON r.oid = c.confrelid JOIN pg_namespace rn ON rn.oid = r.relnamespace "
            + "WHERE c.contype = 'f' AND dn.nspname = 'services' AND rn.nspname <> 'services'", Ct));
        Assert.Equal("0", await fixture.EscalarAsync(
            "SELECT count(*) FROM information_schema.columns WHERE table_schema = 'services' AND column_name IN ('origin_node', 'row_version')", Ct));
    }

    [Fact]
    public async Task La_direccion_publica_es_unica_sin_distinguir_mayusculas()
    {
        await fixture.PrepararAsync(Ct);
        await fixture.EjecutarAsync(
            "INSERT INTO services.service_entries (name, slug, short_description) VALUES ('Anillado', 'anillado', 'Texto')", Ct);

        // El CHECK de formato ya exige minúsculas; la unicidad con core.es_ci
        // es la segunda línea, y se prueba saltándose la primera.
        await fixture.EjecutarAsync("ALTER TABLE services.service_entries DROP CONSTRAINT ck_service_entries_slug_formato", Ct);
        var error = await Assert.ThrowsAsync<PostgresException>(() => fixture.EjecutarAsync(
            "INSERT INTO services.service_entries (name, slug, short_description) VALUES ('Otro', 'ANILLADO', 'Texto')", Ct));
        Assert.Equal(PostgresErrorCodes.UniqueViolation, error.SqlState);

        // Se devuelve el esquema a como lo deja la migración.
        await fixture.EjecutarAsync("DROP SCHEMA services CASCADE", Ct);
    }

    // --- Contrato IServiceShowcaseSnapshots ---------------------------------------

    [Fact]
    public async Task La_fotografia_de_un_servicio_publicado_trae_sus_datos_y_la_url_de_su_imagen()
    {
        await fixture.PrepararAsync(Ct);
        var medio = await fixture.CrearMedioAsync(Ct);
        var medios = new MediosDePrueba();
        medios.Activos.Add(medio);

        await using var db = fixture.CreateContext();
        var servicio = new ServiceShowcaseService(db, medios);
        var creado = (await servicio.CreateAsync(new SaveServiceRequest(
            "Anillado espiral por documento hasta 100 hojas", null, "Tapa transparente", null,
            null, "Por documento", medio, "Documento anillado con tapa transparente"), Ct)).Value!;
        await servicio.TransitionAsync(creado.Id, PublicationState.Published, Ct);

        var foto = await servicio.GetPublishedSnapshotAsync(creado.Id, Ct);

        Assert.NotNull(foto);
        Assert.Equal("anillado-espiral-por-documento-hasta-100-hojas", foto.Slug);
        Assert.Null(foto.Price); // a consultar: nulo, no cero
        Assert.Equal(medio, foto.MediaAssetId);
        Assert.Equal($"/media/2026/10/{medio}.png", foto.ImageUrl);
    }

    [Theory]
    [InlineData("borrador")]
    [InlineData("archivado")]
    [InlineData("inexistente")]
    public async Task Solo_un_servicio_publicado_tiene_fotografia(string caso)
    {
        await fixture.PrepararAsync(Ct);
        await using var db = fixture.CreateContext();
        var servicio = new ServiceShowcaseService(db, new MediosDePrueba());
        var creado = (await servicio.CreateAsync(new SaveServiceRequest("Impresión láser A4", null, "Por hoja", null, null, null, null, null), Ct)).Value!;

        var id = creado.Id;
        if (caso == "archivado") await servicio.TransitionAsync(id, PublicationState.Archived, Ct);
        if (caso == "inexistente") id += 1000;

        Assert.Null(await servicio.GetPublishedSnapshotAsync(id, Ct));
    }

    // --- Vitrina y transiciones contra la base -------------------------------------

    [Fact]
    public async Task La_vitrina_publica_solo_enseña_lo_publicado_en_su_orden()
    {
        await fixture.PrepararAsync(Ct);
        await using var db = fixture.CreateContext();
        var servicio = new ServiceShowcaseService(db, new MediosDePrueba());
        var a = (await servicio.CreateAsync(new SaveServiceRequest("Anillado", null, "Texto", null, null, null, null, null), Ct)).Value!;
        var b = (await servicio.CreateAsync(new SaveServiceRequest("Impresión", null, "Texto", null, 0m, null, null, null), Ct)).Value!;
        await servicio.CreateAsync(new SaveServiceRequest("Borrador", null, "Texto", null, null, null, null, null), Ct);
        await servicio.TransitionAsync(a.Id, PublicationState.Published, Ct);
        await servicio.TransitionAsync(b.Id, PublicationState.Published, Ct);
        var todos = (await servicio.ListAdminAsync(Ct)).Select(x => x.Id).ToList();
        await servicio.ReorderAsync(new ReorderServicesRequest([todos[1], todos[2], todos[0]]), Ct);

        var vitrina = await servicio.ListPublicAsync(Ct);

        Assert.Equal(["impresion", "anillado"], vitrina.Select(x => x.Slug));
        Assert.Equal(0m, vitrina[0].Price); // cero es gratis, distinto de «a consultar»
    }

    [Fact]
    public async Task Una_transicion_no_permitida_se_explica_en_castellano()
    {
        await fixture.PrepararAsync(Ct);
        await using var db = fixture.CreateContext();
        var servicio = new ServiceShowcaseService(db, new MediosDePrueba());
        var creado = (await servicio.CreateAsync(new SaveServiceRequest("Plastificado", null, "Texto", null, null, null, null, null), Ct)).Value!;
        await servicio.TransitionAsync(creado.Id, PublicationState.Archived, Ct);

        var resultado = await servicio.TransitionAsync(creado.Id, PublicationState.Published, Ct);

        Assert.Equal(ServiceOutcome.Conflict, resultado.Outcome);
        Assert.Equal("Un servicio archivado no puede pasar a publicado. Recarga la lista para ver su estado actual.", resultado.Error);
    }

    // --- Desinstalación ---------------------------------------------------------

    [Fact]
    public async Task El_99_drop_quita_services_dos_veces_sin_tocar_core_y_la_migracion_reinstala()
    {
        await fixture.PrepararAsync(Ct);
        await fixture.CrearMedioAsync(Ct);
        var medios = await fixture.EscalarAsync("SELECT count(*) FROM core.media_assets", Ct);
        var tablasCore = await fixture.EscalarAsync("SELECT count(*) FROM information_schema.tables WHERE table_schema = 'core'", Ct);
        var script = await File.ReadAllTextAsync(RutaDelDrop(), Ct);

        await fixture.EjecutarAsync(script, Ct);
        await fixture.EjecutarAsync(script, Ct); // idempotente

        Assert.Equal("0", await fixture.EscalarAsync("SELECT count(*) FROM pg_namespace WHERE nspname = 'services'", Ct));
        Assert.Equal(medios, await fixture.EscalarAsync("SELECT count(*) FROM core.media_assets", Ct));
        Assert.Equal(tablasCore, await fixture.EscalarAsync("SELECT count(*) FROM information_schema.tables WHERE table_schema = 'core'", Ct));

        await fixture.PrepararAsync(Ct);
        Assert.Equal("1", await fixture.EscalarAsync("SELECT count(*) FROM pg_namespace WHERE nspname = 'services'", Ct));
    }

    private static string RutaDelDrop()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            var candidato = Path.Combine(dir.FullName, "database", "modules", "services", "99_drop.sql");
            if (File.Exists(candidato)) return candidato;
        }

        throw new FileNotFoundException("No se encontró database/modules/services/99_drop.sql subiendo desde el binario de pruebas.");
    }
}
