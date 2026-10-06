using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Sillar.Modules.B2B.Bandeja;
using Sillar.Modules.B2B.Catalogo;
using Sillar.Modules.B2B.Data;
using Sillar.Modules.B2B.Solicitudes;
using Sillar.Modules.Catalog.Contracts;
using Sillar.Modules.Crm.Contracts;
using Sillar.Shared.Data.Modularity;

namespace Sillar.Modules.B2B.Tests;

/// <summary>El tramo administrativo contra una base de verdad: bandeja y reacción al catálogo.</summary>
public sealed class BandejaYCatalogoTests
{
    private static readonly Guid Cliente = Guid.Parse("00000000-0000-7000-8000-000000000001");
    private static readonly Guid Producto = Guid.Parse("00000000-0000-7000-8000-0000000000a1");
    private static readonly Guid Otro = Guid.Parse("00000000-0000-7000-8000-0000000000a2");
    private static readonly Guid Item = Guid.Parse("00000000-0000-7000-8000-0000000000b1");

    private static async Task ConInstalacionAsync(Func<string, Task> cuerpo)
        => await BaseEfimera.ConBaseAsync(async cadena =>
        {
            await BaseEfimera.InstalarBaseDeM07Async(cadena);
            await BaseEfimera.InstalarAsync(cadena, new B2BModule());
            await BaseEfimera.EjecutarAsync(cadena, $"""
                INSERT INTO crm.customers (customer_id, full_name, email, origin_node) VALUES ('{Cliente}', 'Cliente', 'c@ejemplo.test', 'central');
                INSERT INTO catalog.products (id, name, slug, origin_node) VALUES
                  ('{Producto}', 'Bolsa de dulces surtidos', 'bolsa-dulces', 'central'),
                  ('{Otro}', 'Bolsa de chocolates', 'bolsa-chocolates', 'central');
                INSERT INTO catalog.product_items (id, product_id, origin_node) VALUES ('{Item}', '{Producto}', 'central');
                INSERT INTO b2b.special_order_leads (customer_id, product_id, product_name, product_slug, description)
                VALUES ('{Cliente}', '{Producto}', 'Nombre viejo', 'slug-viejo', 'Solo con chocolates');
                """);
            await cuerpo(cadena);
        });

    private static B2bDbContext Db(string cadena)
        => new(PersistenciaDeModulo.Opciones<B2bDbContext>(cadena, B2bDbContext.Schema, B2bDbContext.MigrationsHistoryTable));

    private static BandejaService Bandeja(string cadena, CatalogoFalso? catalogo = null, ICustomerIdentityReader? clientes = null)
        => new(Db(cadena), catalogo ?? new CatalogoFalso(), clientes ?? ClienteActivo);

    /// <summary>M04 contestando que el cliente sembrado está activo.</summary>
    private static ClientesDeM04 ClienteActivo => new(new CustomerIdentity(Cliente, "Rosa Mamani", "rosa@ejemplo.test", "+51 900 000 010"));

    // --- Bandeja ------------------------------------------------------------

    [Fact]
    public Task Una_transicion_permitida_cambia_el_estado_y_nombra_la_fila()
        => ConInstalacionAsync(async cadena =>
        {
            var op = await Bandeja(cadena).CambiarEstadoPersonalizacionAsync(1, "en_revision", default);

            Assert.Equal(ResultadoBandeja.Hecho, op.Resultado);
            Assert.Equal("en_revision", op.Valor!.Solicitud.Status);
            Assert.Equal("solicitud de personalización sobre «Nombre viejo»", op.Nombre);
        });

    [Fact]
    public Task Una_transicion_no_permitida_se_rechaza_y_no_cambia_nada()
        => ConInstalacionAsync(async cadena =>
        {
            var op = await Bandeja(cadena).CambiarEstadoPersonalizacionAsync(1, "cerrada", default);

            Assert.Equal(ResultadoBandeja.Conflicto, op.Resultado);
            Assert.Contains("no puede pasar a cerrada", op.Motivo);
            Assert.Equal("recibida", await BaseEfimera.EscalarAsync<string>(cadena, "SELECT status FROM b2b.special_order_leads"));
        });

