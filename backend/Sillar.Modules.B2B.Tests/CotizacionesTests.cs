using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Sillar.Core.Contracts;
using Sillar.Modules.B2B.Bandeja;
using Sillar.Modules.B2B.Catalogo;
using Sillar.Modules.B2B.Cotizaciones;
using Sillar.Modules.B2B.Data;
using Sillar.Modules.B2B.Endpoints;
using Sillar.Modules.Catalog.Contracts;
using Sillar.Shared.Data.Modularity;

namespace Sillar.Modules.B2B.Tests;

/// <summary>
/// El ciclo de cotizaciones contra una base de verdad: numeración C-AAAA-NNNN,
/// edición solo en borrador, pago, E3b cerrada y umbral mayorista por contrato.
/// </summary>
public sealed class CotizacionesTests
{
    private static readonly Guid Cliente = Guid.Parse("00000000-0000-7000-8000-000000000001");
    private static readonly Guid Producto = Guid.Parse("00000000-0000-7000-8000-0000000000a1");
    private static readonly Guid Item = Guid.Parse("00000000-0000-7000-8000-0000000000b1");
    private static readonly int Anio = NumeradorDeCotizaciones.AnioDe(DateTimeOffset.UtcNow);

    private static async Task ConInstalacionAsync(Func<string, Task> cuerpo)
        => await BaseEfimera.ConBaseAsync(async cadena =>
        {
            await BaseEfimera.InstalarBaseDeM07Async(cadena);
            await BaseEfimera.InstalarAsync(cadena, new B2BModule());
            await BaseEfimera.EjecutarAsync(cadena, $"""
                INSERT INTO crm.customers (customer_id, full_name, email, origin_node) VALUES ('{Cliente}', 'Cliente', 'c@ejemplo.test', 'central');
                INSERT INTO catalog.products (id, name, slug, origin_node) VALUES ('{Producto}', 'Cordón para desfile', 'cordon-desfile', 'central');
                INSERT INTO catalog.product_items (id, product_id, origin_node) VALUES ('{Item}', '{Producto}', 'central');
                INSERT INTO b2b.institution_requests (customer_id, institution_name, description, quantity)
                VALUES ('{Cliente}', 'Colegio San Martín', '100 cordones', 100);
                """);
            await cuerpo(cadena);
        });

    private static B2bDbContext Db(string cadena)
        => new(PersistenciaDeModulo.Opciones<B2bDbContext>(cadena, B2bDbContext.Schema, B2bDbContext.MigrationsHistoryTable));

    private static CotizacionesService Servicio(string cadena, CatalogoFalso? catalogo = null, AjustesFalsos? ajustes = null)
    {
        var db = Db(cadena);
        var cat = catalogo ?? new CatalogoFalso();
        return new CotizacionesService(db, new BandejaService(db, cat), cat, new UmbralMayorista(ajustes ?? new AjustesFalsos(null)), TimeProvider.System);
    }

    private static CrearCotizacionRequest DesdeVolumen(params LineaRequest[] lineas) => new("volumen", 1, lineas);

    private static LineaRequest DeCatalogo(decimal precio = 0.70m) => new(Item, null, 100, precio);

    // --- 1 · 2 · Numeración ------------------------------------------------

    [Fact]
    public Task Dos_cotizaciones_concurrentes_reciben_correlativos_distintos_y_consecutivos()
        => ConInstalacionAsync(async cadena =>
        {
            var resultados = await Task.WhenAll(Enumerable.Range(0, 6).Select(_ => Servicio(cadena).CrearAsync(DesdeVolumen(), default)));

            var numeros = resultados.Select(r => r.Valor!.Detalle.Cotizacion.QuoteNumber).Order().ToArray();
            Assert.Equal(Enumerable.Range(1, 6).Select(n => $"C-{Anio}-{n:0000}"), numeros);
        });

