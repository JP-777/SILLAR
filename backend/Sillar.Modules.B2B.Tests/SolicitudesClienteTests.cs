using System.Text.Json;
using Sillar.Modules.B2B.Data;
using Sillar.Modules.B2B.Solicitudes;
using Sillar.Modules.Catalog.Contracts;
using Sillar.Shared.Data.Modularity;

namespace Sillar.Modules.B2B.Tests;

/// <summary>
/// El lado del cliente contra una base de verdad: crear, ver lo propio, el
/// cupo común y lo que nunca sale al público.
/// </summary>
public sealed class SolicitudesClienteTests
{
    private static readonly Guid Cliente = Guid.Parse("00000000-0000-7000-8000-000000000001");
    private static readonly Guid Otro = Guid.Parse("00000000-0000-7000-8000-000000000002");
    private static readonly Guid Publicado = Guid.Parse("00000000-0000-7000-8000-0000000000a1");
    private static readonly Guid NoPublicado = Guid.Parse("00000000-0000-7000-8000-0000000000a2");
    private static readonly Guid DadoDeBaja = Guid.Parse("00000000-0000-7000-8000-0000000000a3");

    private static async Task ConInstalacionAsync(Func<string, Task> cuerpo)
        => await BaseEfimera.ConBaseAsync(async cadena =>
        {
            await BaseEfimera.InstalarBaseDeM07Async(cadena);
            await BaseEfimera.InstalarAsync(cadena, new B2BModule());
            await BaseEfimera.EjecutarAsync(cadena, $"""
                INSERT INTO crm.customers (customer_id, full_name, email, origin_node) VALUES
                  ('{Cliente}', 'Cliente uno', 'uno@ejemplo.test', 'central'),
                  ('{Otro}', 'Cliente dos', 'dos@ejemplo.test', 'central');
                INSERT INTO catalog.products (id, name, slug, is_public, is_active, origin_node) VALUES
                  ('{Publicado}', 'Arreglo floral Día de la Madre mediano', 'arreglo-floral-mediano', true, true, 'central'),
                  ('{NoPublicado}', 'Arreglo en preparación', 'arreglo-en-preparacion', false, true, 'central'),
                  ('{DadoDeBaja}', 'Arreglo retirado', 'arreglo-retirado', true, false, 'central');
                """);
            await cuerpo(cadena);
        });

    private static SolicitudesService Servicio(string cadena, LimiteDeSolicitudes? limite = null)
        => new(Contexto(cadena), new CatalogoDePrueba(), new LimitePorCuenta(limite ?? new(10, TimeSpan.FromHours(1))), TimeProvider.System);

    private static B2bDbContext Contexto(string cadena)
        => new(PersistenciaDeModulo.Opciones<B2bDbContext>(cadena, B2bDbContext.Schema, B2bDbContext.MigrationsHistoryTable));

    [Fact]
    public Task Una_personalizacion_sobre_un_producto_publicado_se_crea_con_su_instantanea()
        => ConInstalacionAsync(async cadena =>
        {
            var op = await Servicio(cadena).CrearPersonalizacionAsync(Cliente, new(Publicado, "  Con otro peluche  ", null, null), default);

            Assert.Equal(ResultadoSolicitud.Creada, op.Resultado);
            Assert.Equal("Con otro peluche", op.Solicitud!.Description);
            Assert.Equal("Arreglo floral Día de la Madre mediano|arreglo-floral-mediano", await BaseEfimera.EscalarAsync<string>(cadena,
                "SELECT product_name || '|' || product_slug FROM b2b.special_order_leads"));
        });

