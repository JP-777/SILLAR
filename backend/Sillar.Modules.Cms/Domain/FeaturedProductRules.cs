using System.Linq.Expressions;

namespace Sillar.Modules.Cms.Domain;

/// <summary>Estados propios de un producto destacado.</summary>
internal static class FeaturedProductRules
{
    /// <summary>
    /// Un snapshot solo puede publicarse si conserva el enlace vivo y M01
    /// mantiene el producto de alta y público. La vigencia se aplica aparte
    /// con PublicationWindow.
    /// </summary>
    internal static Expression<Func<FeaturedProduct, bool>> HasPublicProduct()
        => featured => featured.ProductId != null
            && featured.ProductIsPublic
            && featured.ProductIsActive;

    internal static bool IsPendingRelink(FeaturedProduct featured)
        => featured.ProductId is null && !string.IsNullOrWhiteSpace(featured.ProductName);

    /// <summary>
    /// No usa GetValueOrDefault: el precio nulo y el precio cero son estados
    /// distintos del snapshot y ambos son válidos.
    /// </summary>
    internal static string? ValidateSnapshotValues(decimal? productPrice, string? productCategory)
    {
        if (productPrice is < 0)
        {
            return "El precio del producto no puede ser negativo.";
        }

        return productCategory is not null && string.IsNullOrWhiteSpace(productCategory)
            ? "La categoría del producto no puede estar vacía."
            : null;
    }
}
