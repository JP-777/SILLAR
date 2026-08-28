namespace Sillar.Modules.Crm.Authentication;

/// <summary>Vigencia propia de las sesiones de tienda.</summary>
internal static class CustomerSessionPolicy
{
    /// <summary>
    /// Tope de base de datos. La cookie sigue siendo de sesión del navegador
    /// (sin Max-Age), por lo que normalmente desaparece antes.
    /// </summary>
    public static readonly TimeSpan Lifetime = TimeSpan.FromDays(7);
}