    [Fact]
    public Task Un_rollback_no_consume_correlativo()
        => ConInstalacionAsync(async cadena =>
        {
            await using (var db = Db(cadena))
            {
                await using var tx = await db.Database.BeginTransactionAsync();
                Assert.Equal($"C-{Anio}-0001", await NumeradorDeCotizaciones.SiguienteAsync(db, DateTimeOffset.UtcNow, default));
                await tx.RollbackAsync();
            }

            var creada = await Servicio(cadena).CrearAsync(DesdeVolumen(), default);
            Assert.Equal($"C-{Anio}-0001", creada.Valor!.Detalle.Cotizacion.QuoteNumber);
        });

    [Fact]
    public Task El_numerador_se_niega_a_trabajar_fuera_de_una_transaccion()
        => ConInstalacionAsync(async cadena =>
        {
            await using var db = Db(cadena);
            await Assert.ThrowsAsync<InvalidOperationException>(() => NumeradorDeCotizaciones.SiguienteAsync(db, DateTimeOffset.UtcNow, default));
        });

    [Fact]
    public void El_formato_es_C_anio_y_cuatro_cifras()
    {
        Assert.Equal("C-2026-0147", NumeradorDeCotizaciones.Formatear(2026, 147));
        Assert.Equal("C-2026-12345", NumeradorDeCotizaciones.Formatear(2026, 12345));
    }

    // --- 8 · 9 · Edición solo en borrador ----------------------------------

    [Fact]
    public Task En_borrador_se_editan_las_lineas_y_se_congela_el_precio_de_catalogo()
        => ConInstalacionAsync(async cadena =>
        {
            var creada = await Servicio(cadena).CrearAsync(DesdeVolumen(), default);
            var id = creada.Valor!.Detalle.Cotizacion.Id;

            var editada = await Servicio(cadena).EditarLineasAsync(id, [DeCatalogo(0.70m), new(null, "Grabado del escudo", 1, 20m)], default);

            Assert.Equal(ResultadoBandeja.Hecho, editada.Resultado);
            Assert.Equal(90m, editada.Valor!.Detalle.Cotizacion.TotalAmount);
            var linea = editada.Valor.Detalle.Lines[0];
            Assert.Equal((0.80m, 0.70m, "Cordón para desfile"), (linea.CatalogPriceAtQuote!.Value, linea.UnitPrice, linea.ProductName!));
            Assert.Null(editada.Valor.Detalle.Lines[1].CatalogPriceAtQuote);
        });

    [Fact]
    public Task Una_enviada_no_admite_edicion_de_lineas()
        => ConInstalacionAsync(async cadena =>
        {
            var id = (await Servicio(cadena).CrearAsync(DesdeVolumen(DeCatalogo()), default)).Valor!.Detalle.Cotizacion.Id;
            Assert.Equal(ResultadoBandeja.Hecho, (await Servicio(cadena).EnviarAsync(id, default)).Resultado);

            var intento = await Servicio(cadena).EditarLineasAsync(id, [new(null, "Otra cosa", 1, 1m)], default);

            Assert.Equal(ResultadoBandeja.Conflicto, intento.Resultado);
            Assert.Contains("solo se editan en borrador", intento.Motivo);
            Assert.Equal(70m, await BaseEfimera.EscalarAsync<decimal>(cadena, "SELECT total_amount FROM b2b.quotes"));
        });

    [Fact]
    public Task Una_cotizacion_sin_lineas_no_se_envia()
        => ConInstalacionAsync(async cadena =>
        {
            var id = (await Servicio(cadena).CrearAsync(DesdeVolumen(), default)).Valor!.Detalle.Cotizacion.Id;
            Assert.Equal(ResultadoBandeja.Conflicto, (await Servicio(cadena).EnviarAsync(id, default)).Resultado);
        });

