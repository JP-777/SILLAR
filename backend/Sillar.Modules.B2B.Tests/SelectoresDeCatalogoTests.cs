using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Sillar.Modules.B2B.Bandeja;
using Sillar.Modules.B2B.Endpoints;
using Sillar.Modules.Catalog;
using Sillar.Modules.Catalog.Contracts;

namespace Sillar.Modules.B2B.Tests;

/// <summary>
/// Los dos selectores del panel de M07 (<c>/api/admin/b2b/catalog/products</c> y
/// <c>/items</c>). Sin texto no preguntan nada; proyectan solo el DTO de M07; y
/// lo que se puede elegir es lo que el contrato real de M01 da por activo.
/// </summary>
public sealed class SelectoresDeCatalogoTests
{
    private static T Valor<T>(IResult resultado) => Assert.IsType<Ok<T>>(resultado).Value!;

    private static string[] Claves<T>(T valor)
        => [.. JsonDocument.Parse(JsonSerializer.Serialize(valor, JsonSerializerOptions.Web)).RootElement.EnumerateObject().Select(p => p.Name).Order()];

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Sin_texto_no_devuelven_nada_ni_preguntan_al_catalogo(string? q)
    {
        var catalogo = new CatalogoQueNoSeDebeLlamar();

        Assert.Empty(Valor<ProductoParaElegir[]>(await BandejaAdminEndpoints.BuscarProductos(q, catalogo, default)));
        Assert.Empty(Valor<PresentacionParaElegir[]>(await BandejaAdminEndpoints.BuscarPresentaciones(q, catalogo, default)));
    }

    [Fact]
    public async Task El_selector_de_productos_proyecta_solo_el_DTO_de_M07_y_descarta_los_inactivos()
    {
        var activo = new ProductPickerItem(Guid.NewGuid(), "Cordón para desfile", "cordon", Guid.NewGuid(), "Útiles", 0.8m, true, false, true);
        var inactivo = activo with { ProductId = Guid.NewGuid(), Name = "Cordón retirado", IsActive = false };
        var catalogo = new CatalogoFijo { Productos = [activo, inactivo] };

        var elegibles = Valor<ProductoParaElegir[]>(await BandejaAdminEndpoints.BuscarProductos("cordón", catalogo, default));

        Assert.Equal([new ProductoParaElegir(activo.ProductId, "Cordón para desfile", false)], elegibles);
        Assert.Equal(["isPublic", "name", "productId"], Claves(elegibles[0]));
        Assert.Equal("cordón", catalogo.UltimaBusqueda);
    }

    [Fact]
    public async Task El_selector_de_presentaciones_usa_el_contrato_y_no_expone_codigos_internos_de_M01()
    {
        var item = new ItemSnapshot(Guid.NewGuid(), Guid.NewGuid(), "Cuaderno cuadriculado A4", "100 hojas", "CUA-100", "7750000000001", null, "unidad");
        var catalogo = new CatalogoFijo { Items = [item] };

        var elegibles = Valor<PresentacionParaElegir[]>(await BandejaAdminEndpoints.BuscarPresentaciones("cuaderno", catalogo, default));

        Assert.Equal([new PresentacionParaElegir(item.ItemId, "Cuaderno cuadriculado A4", "100 hojas", "unidad", null)], elegibles);
        Assert.Equal(["itemId", "price", "productName", "saleUnit", "variantValue"], Claves(elegibles[0]));
        Assert.Equal("cuaderno", catalogo.UltimaBusqueda);
    }

