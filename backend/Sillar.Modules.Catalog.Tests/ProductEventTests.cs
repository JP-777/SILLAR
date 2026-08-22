using Microsoft.EntityFrameworkCore;
using Sillar.Core.Contracts;
using Sillar.Modules.Catalog.Contracts.Events;
using Sillar.Modules.Catalog.Data;
using Sillar.Modules.Catalog.Domain;
using Sillar.Modules.Catalog.Dtos;
using Sillar.Modules.Catalog.Services;
using Sillar.Shared.Configuration;
using Sillar.Shared.Events;
using Sillar.Shared.Replication;

namespace Sillar.Modules.Catalog.Tests;

/// <summary>
/// Qué eventos emite M01, comprobados <b>por lo que llega al bus</b>.
/// </summary>
/// <remarks>
/// <para>
/// La regla que fijan estas pruebas: <b>M01 emite <c>ProductoActualizado</c>
/// siempre que cambie algo que altere lo que publica de ese producto</b>. No
/// es «se editó la fila»: cambiar una presentación o las categorías también lo
/// es, porque desde fuera eso es cambiar el producto.
/// </para>
/// <para>
/// Existen por un hueco concreto y por su simétrico. El hueco: editar una
/// presentación —el cambio más frecuente que hay en un catálogo— no emitía
/// nada, y solo se vio porque <b>el mismo cambio observable emitía o no según
/// cuántas presentaciones tuviera el producto</b>. El simétrico: dar de baja
/// emite, y había que comprobar que reactivar también, porque una conducta
/// escrita para un caso y no para su contrario es exactamente la forma del
/// primero.
/// </para>
/// <para>
/// Se comprueba con un publicador que anota lo que recibe, no leyendo el
/// código: lo que importa es qué llega al bus. Contra la base real, dentro de
/// una transacción que revierte — <b>no deja nada</b>.
/// </para>
/// </remarks>
public class ProductEventTests
{
    /// <summary>Anota lo que se publica, en orden.</summary>
    private sealed class Espia : IEventPublisher
    {
        public List<object> Publicados { get; } = [];

        public Task PublishAsync<TEvent>(TEvent domainEvent, CancellationToken cancellationToken)
            where TEvent : notnull
        {
            Publicados.Add(domainEvent);
            return Task.CompletedTask;
        }

        public IEnumerable<string> Nombres => Publicados.Select(evento => evento.GetType().Name);
    }

    /// <summary>La auditoría no es lo que se mide aquí: se traga y calla.</summary>
    private sealed class AuditoriaMuda : IAuditWriter
    {
        public Task WriteAsync(AuditEntry entry, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    /// <summary>Ningún archivo: estas pruebas no tocan medios.</summary>
    private sealed class MediosVacios : IMediaStorage
    {
        public Task<MediaAsset> SaveAsync(Stream content, string originalName, string ownerModuleCode, CancellationToken ct)
            => throw new NotSupportedException("Estas pruebas no suben archivos.");

        public Task<bool> DeleteAsync(Guid mediaAssetId, CancellationToken ct) => Task.FromResult(false);

        public string? GetPublicUrl(Guid mediaAssetId) => null;
    }

    private static CatalogDbContext? Abrir()
    {
        DotEnv.Load();
        var cadena = Environment.GetEnvironmentVariable("ConnectionStrings__Default");

        if (string.IsNullOrWhiteSpace(cadena))
        {
            return null;
        }

        var options = new DbContextOptionsBuilder<CatalogDbContext>().UseNpgsql(cadena).Options;
        return new CatalogDbContext(options, new NodeIdentity(NodeIdentity.DefaultCode), TimeProvider.System);
    }

    private static async Task<CatalogDbContext?> AbrirODescartarAsync(CancellationToken ct)
    {
        var db = Abrir();

        if (db is null)
        {
            Assert.Skip("Sin ConnectionStrings__Default: no hay base contra la que comprobar los eventos.");
            return null;
        }

        if (!await db.Database.CanConnectAsync(ct))
        {
            await db.DisposeAsync();
            Assert.Skip("La base no responde. ¿Está levantado 'docker compose up -d db'?");
            return null;
        }

        return db;
    }

    /// <summary>Un producto recién creado, con su presentación única.</summary>
    private static async Task<Product> SembrarAsync(CatalogDbContext db, CancellationToken ct)
    {
        var sufijo = Guid.NewGuid().ToString("N");

        var producto = new Product
        {
            Name = $"Producto de prueba de eventos {sufijo}",
            Slug = $"producto-prueba-eventos-{sufijo}",
            ListPrice = 10m,
        };

        producto.Items.Add(new ProductItem { VariantValue = null, SortOrder = 0 });

        db.Products.Add(producto);
        await db.SaveChangesAsync(ct);

        return producto;
    }

    private static UpdateProductRequest Edicion(Product producto, bool activo) => new(
        producto.Name,
        producto.Slug,
        null,
        null,
        null,
        producto.ListPrice,
        null,
        null,
        IsPublic: true,
        IsActive: activo);

    [Fact]
    public async Task Dar_de_baja_y_reactivar_avisan_los_dos()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var db = await AbrirODescartarAsync(ct);
        if (db is null) return;

        await using var transaccion = await db.Database.BeginTransactionAsync(ct);

        var producto = await SembrarAsync(db, ct);
        var espia = new Espia();
        var servicio = new ProductService(db, new MediosVacios(), new AuditoriaMuda(), espia, TimeProvider.System);

        // 1 · La baja avisa de que lo es.
        await servicio.DeactivateAsync(producto.Id, 1, "prueba@sillar.test", ct);
        Assert.Equal([nameof(ProductoDesactivado)], espia.Nombres);

        // 2 · **Y la vuelta también.** Sin esto, quien retiró el producto de su
        //     portada al recibir la baja no se entera nunca de que volvió, y el
        //     snapshot se queda con `IsActive = false` para siempre.
        espia.Publicados.Clear();
        await servicio.UpdateAsync(producto.Id, Edicion(producto, activo: true), 1, "prueba@sillar.test", ct);
        Assert.Contains(nameof(ProductoActualizado), espia.Nombres);

        await transaccion.RollbackAsync(ct);
    }

