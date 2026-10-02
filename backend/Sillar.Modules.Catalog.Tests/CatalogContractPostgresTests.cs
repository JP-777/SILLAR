using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Sillar.Modules.Catalog.Data;
using Sillar.Modules.Catalog.Domain;
using Sillar.Modules.Catalog.Services;
using Sillar.Shared.Configuration;
using Sillar.Shared.Replication;

namespace Sillar.Modules.Catalog.Tests;

/// <summary>C15: contrato ICatalogService ejecutado y SQL observado en PostgreSQL real.</summary>
public sealed class CatalogContractPostgresTests
{
    private static async Task<(CatalogDbContext Db, SqlCapture Sql)> OpenAsync(CancellationToken ct)
    {
        DotEnv.Load();
        var connection = Environment.GetEnvironmentVariable("ConnectionStrings__Default");
        if (string.IsNullOrWhiteSpace(connection))
        {
            Assert.Skip("Sin ConnectionStrings__Default: C15 exige PostgreSQL real.");
        }

        var capture = new SqlCapture();
        var options = new DbContextOptionsBuilder<CatalogDbContext>()
            .UseNpgsql(connection)
            .AddInterceptors(capture)
            .Options;
        var db = new CatalogDbContext(options, new NodeIdentity(NodeIdentity.DefaultCode), TimeProvider.System);
        if (!await db.Database.CanConnectAsync(ct))
        {
            await db.DisposeAsync();
            Assert.Skip("PostgreSQL no responde: C15 no se sustituye por mocks.");
        }
        return (db, capture);
    }

    private static Product Product(string marker, bool active = true, decimal? listPrice = 21.40m, string? saleUnit = "unidad")
        => new()
        {
            Name = $"Cuaderno C15 {marker}",
            Slug = $"cuaderno-c15-{marker}",
            IsActive = active,
            ListPrice = listPrice,
            SaleUnit = saleUnit,
        };

    private static ProductItem Item(string marker, bool active = true, decimal? price = null, int order = 0)
        => new()
        {
            VariantValue = $"A4 {marker}",
            Code = $"COD-{marker}",
            Barcode = $"BAR-{marker}",
            PriceOverride = price,
            IsActive = active,
            SortOrder = order,
        };

    [Fact]
    public async Task ObtenerItem_traduce_con_inner_join_y_relee_variante_inactiva()
    {
        var ct = TestContext.Current.CancellationToken;
        var (db, sql) = await OpenAsync(ct);
        await using (db)
        await using (var tx = await db.Database.BeginTransactionAsync(ct))
        {
            var marker = Guid.NewGuid().ToString("N");
            var product = Product(marker, listPrice: 14.25m, saleUnit: "millar");
            var item = Item(marker, active: false);
            product.Items.Add(item);
            db.Products.Add(product);
            await db.SaveChangesAsync(ct);
            sql.Clear();

            var snapshot = Assert.IsType<Contracts.ItemSnapshot>(await new CatalogService(db).ObtenerItemAsync(item.Id, ct));

            Assert.Equal(item.Id, snapshot.ItemId);
            Assert.Equal(product.Id, snapshot.ProductId);
            Assert.Equal(product.Name, snapshot.ProductName);
            Assert.Equal(item.VariantValue, snapshot.VariantValue);
            Assert.Equal(item.Code, snapshot.Code);
            Assert.Equal(item.Barcode, snapshot.Barcode);
            Assert.Equal(14.25m, snapshot.Price);
            Assert.Equal("millar", snapshot.SaleUnit);
            sql.AssertInnerJoin();
            await tx.RollbackAsync(ct);
        }
    }

    [Fact]
    public async Task BuscarPorCodigo_traduce_con_inner_join_y_respeta_actividad_y_proyeccion()
    {
        var ct = TestContext.Current.CancellationToken;
        var (db, sql) = await OpenAsync(ct);
        await using (db)
        await using (var tx = await db.Database.BeginTransactionAsync(ct))
        {
            var marker = Guid.NewGuid().ToString("N");
            var activeProduct = Product(marker, listPrice: 18m, saleUnit: "caja");
            var active = Item(marker, price: 19.90m);
            activeProduct.Items.Add(active);
            var inactiveItemProduct = Product(marker + "i");
            var inactiveItem = Item(marker + "i", active: false);
            inactiveItemProduct.Items.Add(inactiveItem);
            var inactiveProduct = Product(marker + "p", active: false);
            var itemOfInactiveProduct = Item(marker + "p");
            inactiveProduct.Items.Add(itemOfInactiveProduct);
            db.Products.AddRange(activeProduct, inactiveItemProduct, inactiveProduct);
            await db.SaveChangesAsync(ct);
            var service = new CatalogService(db);
            sql.Clear();

            var byCode = Assert.IsType<Contracts.ItemSnapshot>(await service.BuscarPorCodigoAsync(active.Code!, ct));
            var byBarcode = Assert.IsType<Contracts.ItemSnapshot>(await service.BuscarPorCodigoAsync(active.Barcode!, ct));
            Assert.Equal(19.90m, byCode.Price);
            Assert.Equal(active.Code, byCode.Code);
            Assert.Equal(active.Barcode, byBarcode.Barcode);
            Assert.Equal("caja", byCode.SaleUnit);
            Assert.Null(await service.BuscarPorCodigoAsync(inactiveItem.Code!, ct));
            Assert.Null(await service.BuscarPorCodigoAsync(itemOfInactiveProduct.Code!, ct));
            sql.AssertInnerJoin();
            await tx.RollbackAsync(ct);
        }
    }

