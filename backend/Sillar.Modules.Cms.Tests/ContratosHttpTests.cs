using Sillar.Modules.Cms.Dtos;

namespace Sillar.Modules.Cms.Tests;

public sealed class ContratosHttpTests
{
    [Fact]
    public void Ninguna_respuesta_publica_expone_identificadores_de_medios()
    {
        Type[] publicResponses =
        [
            typeof(BannerResponse),
            typeof(PromotionResponse),
            typeof(FeaturedProductResponse),
            typeof(FeaturedProjectResponse),
            typeof(SocialLinkResponse)
        ];

        var exposed = publicResponses
            .SelectMany(type => type.GetProperties().Select(property => $"{type.Name}.{property.Name}"))
            .Where(name => name.Contains("ImageId", StringComparison.Ordinal)
                           || name.Contains("MediaAssetId", StringComparison.Ordinal))
            .ToArray();

        Assert.Empty(exposed);
    }

    [Fact]
    public void Editar_no_permite_desactivar_ni_reordenar_por_un_campo_suelto()
    {
        Type[] updateRequests =
        [
            typeof(UpdateBannerRequest),
            typeof(UpdatePromotionRequest),
            typeof(UpdateFeaturedProductRequest),
            typeof(UpdateFeaturedProjectRequest),
            typeof(UpdateSocialLinkRequest)
        ];

        foreach (var request in updateRequests)
        {
            Assert.Null(request.GetProperty("IsActive"));
            Assert.Null(request.GetProperty("DisplayOrder"));
        }
    }

    [Fact]
    public void Administracion_de_trabajos_distingue_los_incompletos() =>
        Assert.NotNull(typeof(FeaturedProjectAdminResponse).GetProperty("IsComplete"));

    [Fact]
    public void Destacados_exponen_el_snapshot_sin_precio_formateado()
    {
        Type[] responses = [typeof(FeaturedProductResponse), typeof(FeaturedProductAdminResponse)];

        foreach (var response in responses)
        {
            Assert.Equal(typeof(decimal?), response.GetProperty("ProductPrice")?.PropertyType);
            Assert.Equal(typeof(bool), response.GetProperty("ProductPriceVaries")?.PropertyType);
            Assert.Equal(typeof(string), response.GetProperty("ProductCategory")?.PropertyType);
            Assert.Equal(typeof(bool), response.GetProperty("ProductIsPublic")?.PropertyType);
            Assert.Equal(typeof(bool), response.GetProperty("ProductIsActive")?.PropertyType);
            Assert.DoesNotContain(
                response.GetProperties(),
                property => property.PropertyType == typeof(string)
                            && property.Name.Contains("Price", StringComparison.Ordinal));
        }
    }

    [Fact]
    public void Administracion_distingue_el_estado_del_producto_del_estado_del_destacado()
    {
        var response = typeof(FeaturedProductAdminResponse);

        Assert.Equal(typeof(bool), response.GetProperty("ProductIsActive")?.PropertyType);
        Assert.Equal(typeof(bool), response.GetProperty("IsActive")?.PropertyType);
    }
}
