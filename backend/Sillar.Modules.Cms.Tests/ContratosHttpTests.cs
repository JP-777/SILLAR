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
}
