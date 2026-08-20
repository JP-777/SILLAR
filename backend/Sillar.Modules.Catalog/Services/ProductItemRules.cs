namespace Sillar.Modules.Catalog.Services;

/// <summary>Reglas de desactivación de variantes que no necesitan la base.</summary>
public static class ProductItemRules
{
    /// <summary>
    /// El mensaje que explica por qué no se puede desactivar, o <c>null</c> si
    /// se puede (SPEC regla 8).
    /// </summary>
    /// <remarks>
    /// El servicio cuenta las variantes activas del producto y comprueba si el
    /// producto está activo; esta función solo decide el mensaje a partir de
    /// ese veredicto, para poder probarlo sin la base. Nunca un «no se pudo»
    /// genérico: propone la acción que sí resuelve el caso.
    /// </remarks>
    /// <param name="isLastActiveVariantOfActiveProduct">
    /// Verdadero si desactivar esta variante dejaría al producto activo sin
    /// ninguna variante activa.
    /// </param>
    public static string? DeactivationBlockedReason(bool isLastActiveVariantOfActiveProduct)
        => isLastActiveVariantOfActiveProduct
            ? "No se puede desactivar la última variante activa de un producto activo. Desactiva el producto en su lugar."
            : null;
}