    [Theory]
    [InlineData("00000000-0000-7000-8000-0000000000a2")]
    [InlineData("00000000-0000-7000-8000-0000000000a3")]
    [InlineData("00000000-0000-7000-8000-0000000000ff")]
    public Task Sobre_un_producto_no_publicado_dado_de_baja_o_inexistente_no_se_crea_nada(string producto)
        => ConInstalacionAsync(async cadena =>
        {
            var op = await Servicio(cadena).CrearPersonalizacionAsync(Cliente, new(Guid.Parse(producto), "Cambio", null, null), default);

            Assert.Equal(ResultadoSolicitud.ProductoNoDisponible, op.Resultado);
            Assert.Equal(0L, await BaseEfimera.EscalarAsync<long>(cadena, "SELECT count(*) FROM b2b.special_order_leads"));
        });

    [Fact]
    public Task Una_peticion_invalida_no_crea_nada_ni_gasta_cupo()
        => ConInstalacionAsync(async cadena =>
        {
            var servicio = Servicio(cadena, new(1, TimeSpan.FromHours(1)));

            Assert.Equal(ResultadoSolicitud.Invalida, (await servicio.CrearVolumenAsync(Cliente, new(" ", null, null, "Cordones", 100, null), default)).Resultado);
            Assert.Equal(ResultadoSolicitud.Invalida, (await servicio.CrearPersonalizacionAsync(Cliente, new(Publicado, "Cambio", 0, null), default)).Resultado);
            Assert.Equal(ResultadoSolicitud.Creada, (await servicio.CrearVolumenAsync(Cliente, new("Colegio", null, null, "Cordones", 100, null), default)).Resultado);
        });

    /// <summary>Plan de pruebas 2.5: un solo cupo para los dos tipos.</summary>
    [Fact]
    public Task Las_dos_formas_de_solicitud_comparten_el_cupo_de_la_cuenta()
        => ConInstalacionAsync(async cadena =>
        {
            var servicio = Servicio(cadena, new(2, TimeSpan.FromHours(1)));

            Assert.Equal(ResultadoSolicitud.Creada, (await servicio.CrearPersonalizacionAsync(Cliente, new(Publicado, "Cambio", null, null), default)).Resultado);
            Assert.Equal(ResultadoSolicitud.Creada, (await servicio.CrearVolumenAsync(Cliente, new("Colegio", null, null, "Cordones", 100, null), default)).Resultado);
            var tercera = await servicio.CrearVolumenAsync(Cliente, new("Colegio", null, null, "Más cordones", 50, null), default);

            Assert.Equal(ResultadoSolicitud.Limitada, tercera.Resultado);
            Assert.NotNull(tercera.Esperar);
            Assert.Equal(2L, await BaseEfimera.EscalarAsync<long>(cadena,
                "SELECT (SELECT count(*) FROM b2b.special_order_leads) + (SELECT count(*) FROM b2b.institution_requests)"));
            Assert.Equal(ResultadoSolicitud.Creada, (await servicio.CrearVolumenAsync(Otro, new("Empresa", null, null, "Regalos", 50, null), default)).Resultado);
        });

    /// <summary>Plan de pruebas 1.9: la nota interna no sale, afirmado sobre el JSON crudo.</summary>
    [Fact]
    public Task Las_solicitudes_propias_no_traen_notas_internas_ni_las_de_otros()
        => ConInstalacionAsync(async cadena =>
        {
            var servicio = Servicio(cadena);
            await servicio.CrearPersonalizacionAsync(Cliente, new(Publicado, "Mía", null, null), default);
            await servicio.CrearVolumenAsync(Otro, new("Empresa", null, null, "Ajena", 5, null), default);
            await BaseEfimera.EjecutarAsync(cadena, "UPDATE b2b.special_order_leads SET staff_notes = 'MARCADOR-NOTA-INTERNA-7f3a'");

            var propias = await Servicio(cadena).ListarPropiasAsync(Cliente, default);
            var json = JsonSerializer.Serialize(propias);

            Assert.Single(propias);
            Assert.Equal("Mía", propias[0].Description);
            Assert.DoesNotContain("MARCADOR-NOTA-INTERNA-7f3a", json);
            Assert.DoesNotContain("staff", json, StringComparison.OrdinalIgnoreCase);
        });