    [Fact]
    public Task Un_estado_inventado_es_invalido()
        => ConInstalacionAsync(async cadena =>
            Assert.Equal(ResultadoBandeja.Invalida, (await Bandeja(cadena).CambiarEstadoPersonalizacionAsync(1, "aprobada", default)).Resultado));

    [Fact]
    public Task Las_notas_internas_estan_en_el_detalle_del_panel_y_no_en_la_bandeja_ni_en_lo_del_cliente()
        => ConInstalacionAsync(async cadena =>
        {
            await Bandeja(cadena).NotasPersonalizacionAsync(1, "MARCADOR-NOTA-9c1e", default);

            Assert.Equal("MARCADOR-NOTA-9c1e", (await Bandeja(cadena).ObtenerPersonalizacionAsync(1, default))!.StaffNotes);
            Assert.DoesNotContain("MARCADOR-NOTA-9c1e", System.Text.Json.JsonSerializer.Serialize(await Bandeja(cadena).ListarPersonalizacionesAsync(null, default)));
            var cliente = new SolicitudesService(Db(cadena), new CatalogoFalso(), new LimitePorCuenta(LimiteDeSolicitudes.PorDefecto), TimeProvider.System);
            Assert.DoesNotContain("MARCADOR-NOTA-9c1e", System.Text.Json.JsonSerializer.Serialize(await cliente.ListarPropiasAsync(Cliente, default)));
        });

    [Fact]
    public Task Reenlazar_a_un_producto_activo_refresca_la_instantanea_y_quita_el_pendiente()
        => ConInstalacionAsync(async cadena =>
        {
            await BaseEfimera.EjecutarAsync(cadena, "UPDATE b2b.special_order_leads SET pending_relink = true");
            var op = await Bandeja(cadena).ReenlazarAsync(1, Otro, default);

            Assert.Equal(ResultadoBandeja.Hecho, op.Resultado);
            Assert.False(op.Valor!.Solicitud.PendingRelink);
            Assert.Equal("Bolsa de chocolates", op.Valor.Solicitud.ProductName);
        });

    [Fact]
    public Task Reenlazar_a_un_producto_dado_de_baja_se_rechaza()
        => ConInstalacionAsync(async cadena =>
        {
            var catalogo = new CatalogoFalso { OtroActivo = false };
            Assert.Equal(ResultadoBandeja.Conflicto, (await Bandeja(cadena, catalogo).ReenlazarAsync(1, Otro, default)).Resultado);
            Assert.Equal(Producto, await BaseEfimera.EscalarAsync<Guid>(cadena, "SELECT product_id FROM b2b.special_order_leads"));
        });

    [Fact]
    public Task La_baja_es_logica_la_saca_de_lo_del_cliente_y_no_admite_mas_cambios()
        => ConInstalacionAsync(async cadena =>
        {
            Assert.Equal(ResultadoBandeja.Hecho, (await Bandeja(cadena).BajaPersonalizacionAsync(1, default)).Resultado);

            Assert.Equal(1L, await BaseEfimera.EscalarAsync<long>(cadena, "SELECT count(*) FROM b2b.special_order_leads"));
            var cliente = new SolicitudesService(Db(cadena), new CatalogoFalso(), new LimitePorCuenta(LimiteDeSolicitudes.PorDefecto), TimeProvider.System);
            Assert.Empty(await cliente.ListarPropiasAsync(Cliente, default));
            Assert.Equal(ResultadoBandeja.NoEncontrada, (await Bandeja(cadena).CambiarEstadoPersonalizacionAsync(1, "en_revision", default)).Resultado);
        });

    // --- Identidad del cliente, por contrato con M04 ------------------------

