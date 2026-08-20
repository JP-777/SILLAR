namespace Sillar.Modules.Cms.Domain;

/// <summary>Estados propios de un producto destacado.</summary>
internal static class FeaturedProductRules
{
    internal static bool IsPendingRelink(FeaturedProduct featured)
        => featured.ProductId is null && !string.IsNullOrWhiteSpace(featured.ProductName);
}