    [Fact]
    public Task Una_cotizacion_ajena_o_en_borrador_no_se_distingue_de_una_inexistente()
        => ConInstalacionAsync(async cadena =>
        {
            await Servicio(cadena).CrearVolumenAsync(Cliente, new("Colegio", null, null, "Cordones", 100, null), default);
            await BaseEfimera.EjecutarAsync(cadena, $"""
                INSERT INTO b2b.quotes (quote_number, customer_id, institution_request_id, total_amount, status)
                SELECT 'PRUEBA-ENVIADA', '{Cliente}', min(institution_request_id), 80, 'enviada' FROM b2b.institution_requests;
                INSERT INTO b2b.quotes (quote_number, customer_id, institution_request_id, total_amount, status)
                SELECT 'PRUEBA-BORRADOR', '{Cliente}', min(institution_request_id), 80, 'borrador' FROM b2b.institution_requests;
                INSERT INTO b2b.quote_lines (quote_id, description, quantity, unit_price, catalog_price_at_quote)
                SELECT quote_id, 'Cordón', 100, 0.80, NULL FROM b2b.quotes WHERE quote_number = 'PRUEBA-ENVIADA';
                """);
            var servicio = Servicio(cadena);

            var propia = await servicio.ObtenerCotizacionPropiaAsync(Cliente, "PRUEBA-ENVIADA", default);
            Assert.NotNull(propia);
            Assert.True(propia.SigueValida);
            Assert.DoesNotContain("catalog", JsonSerializer.Serialize(propia), StringComparison.OrdinalIgnoreCase);

            Assert.Null(await servicio.ObtenerCotizacionPropiaAsync(Otro, "PRUEBA-ENVIADA", default));
            Assert.Null(await servicio.ObtenerCotizacionPropiaAsync(Cliente, "PRUEBA-BORRADOR", default));
            Assert.Null(await servicio.ObtenerCotizacionPropiaAsync(Cliente, "NO-EXISTE", default));
        });

    /// <summary>Lo único de M01 que usa el servicio: releer un producto elegido.</summary>
    private sealed class CatalogoDePrueba : ICatalogService
    {
        public Task<ProductPickerItem?> ObtenerParaSeleccionAsync(Guid productId, CancellationToken ct)
            => Task.FromResult(productId switch
            {
                var id when id == Publicado => new ProductPickerItem(id, "Arreglo floral Día de la Madre mediano", "arreglo-floral-mediano", null, null, 45m, false, true, true),
                var id when id == NoPublicado => new ProductPickerItem(id, "Arreglo en preparación", "arreglo-en-preparacion", null, null, null, false, false, true),
                var id when id == DadoDeBaja => new ProductPickerItem(id, "Arreglo retirado", "arreglo-retirado", null, null, 30m, false, true, false),
                _ => (ProductPickerItem?)null,
            });

        public Task<ItemSnapshot?> ObtenerItemAsync(Guid itemId, CancellationToken ct) => throw new NotSupportedException();
        public Task<ItemSnapshot?> BuscarPorCodigoAsync(string codigo, CancellationToken ct) => throw new NotSupportedException();
        public Task<IReadOnlyList<ItemSnapshot>> BuscarAsync(string texto, int limite, CancellationToken ct) => throw new NotSupportedException();
        public Task<IReadOnlyList<ItemSnapshot>> VariantesDeAsync(Guid productId, CancellationToken ct) => throw new NotSupportedException();
        public Task<bool> ItemExisteYEstaActivoAsync(Guid itemId, CancellationToken ct) => throw new NotSupportedException();
        public Task<IReadOnlyList<ProductPickerItem>> BuscarParaSeleccionAsync(string texto, int limite, CancellationToken ct) => throw new NotSupportedException();
    }
}