    /// <summary>
    /// <b>La bandeja enseña una persona.</b> Antes devolvía el <c>customer_id</c> y el
    /// frontend lo declaraba sin pintarlo nunca: la fila no decía de quién era.
    /// </summary>
    [Fact]
    public Task La_bandeja_dice_de_quien_es_cada_fila_y_no_devuelve_su_identificador()
        => ConInstalacionAsync(async cadena =>
        {
            var filas = await Bandeja(cadena).ListarPersonalizacionesAsync(null, default);

            var cliente = Assert.Single(filas).Cliente;
            Assert.NotNull(cliente);
            Assert.Equal(("Rosa Mamani", "rosa@ejemplo.test", "+51 900 000 010"), (cliente!.FullName, cliente.Email, cliente.Phone));

            // Y el uuid no viaja: no es que no se pinte, es que no sale.
            Assert.DoesNotContain(
                $"{Cliente}",
                JsonSerializer.Serialize(filas),
                StringComparison.OrdinalIgnoreCase);
        });

    /// <summary>
    /// <b>Si M04 no da la ficha, la fila llega sin cliente y nadie la rellena.</b>
    /// </summary>
    /// <remarks>
    /// Es como llega una ficha de baja o bloqueada: el contrato la omite sin decir por
    /// qué. Lo que se comprueba es que M07 <b>no inventa</b> —ni el correo, ni el uuid,
    /// ni un «Cliente dado de baja»— y que el listado no se cae por una ficha ausente.
    /// </remarks>
    [Fact]
    public Task Un_cliente_que_M04_no_da_deja_la_fila_sin_cliente_y_no_se_rellena()
        => ConInstalacionAsync(async cadena =>
        {
            var filas = await Bandeja(cadena, clientes: new ClientesDeM04()).ListarPersonalizacionesAsync(null, default);

            Assert.Null(Assert.Single(filas).Cliente);
            Assert.DoesNotContain($"{Cliente}", JsonSerializer.Serialize(filas), StringComparison.OrdinalIgnoreCase);

            // Y el detalle hace lo mismo: no es que la lista sea más prudente.
            var detalle = await Bandeja(cadena, clientes: new ClientesDeM04()).ObtenerPersonalizacionAsync(1, default);
            Assert.Null(detalle!.Solicitud.Cliente);
        });

    /// <summary>
    /// <b>Un listado pregunta una vez, con los identificadores distintos.</b>
    /// </summary>
    /// <remarks>
    /// El contrato tiene <c>GetManyAsync</c> precisamente para esto. Sin él —o
    /// llamando a <c>GetAsync</c> en el bucle— una bandeja de cincuenta filas haría
    /// cincuenta lecturas, y esta prueba es la que lo nota: tres solicitudes del mismo
    /// cliente siguen siendo una consulta de un identificador.
    /// </remarks>
    [Fact]
    public Task Un_listado_pregunta_una_sola_vez_y_sin_repetir_identificadores()
        => ConInstalacionAsync(async cadena =>
        {
            await BaseEfimera.EjecutarAsync(cadena, $"""
                INSERT INTO b2b.special_order_leads (customer_id, product_id, product_name, product_slug, description) VALUES
                  ('{Cliente}', '{Producto}', 'Nombre viejo', 'slug-viejo', 'Otra más'),
                  ('{Cliente}', '{Producto}', 'Nombre viejo', 'slug-viejo', 'Y otra');
                """);

            var m04 = ClienteActivo;
            var filas = await Bandeja(cadena, clientes: m04).ListarPersonalizacionesAsync(null, default);

            Assert.Equal(3, filas.Count);
            Assert.Equal([Cliente], Assert.Single(m04.Consultas));
        });

    /// <summary>
    /// <b>Ningún DTO de la bandeja vuelve a llevar un identificador de cliente.</b>
    /// </summary>
    /// <remarks>
    /// Se comprueba por reflexión y no leyendo el código: la forma en que esto se
    /// deshace no es borrando la costura, es añadiendo «solo para enlazar» otra vez.
    /// Los identificadores no se muestran al usuario (CLAUDE.md), y un campo que viaja
    /// sin uso es un campo que algún día se pinta.
    /// </remarks>
    [Fact]
    public void Ningun_DTO_de_la_bandeja_lleva_el_identificador_del_cliente()
    {
        var dtos = new[] { typeof(PersonalizacionEnBandeja), typeof(VolumenEnBandeja), typeof(CotizacionEnBandeja) };

        foreach (var dto in dtos)
        {
            Assert.DoesNotContain(
                dto.GetProperties(),
                p => p.Name.Contains("Customer", StringComparison.Ordinal)
                  || (p.PropertyType == typeof(Guid) && p.Name.Contains("Cliente", StringComparison.Ordinal)));
        }

        // Y el que sustituye al uuid tampoco lo lleva dentro.
        Assert.DoesNotContain(typeof(ClienteDeLaBandeja).GetProperties(), p => p.PropertyType == typeof(Guid));
    }