    [Fact]
    public async Task Buscar_traduce_con_inner_join_full_text_filtros_orden_y_limite_cero()
    {
        var ct = TestContext.Current.CancellationToken;
        var (db, sql) = await OpenAsync(ct);
        await using (db)
        await using (var tx = await db.Database.BeginTransactionAsync(ct))
        {
            var marker = Guid.NewGuid().ToString("N");
            var zeta = Product(marker, listPrice: null); zeta.Name = $"Zeta impresiones {marker}"; zeta.Items.Add(Item(marker));
            var alfa = Product(marker + "a", listPrice: null); alfa.Name = $"Alfa impresión {marker}"; alfa.Items.Add(Item(marker + "a"));
            var inactiveItemProduct = Product(marker + "i"); inactiveItemProduct.Name = $"Impresión inactiva item {marker}"; inactiveItemProduct.Items.Add(Item(marker + "i", active: false));
            var inactiveProduct = Product(marker + "p", active: false); inactiveProduct.Name = $"Impresión inactiva producto {marker}"; inactiveProduct.Items.Add(Item(marker + "p"));
            db.Products.AddRange(zeta, alfa, inactiveItemProduct, inactiveProduct);
            await db.SaveChangesAsync(ct);
            var service = new CatalogService(db);
            sql.Clear();

            var found = await service.BuscarAsync($"impresiones {marker}", 20, ct);
            var own = found.Where(x => x.ProductId == alfa.Id || x.ProductId == zeta.Id).ToList();
            Assert.Equal([alfa.Id, zeta.Id], own.Select(x => x.ProductId));
            Assert.DoesNotContain(found, x => x.ProductId == inactiveItemProduct.Id || x.ProductId == inactiveProduct.Id);
            Assert.All(own, x => Assert.Null(x.Price));
            Assert.Empty(await service.BuscarAsync("impresión", 0, ct));
            sql.AssertInnerJoin();
            Assert.Contains("to_tsvector('spanish'", sql.All, StringComparison.OrdinalIgnoreCase);
            await tx.RollbackAsync(ct);
        }
    }

    [Fact]
    public async Task VariantesDe_traduce_con_inner_join_y_conserva_solo_item_activo_y_orden()
    {
        var ct = TestContext.Current.CancellationToken;
        var (db, sql) = await OpenAsync(ct);
        await using (db)
        await using (var tx = await db.Database.BeginTransactionAsync(ct))
        {
            var marker = Guid.NewGuid().ToString("N");
            // Producto inactivo a propósito: VariantesDe no filtra Product.IsActive.
            var product = Product(marker, active: false, listPrice: 8m);
            var second = Item(marker + "2", order: 2);
            var first = Item(marker + "1", price: 0m, order: 1);
            var inactive = Item(marker + "x", active: false, order: 0);
            product.Items.Add(second); product.Items.Add(first); product.Items.Add(inactive);
            db.Products.Add(product);
            await db.SaveChangesAsync(ct);
            sql.Clear();

            var variants = await new CatalogService(db).VariantesDeAsync(product.Id, ct);

            Assert.Equal([first.Id, second.Id], variants.Select(x => x.ItemId));
            Assert.Equal(0m, variants[0].Price);
            Assert.Equal(8m, variants[1].Price);
            Assert.DoesNotContain(variants, x => x.ItemId == inactive.Id);
            sql.AssertInnerJoin();
            await tx.RollbackAsync(ct);
        }
    }

    private sealed class SqlCapture : DbCommandInterceptor
    {
        private readonly List<string> commands = [];
        internal string All => string.Join("\n", commands);
        internal void Clear() => commands.Clear();
        internal void AssertInnerJoin()
        {
            Assert.Contains("INNER JOIN catalog.products", All, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("LEFT JOIN catalog.products", All, StringComparison.OrdinalIgnoreCase);
        }
        public override InterceptionResult<DbDataReader> ReaderExecuting(DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result)
        {
            commands.Add(command.CommandText);
            return result;
        }
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            commands.Add(command.CommandText);
            return ValueTask.FromResult(result);
        }
    }
}
