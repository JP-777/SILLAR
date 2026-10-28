namespace Sillar.Modules.Payments.Domain;

/// <summary>
/// Fotografía inmutable de la persona que registró un pago manualmente.
/// </summary>
/// <remarks>
/// Los tres datos forman una unidad: nombre real congelado, identificador local
/// positivo y nodo de pertenencia de la cuenta. El nodo no se deriva del lugar
/// donde ocurrió la operación.
/// </remarks>
public sealed record StaffPaymentAttribution
{
    private StaffPaymentAttribution(string name, int localId, string homeNode)
    {
        Name = name;
        LocalId = localId;
        HomeNode = homeNode;
    }

    public string Name { get; }

    public int LocalId { get; }

    public string HomeNode { get; }

    /// <summary>Crea la fotografía completa de una persona autenticada.</summary>
    /// <exception cref="ArgumentException">
    /// Cuando el nombre o el nodo están vacíos, o el identificador no es positivo.
    /// </exception>
    public static StaffPaymentAttribution Create(
        string? name,
        int localId,
        string? homeNode)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException(
                "El origen staff exige el nombre real congelado de la persona.",
                nameof(name));
        }

        if (localId < 1)
        {
            throw new ArgumentException(
                "El origen staff exige un identificador local positivo.",
                nameof(localId));
        }

        if (string.IsNullOrWhiteSpace(homeNode))
        {
            throw new ArgumentException(
                "El origen staff exige el HomeNode de la cuenta.",
                nameof(homeNode));
        }

        return new StaffPaymentAttribution(
            name.Trim(),
            localId,
            homeNode.Trim());
    }
}