    // --- Reacción al catálogo -----------------------------------------------

    private static async Task SembrarCotizacionesAsync(string cadena)
        => await BaseEfimera.EjecutarAsync(cadena, $"""
            INSERT INTO b2b.quotes (quote_number, customer_id, special_order_lead_id, total_amount, status)
            SELECT 'PRUEBA-' || e, '{Cliente}', 1, 10, e FROM unnest(ARRAY['borrador','enviada','aprobada','pagada']) AS e;
            INSERT INTO b2b.quote_lines (quote_id, item_id, product_name, description, quantity, unit_price, catalog_price_at_quote)
            SELECT quote_id, '{Item}', 'Bolsa de dulces surtidos', 'Bolsa', 1, 9, 10 FROM b2b.quotes;
            """);

    private static ReaccionAlCatalogo Reaccion(string cadena, CatalogoFalso catalogo)
    {
        var servicios = new ServiceCollection()
            .AddScoped(_ => Db(cadena))
            .AddSingleton<ICatalogService>(catalogo)
            .AddSingleton<Sillar.Core.Contracts.ISettingsReader>(new MonedaPen())
            .BuildServiceProvider();
        return new ReaccionAlCatalogo(servicios.GetRequiredService<IServiceScopeFactory>(), TimeProvider.System);
    }

    private static Task<string> InvalidadasAsync(string cadena)
        => BaseEfimera.EscalarAsync<string>(cadena,
            "SELECT coalesce(string_agg(status, ',' ORDER BY status), '') FROM b2b.quotes WHERE invalidated_at IS NOT NULL");

    [Fact]
    public Task Un_cambio_de_precio_invalida_solo_las_cotizaciones_enviadas()
        => ConInstalacionAsync(async cadena =>
        {
            await SembrarCotizacionesAsync(cadena);
            await Reaccion(cadena, new CatalogoFalso { PrecioItem = 12 }).ProductoActualizadoAsync(Producto, default);

            Assert.Equal("enviada", await InvalidadasAsync(cadena));
            Assert.Contains("se cotizó con 10.00 PEN y ahora está 12.00 PEN", await BaseEfimera.EscalarAsync<string>(cadena,
                "SELECT invalidated_reason FROM b2b.quotes WHERE invalidated_at IS NOT NULL"));
        });

    [Fact]
    public Task Sin_cambio_de_precio_no_se_invalida_nada_y_aplicar_dos_veces_da_lo_mismo()
        => ConInstalacionAsync(async cadena =>
        {
            await SembrarCotizacionesAsync(cadena);
            var sinCambio = Reaccion(cadena, new CatalogoFalso { PrecioItem = 10 });
            await sinCambio.ProductoActualizadoAsync(Producto, default);
            Assert.Equal("", await InvalidadasAsync(cadena));

            var conCambio = Reaccion(cadena, new CatalogoFalso { PrecioItem = 12 });
            await conCambio.ProductoActualizadoAsync(Producto, default);
            var primera = await BaseEfimera.EscalarAsync<DateTime>(cadena, "SELECT invalidated_at FROM b2b.quotes WHERE invalidated_at IS NOT NULL");
            await conCambio.ProductoActualizadoAsync(Producto, default);
            Assert.Equal(primera, await BaseEfimera.EscalarAsync<DateTime>(cadena, "SELECT invalidated_at FROM b2b.quotes WHERE invalidated_at IS NOT NULL"));
        });