    [Fact]
    public Task Una_enviada_caducada_no_se_aprueba_y_una_vigente_si_hasta_pagarse()
        => ConInstalacionAsync(async cadena =>
        {
            var id = (await Servicio(cadena).CrearAsync(DesdeVolumen(DeCatalogo()), default)).Valor!.Detalle.Cotizacion.Id;
            await Servicio(cadena).EnviarAsync(id, default);
            await BaseEfimera.EjecutarAsync(cadena, "UPDATE b2b.quotes SET invalidated_at = now(), invalidated_reason = 'Cambió el precio.'");
            Assert.Equal(ResultadoBandeja.Conflicto, (await Servicio(cadena).AprobarAsync(id, default)).Resultado);

            await BaseEfimera.EjecutarAsync(cadena, "UPDATE b2b.quotes SET invalidated_at = NULL, invalidated_reason = NULL");
            Assert.Equal(ResultadoBandeja.Hecho, (await Servicio(cadena).AprobarAsync(id, default)).Resultado);
            Assert.Equal(ResultadoBandeja.Conflicto, (await Servicio(cadena).RegistrarPagoAsync(id, new("yape", null), "caja@ejemplo.test", default)).Resultado);
            Assert.Equal(ResultadoBandeja.Conflicto, (await Servicio(cadena).RegistrarPagoAsync(id, new("tarjeta", "x"), "caja@ejemplo.test", default)).Resultado);
            var pagada = await Servicio(cadena).RegistrarPagoAsync(id, new("yape", "OP-123"), "caja@ejemplo.test", default);
            Assert.Equal(("pagada", "caja@ejemplo.test"), (pagada.Valor!.Detalle.Cotizacion.Status, pagada.Valor.Detalle.PaidRegisteredBy!));
        });

    // --- 12 · Auditoría con el número visible ------------------------------

    [Fact]
    public Task La_auditoria_nombra_la_cotizacion_por_su_numero_visible()
        => ConInstalacionAsync(async cadena =>
        {
            var auditoria = new AuditoriaQueCaptura();
            await BandejaAdminEndpoints.CrearCotizacion(DesdeVolumen(DeCatalogo()), Servicio(cadena), auditoria, new Administrador(), default);
            await BandejaAdminEndpoints.EnviarCotizacion(1, Servicio(cadena), auditoria, new Administrador(), default);

            Assert.Equal(2, auditoria.Entradas.Count);
            Assert.All(auditoria.Entradas, e =>
            {
                Assert.Equal("b2b", e.ModuleCode);
                Assert.Matches(new Regex($@"Cotización C-{Anio}-0001\b"), e.Summary);
            });
            Assert.Contains("solicitud de volumen de «Colegio San Martín»", auditoria.Entradas[0].Summary);
        });

    // --- 4 · 5 · 6 · 7 · E3b cerrada ---------------------------------------

    private static ReaccionAlCatalogo Reaccion(string cadena, CatalogoFalso catalogo)
        => new(new ServiceCollection().AddScoped(_ => Db(cadena)).AddSingleton<ICatalogService>(catalogo)
            .BuildServiceProvider().GetRequiredService<IServiceScopeFactory>(), TimeProvider.System);

    private static async Task<int> EnviadaAsync(string cadena, CatalogoFalso catalogo, params LineaRequest[] lineas)
    {
        var id = (await Servicio(cadena, catalogo).CrearAsync(DesdeVolumen(lineas), default)).Valor!.Detalle.Cotizacion.Id;
        await Servicio(cadena, catalogo).EnviarAsync(id, default);
        return id;
    }

    private static Task<bool> InvalidadaAsync(string cadena)
        => BaseEfimera.EscalarAsync<bool>(cadena, "SELECT bool_or(invalidated_at IS NOT NULL) FROM b2b.quotes");

    [Fact]
    public Task Una_enviada_con_precio_conocido_que_cambia_se_invalida()
        => ConInstalacionAsync(async cadena =>
        {
            await EnviadaAsync(cadena, new CatalogoFalso { Precio = 0.80m }, DeCatalogo());
            await Reaccion(cadena, new CatalogoFalso { Precio = 0.95m }).ProductoActualizadoAsync(Producto, default);
            Assert.True(await InvalidadaAsync(cadena));
        });