    [Fact]
    public async Task Editar_una_presentacion_avisa_de_que_cambio_el_producto()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var db = await AbrirODescartarAsync(ct);
        if (db is null) return;

        await using var transaccion = await db.Database.BeginTransactionAsync(ct);

        var producto = await SembrarAsync(db, ct);
        var item = producto.Items.First();
        var espia = new Espia();
        var servicio = new ProductItemService(db, new MediosVacios(), new AuditoriaMuda(), espia, TimeProvider.System);

        // Cambiar un precio de 8 a 5 no es crear ni desactivar: es editar, y
        // era el camino que no emitía nada.
        await servicio.UpdateAsync(
            item.Id,
            new UpdateProductItemRequest(null, null, null, PriceOverride: 5m, null, null, IsActive: true),
            1,
            "prueba@sillar.test",
            ct);

        Assert.Contains(nameof(ProductoActualizado), espia.Nombres);

        await transaccion.RollbackAsync(ct);
    }

    [Fact]
    public async Task La_forma_interna_del_producto_no_cambia_lo_que_se_emite()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var db = await AbrirODescartarAsync(ct);
        if (db is null) return;

        await using var transaccion = await db.Database.BeginTransactionAsync(ct);

        var producto = await SembrarAsync(db, ct);
        var items = new ProductItemService(db, new MediosVacios(), new AuditoriaMuda(), new Espia(), TimeProvider.System);

        // **La asimetría que destapó el hueco, afirmada.** Con una sola
        // presentación el código y el precio se editan por el `PUT` del
        // producto; con varias, por el de la presentación. Los dos caminos
        // tienen que avisar igual: quien consume el catálogo no puede deducir
        // cuántas presentaciones tiene un producto, ni tiene por qué.
        var conUna = new Espia();
        await new ProductService(db, new MediosVacios(), new AuditoriaMuda(), conUna, TimeProvider.System)
            .UpdateAsync(producto.Id, Edicion(producto, activo: true), 1, "prueba@sillar.test", ct);

        await items.CreateAsync(
            producto.Id,
            new CreateProductItemRequest("Azul", null, null, null, null),
            1,
            "prueba@sillar.test",
            ct);

        var conVarias = new Espia();
        await new ProductItemService(db, new MediosVacios(), new AuditoriaMuda(), conVarias, TimeProvider.System)
            .UpdateAsync(
                producto.Items.First().Id,
                new UpdateProductItemRequest(null, null, null, PriceOverride: 7m, null, null, IsActive: true),
                1,
                "prueba@sillar.test",
                ct);

        Assert.Contains(nameof(ProductoActualizado), conUna.Nombres);
        Assert.Contains(nameof(ProductoActualizado), conVarias.Nombres);

        await transaccion.RollbackAsync(ct);
    }

    [Fact]
    public async Task Cambiar_las_categorias_avisa()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var db = await AbrirODescartarAsync(ct);
        if (db is null) return;

        await using var transaccion = await db.Database.BeginTransactionAsync(ct);

        var producto = await SembrarAsync(db, ct);
        var espia = new Espia();
        var servicio = new ProductService(db, new MediosVacios(), new AuditoriaMuda(), espia, TimeProvider.System);

        // La categoría efectiva viaja en el snapshot de quien destaca un
        // producto: moverlo de sitio sin avisar deja la tarjeta diciendo la
        // categoría vieja.
        await servicio.SetCategoriesAsync(
            producto.Id,
            new SetProductCategoriesRequest([], null),
            1,
            "prueba@sillar.test",
            ct);

        Assert.Contains(nameof(ProductoActualizado), espia.Nombres);

        await transaccion.RollbackAsync(ct);
    }
}