    /// <summary>E3b, cerrada por JP el 30/09: una línea «a consultar» (precio de catálogo nulo) no se evalúa.</summary>
    [Fact]
    public Task E3b_una_linea_a_consultar_no_invalida_su_cotizacion_aunque_el_precio_cambie()
        => ConInstalacionAsync(async cadena =>
        {
            await SembrarCotizacionesAsync(cadena);
            await BaseEfimera.EjecutarAsync(cadena, "UPDATE b2b.quote_lines SET catalog_price_at_quote = NULL");
            await Reaccion(cadena, new CatalogoFalso { PrecioItem = 12 }).ProductoActualizadoAsync(Producto, default);

            Assert.Equal("", await InvalidadasAsync(cadena));
        });

    [Fact]
    public Task Actualizar_el_producto_refresca_la_instantanea_de_la_solicitud()
        => ConInstalacionAsync(async cadena =>
        {
            await Reaccion(cadena, new CatalogoFalso()).ProductoActualizadoAsync(Producto, default);

            Assert.Equal("Bolsa de dulces surtidos|bolsa-dulces|False", await BaseEfimera.EscalarAsync<string>(cadena,
                "SELECT product_name || '|' || product_slug || '|' || (CASE WHEN pending_relink THEN 'True' ELSE 'False' END) FROM b2b.special_order_leads"));
        });

    /// <summary>La asimetría de la SPEC §5: desactivar marca el reenlace y no invalida ninguna cotización.</summary>
    [Fact]
    public Task Desactivar_el_producto_marca_el_reenlace_y_no_invalida_ninguna_cotizacion()
        => ConInstalacionAsync(async cadena =>
        {
            await SembrarCotizacionesAsync(cadena);
            await Reaccion(cadena, new CatalogoFalso { PrecioItem = 12 }).ProductoDesactivadoAsync(Producto, default);

            Assert.True(await BaseEfimera.EscalarAsync<bool>(cadena, "SELECT pending_relink FROM b2b.special_order_leads"));
            Assert.Equal("", await InvalidadasAsync(cadena));
            Assert.Equal("Nombre viejo", await BaseEfimera.EscalarAsync<string>(cadena, "SELECT product_name FROM b2b.special_order_leads"));
        });

    private sealed class CatalogoFalso : ICatalogService
    {
        public decimal? PrecioItem { get; init; } = 10;
        public bool OtroActivo { get; init; } = true;

        public Task<ProductPickerItem?> ObtenerParaSeleccionAsync(Guid id, CancellationToken ct)
            => Task.FromResult(id == Producto
                ? new ProductPickerItem(id, "Bolsa de dulces surtidos", "bolsa-dulces", null, null, PrecioItem, false, true, true)
                : id == Otro ? new ProductPickerItem(id, "Bolsa de chocolates", "bolsa-chocolates", null, null, 15m, false, true, OtroActivo)
                : (ProductPickerItem?)null);

        public Task<IReadOnlyList<ItemSnapshot>> VariantesDeAsync(Guid productId, CancellationToken ct)
            => Task.FromResult<IReadOnlyList<ItemSnapshot>>(productId == Producto
                ? [new ItemSnapshot(Item, Producto, "Bolsa de dulces surtidos", null, null, null, PrecioItem, null)] : []);

        public Task<ItemSnapshot?> ObtenerItemAsync(Guid itemId, CancellationToken ct) => throw new NotSupportedException();
        public Task<ItemSnapshot?> BuscarPorCodigoAsync(string codigo, CancellationToken ct) => throw new NotSupportedException();
        public Task<IReadOnlyList<ItemSnapshot>> BuscarAsync(string texto, int limite, CancellationToken ct) => throw new NotSupportedException();
        public Task<bool> ItemExisteYEstaActivoAsync(Guid itemId, CancellationToken ct) => throw new NotSupportedException();
        public Task<IReadOnlyList<ProductPickerItem>> BuscarParaSeleccionAsync(string texto, int limite, CancellationToken ct) => throw new NotSupportedException();
    }

    private sealed class MonedaPen : Sillar.Core.Contracts.ISettingsReader
    {
        public string? Get(string key) => key == "currency_code" ? "PEN" : null;
        public T? Get<T>(string key) => default;
        public IReadOnlyDictionary<string, string> GetPublic() => new Dictionary<string, string>();
    }
}