    [Fact]
    public Task E3b_de_a_consultar_a_un_precio_no_invalida()
        => ConInstalacionAsync(async cadena =>
        {
            await EnviadaAsync(cadena, new CatalogoFalso { Precio = null }, DeCatalogo());
            Assert.Null(await BaseEfimera.EscalarAsync<object>(cadena, "SELECT catalog_price_at_quote FROM b2b.quote_lines") as decimal?);
            await Reaccion(cadena, new CatalogoFalso { Precio = 0.95m }).ProductoActualizadoAsync(Producto, default);
            Assert.False(await InvalidadaAsync(cadena));
        });

    [Fact]
    public Task De_un_precio_a_a_consultar_si_invalida()
        => ConInstalacionAsync(async cadena =>
        {
            await EnviadaAsync(cadena, new CatalogoFalso { Precio = 0.80m }, DeCatalogo());
            await Reaccion(cadena, new CatalogoFalso { Precio = null }).ProductoActualizadoAsync(Producto, default);
            Assert.True(await InvalidadaAsync(cadena));
            Assert.Contains("ahora está a consultar", await BaseEfimera.EscalarAsync<string>(cadena, "SELECT invalidated_reason FROM b2b.quotes"));
        });

    [Fact]
    public Task Las_lineas_libres_no_participan_en_la_invalidacion()
        => ConInstalacionAsync(async cadena =>
        {
            await EnviadaAsync(cadena, new CatalogoFalso(), new LineaRequest(null, "Cordón bordado a medida", 100, 1.20m));
            await Reaccion(cadena, new CatalogoFalso { Precio = 5m }).ProductoActualizadoAsync(Producto, default);
            Assert.False(await InvalidadaAsync(cadena));
        });

    // --- 13 · 14 · Umbral mayorista ----------------------------------------

    [Theory]
    [InlineData(null)]
    [InlineData("PENDIENTE_DEFINIR")]
    [InlineData("doscientos")]
    [InlineData("-1")]
    public void Sin_umbral_configurado_lo_dice_y_no_inventa_importe(string? valor)
    {
        var evaluacion = new UmbralMayorista(new AjustesFalsos(valor)).Evaluar([(0.80m, 100, true)]);

        Assert.Equal("configuracion_pendiente", evaluacion.Estado);
        Assert.Null(evaluacion.Umbral);
        Assert.Null(evaluacion.ImporteDeLista);
    }

    [Fact]
    public void Con_umbral_se_compara_el_importe_a_precio_de_lista_y_una_linea_libre_lo_hace_no_evaluable()
    {
        var umbral = new UmbralMayorista(new AjustesFalsos("80"));

        Assert.Equal(("alcanza", 80m), (umbral.Evaluar([(0.80m, 100, true)]).Estado, umbral.Evaluar([(0.80m, 100, true)]).ImporteDeLista!.Value));
        Assert.Equal("no_alcanza", umbral.Evaluar([(0.80m, 99, true)]).Estado);
        Assert.Equal("no_evaluable", umbral.Evaluar([(0.80m, 100, true), (null, 1, false)]).Estado);
    }

    [Fact]
    public Task Crear_con_el_umbral_pendiente_no_toca_ningun_precio()
        => ConInstalacionAsync(async cadena =>
        {
            var creada = await Servicio(cadena, ajustes: new AjustesFalsos("PENDIENTE_DEFINIR")).CrearAsync(DesdeVolumen(DeCatalogo(0.80m)), default);

            Assert.Equal("configuracion_pendiente", creada.Valor!.Mayorista.Estado);
            Assert.Equal(0.80m, creada.Valor.Detalle.Lines.Single().UnitPrice);
            Assert.Equal(80m, creada.Valor.Detalle.Cotizacion.TotalAmount);
        });