    /// <summary>
    /// Con el servicio REAL de M01 sobre una base efímera: un producto dado de baja
    /// no se puede elegir para reenlazar. (La misma prueba para presentaciones no se
    /// puede escribir en verde hoy: <c>BuscarAsync</c> de M01 no se traduce a SQL —
    /// C15—; el diagnóstico está en <c>evidencias/C15-DIAGNOSTICO-CATALOGO-REAL-20260930.txt</c>.)
    /// </summary>
    [Fact]
    public Task Con_el_catalogo_real_no_se_eligen_productos_dados_de_baja()
        => BaseEfimera.ConBaseAsync(async cadena =>
        {
            await BaseEfimera.InstalarBaseDeM07Async(cadena);
            var (activo, inactivo) = (Guid.NewGuid(), Guid.NewGuid());
            await BaseEfimera.EjecutarAsync(cadena, $"""
                INSERT INTO catalog.products (id, name, slug, is_active, origin_node) VALUES
                  ('{activo}', 'Pañuelo bordado para desfile', 'panuelo-bordado', true, 'central'),
                  ('{inactivo}', 'Pañuelo liso para desfile', 'panuelo-liso', false, 'central');
                """);

            // Lo que en el host registra otra pieza y el contexto de M01 necesita.
            var servicios = new ServiceCollection().AddSingleton(TimeProvider.System);
            new CatalogModule().RegisterServices(servicios, new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?> { ["ConnectionStrings:Default"] = cadena }).Build());
            await using var proveedor = servicios.BuildServiceProvider();
            await using var ambito = proveedor.CreateAsyncScope();
            var catalogoReal = ambito.ServiceProvider.GetRequiredService<ICatalogService>();

            var elegibles = Valor<ProductoParaElegir[]>(await BandejaAdminEndpoints.BuscarProductos("pañuelo", catalogoReal, default));

            Assert.Equal([activo], elegibles.Select(e => e.ProductId));
        });

    /// <summary>El frontend de M07 solo habla con M07: todas sus rutas cuelgan de <c>/admin/b2b</c>.</summary>
    [Fact]
    public void El_frontend_de_M07_solo_llama_a_rutas_de_M07()
    {
        var raiz = Path.GetDirectoryName(RutaDe("frontend/src/modules/b2b/services/b2b.ts"))!;
        var codigo = Directory.EnumerateFiles(Path.GetDirectoryName(raiz)!, "*.ts*", SearchOption.AllDirectories)
            .Select(File.ReadAllText).ToList();

        Assert.Contains(codigo, c => c.Contains("const base = '/admin/b2b';"));
        Assert.DoesNotContain(codigo, c => System.Text.RegularExpressions.Regex.IsMatch(c, @"['""`]/(api/)?(admin/)?catalog"));
        var servicios = File.ReadAllText(RutaDe("frontend/src/modules/b2b/services/b2b.ts"));
        Assert.All(System.Text.RegularExpressions.Regex.Matches(servicios, @"http\.(get|post|put|delete)<[^>]+>\(`([^`]+)`").Select(m => m.Groups[2].Value),
            ruta => Assert.StartsWith("${base}/", ruta));
    }

    private static string RutaDe(string relativa)
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            var ruta = Path.Combine(dir.FullName, relativa);
            if (File.Exists(ruta)) return ruta;
        }
        throw new FileNotFoundException(relativa);
    }

    private sealed class CatalogoQueNoSeDebeLlamar : ICatalogService
    {
        private static T No<T>() => throw new InvalidOperationException("Sin texto no se pregunta al catálogo.");
        public Task<IReadOnlyList<ProductPickerItem>> BuscarParaSeleccionAsync(string texto, int limite, CancellationToken ct) => No<Task<IReadOnlyList<ProductPickerItem>>>();
        public Task<IReadOnlyList<ItemSnapshot>> BuscarAsync(string texto, int limite, CancellationToken ct) => No<Task<IReadOnlyList<ItemSnapshot>>>();
        public Task<ProductPickerItem?> ObtenerParaSeleccionAsync(Guid productId, CancellationToken ct) => No<Task<ProductPickerItem?>>();
        public Task<ItemSnapshot?> ObtenerItemAsync(Guid itemId, CancellationToken ct) => No<Task<ItemSnapshot?>>();
        public Task<ItemSnapshot?> BuscarPorCodigoAsync(string codigo, CancellationToken ct) => No<Task<ItemSnapshot?>>();
        public Task<IReadOnlyList<ItemSnapshot>> VariantesDeAsync(Guid productId, CancellationToken ct) => No<Task<IReadOnlyList<ItemSnapshot>>>();
        public Task<bool> ItemExisteYEstaActivoAsync(Guid itemId, CancellationToken ct) => No<Task<bool>>();
    }

    private sealed class CatalogoFijo : ICatalogService
    {
        public IReadOnlyList<ProductPickerItem> Productos { get; init; } = [];
        public IReadOnlyList<ItemSnapshot> Items { get; init; } = [];
        public string? UltimaBusqueda { get; private set; }

        public Task<IReadOnlyList<ProductPickerItem>> BuscarParaSeleccionAsync(string texto, int limite, CancellationToken ct)
        { UltimaBusqueda = texto; return Task.FromResult(Productos); }
        public Task<IReadOnlyList<ItemSnapshot>> BuscarAsync(string texto, int limite, CancellationToken ct)
        { UltimaBusqueda = texto; return Task.FromResult(Items); }
        public Task<ProductPickerItem?> ObtenerParaSeleccionAsync(Guid productId, CancellationToken ct) => throw new NotSupportedException();
        public Task<ItemSnapshot?> ObtenerItemAsync(Guid itemId, CancellationToken ct) => throw new NotSupportedException();
        public Task<ItemSnapshot?> BuscarPorCodigoAsync(string codigo, CancellationToken ct) => throw new NotSupportedException();
        public Task<IReadOnlyList<ItemSnapshot>> VariantesDeAsync(Guid productId, CancellationToken ct) => throw new NotSupportedException();
        public Task<bool> ItemExisteYEstaActivoAsync(Guid itemId, CancellationToken ct) => throw new NotSupportedException();
    }
}
