namespace Sillar.Modules.Crm.Contracts;

/// <summary>Contrato de autorización para rutas exclusivas de clientela.</summary>
public static class CustomerAuthorization
{
    /// <summary>
    /// Política que exige exclusivamente el esquema de sesión de cliente.
    /// </summary>
    public const string PolicyName = "crm:customer";
}