    /// <summary>El módulo no puede tocar <c>core.site_settings</c>: no referencia CORE ni escribe su tabla.</summary>
    [Fact]
    public void El_umbral_solo_se_consume_por_el_contrato_ISettingsReader()
    {
        var ensamblado = typeof(B2BModule).Assembly;
        Assert.DoesNotContain(ensamblado.GetReferencedAssemblies(), a => a.Name == "Sillar.Core");

        var raiz = Path.GetDirectoryName(RutaDelModulo())!;
        var codigo = Directory.EnumerateFiles(raiz, "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}") && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
            .SelectMany(f => File.ReadLines(f).Select(l => (f, l.Trim())))
            .Where(x => !x.Item2.StartsWith("//"))
            .Where(x => x.Item2.Contains("site_settings", StringComparison.OrdinalIgnoreCase) || x.Item2.Contains("SiteSetting"))
            .ToList();
        Assert.Empty(codigo);
    }

    private static string RutaDelModulo()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            var ruta = Path.Combine(dir.FullName, "backend", "Sillar.Modules.B2B", "B2BModule.cs");
            if (File.Exists(ruta)) return ruta;
        }
        throw new FileNotFoundException("No se encontró backend/Sillar.Modules.B2B.");
    }

    // --- Dobles --------------------------------------------------------------

    private sealed class AjustesFalsos(string? umbral) : ISettingsReader
    {
        public string? Get(string key) => key == UmbralMayorista.Clave ? umbral : null;
        public T? Get<T>(string key) => throw new InvalidOperationException("M07 no usa Get<T>: devolvería 0 con PENDIENTE_DEFINIR.");
        public IReadOnlyDictionary<string, string> GetPublic() => new Dictionary<string, string>();
    }

    private sealed class AuditoriaQueCaptura : IAuditWriter
    {
        public List<AuditEntry> Entradas { get; } = [];
        public Task WriteAsync(AuditEntry entry, CancellationToken cancellationToken) { Entradas.Add(entry); return Task.CompletedTask; }
    }

    private sealed class Administrador : ICurrentAdmin
    {
        public int? AdminUserId => 1;
        public string? Email => "admin@ejemplo.test";
        public string? Role => "admin";
        public bool IsInRole(string role) => role is "admin" or "editor";
    }

    private sealed class CatalogoFalso : ICatalogService
    {
        public decimal? Precio { get; init; } = 0.80m;

        private ItemSnapshot Snapshot => new(Item, Producto, "Cordón para desfile", null, null, null, Precio, "unidad");

        public Task<ItemSnapshot?> ObtenerItemAsync(Guid itemId, CancellationToken ct) => Task.FromResult(itemId == Item ? Snapshot : null);
        public Task<bool> ItemExisteYEstaActivoAsync(Guid itemId, CancellationToken ct) => Task.FromResult(itemId == Item);
        public Task<IReadOnlyList<ItemSnapshot>> VariantesDeAsync(Guid productId, CancellationToken ct)
            => Task.FromResult<IReadOnlyList<ItemSnapshot>>(productId == Producto ? [Snapshot] : []);
        public Task<ProductPickerItem?> ObtenerParaSeleccionAsync(Guid id, CancellationToken ct)
            => Task.FromResult(id == Producto ? new ProductPickerItem(id, "Cordón para desfile", "cordon-desfile", null, null, Precio, false, true, true) : null);
        public Task<ItemSnapshot?> BuscarPorCodigoAsync(string codigo, CancellationToken ct) => throw new NotSupportedException();
        public Task<IReadOnlyList<ItemSnapshot>> BuscarAsync(string texto, int limite, CancellationToken ct) => throw new NotSupportedException();
        public Task<IReadOnlyList<ProductPickerItem>> BuscarParaSeleccionAsync(string texto, int limite, CancellationToken ct) => throw new NotSupportedException();
    }
}
