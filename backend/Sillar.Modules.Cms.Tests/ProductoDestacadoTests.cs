using System.Text.Json;
using Sillar.Modules.Catalog.Contracts;
using Sillar.Modules.Cms.Domain;
using Sillar.Modules.Cms.Dtos;
using Sillar.Modules.Cms.Services;

namespace Sillar.Modules.Cms.Tests;

public sealed class ProductoDestacadoTests
{
    [Fact]
    public void Snapshot_sin_producto_esta_pendiente_de_volver_a_enlazar()
    {
        var featured = new FeaturedProduct
        {
            ProductId = null,
            ProductName = "Cuaderno cuadriculado",
            ProductSlug = "cuaderno-cuadriculado"
        };

        Assert.True(FeaturedProductRules.IsPendingRelink(featured));
    }

    [Fact]
    public void Snapshot_con_producto_no_esta_pendiente_de_volver_a_enlazar()
    {
        var featured = new FeaturedProduct
        {
            ProductId = Guid.NewGuid(),
            ProductName = "Cuaderno cuadriculado"
        };

        Assert.False(FeaturedProductRules.IsPendingRelink(featured));
    }

    [Fact]
    public void Producto_no_publico_no_puede_publicarse()
    {
        var featured = new FeaturedProduct
        {
            ProductId = Guid.NewGuid(),
            ProductName = "Producto preparado",
            ProductIsPublic = false
        };

        Assert.False(FeaturedProductRules.HasPublicProduct().Compile()(featured));
    }

    [Fact]
    public void Producto_publico_y_enlazado_puede_publicarse()
    {
        var featured = new FeaturedProduct
        {
            ProductId = Guid.NewGuid(),
            ProductName = "Producto publicado",
            ProductIsPublic = true
        };

        Assert.True(FeaturedProductRules.HasPublicProduct().Compile()(featured));
    }

    [Fact]
    public void Producto_dado_de_baja_no_puede_publicarse_aunque_siga_publico()
    {
        var featured = new FeaturedProduct
        {
            ProductId = Guid.NewGuid(),
            ProductName = "Producto dado de baja",
            ProductIsPublic = true,
            ProductIsActive = false
        };

        Assert.False(FeaturedProductRules.HasPublicProduct().Compile()(featured));
    }

    [Fact]
    public void Releer_el_snapshot_sobrescribe_el_estado_del_producto_y_es_idempotente()
    {
        var productId = Guid.NewGuid();
        var featured = new FeaturedProduct
        {
            ProductId = productId,
            ProductName = "Nombre anterior",
            ProductIsPublic = true,
            ProductIsActive = true
        };
        var product = new ProductPickerItem(
            productId,
            "Nombre nuevo",
            "nombre-nuevo",
            null,
            null,
            0m,
            false,
            false,
            false);

        for (var attempt = 0; attempt < 4; attempt++)
        {
            FeaturedProductService.ApplySnapshot(featured, product);
        }

        Assert.Equal("Nombre nuevo", featured.ProductName);
        Assert.Equal(0m, featured.ProductPrice);
        Assert.False(featured.ProductIsPublic);
        Assert.False(featured.ProductIsActive);
    }

    [Fact]
    public void Precio_a_consultar_gratis_y_positivo_son_validos()
    {
        Assert.Null(FeaturedProductRules.ValidateSnapshotValues(null, null));
        Assert.Null(FeaturedProductRules.ValidateSnapshotValues(0m, null));
        Assert.Null(FeaturedProductRules.ValidateSnapshotValues(8m, null));
    }

    [Fact]
    public void Precio_negativo_se_rechaza() =>
        Assert.Equal(
            "El precio del producto no puede ser negativo.",
            FeaturedProductRules.ValidateSnapshotValues(-0.01m, null));

    [Fact]
    public void Respuesta_conserva_precio_nulo_cero_y_positivo_como_estados_distintos()
    {
        FeaturedProductResponse[] responses =
        [
            new(1, "A consultar", "a-consultar", null, null, false, null, true, true),
            new(2, "Gratis", "gratis", null, 0m, false, null, true, true),
            new(3, "Desde ocho", "desde-ocho", null, 8m, true, "Cuadernos", true, true)
        ];

        using var json = JsonDocument.Parse(JsonSerializer.Serialize(
            responses,
            new JsonSerializerOptions(JsonSerializerDefaults.Web)));
        var items = json.RootElement;

        Assert.Equal(JsonValueKind.Null, items[0].GetProperty("productPrice").ValueKind);
        Assert.Equal(JsonValueKind.Null, items[0].GetProperty("productCategory").ValueKind);
        Assert.Equal(0m, items[1].GetProperty("productPrice").GetDecimal());
        Assert.Equal(8m, items[2].GetProperty("productPrice").GetDecimal());
        Assert.True(items[2].GetProperty("productPriceVaries").GetBoolean());
    }
}
