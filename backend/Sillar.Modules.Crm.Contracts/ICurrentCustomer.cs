namespace Sillar.Modules.Crm.Contracts;

/// <summary>
/// Cliente autenticado en la tienda durante la petición actual.
/// </summary>
/// <remarks>
/// Es deliberadamente distinto de ICurrentAdmin: personal y clientela son
/// poblaciones independientes y una credencial nunca sirve en el otro lado.
/// </remarks>
public interface ICurrentCustomer
{
    /// <summary>Identificador de la ficha de cliente, o null si no hay sesión.</summary>
    Guid? CustomerId { get; }

    /// <summary>Correo de la ficha, o null si no hay sesión.</summary>
    string? Email { get; }

    /// <summary>Indica si la cuenta ya verificó su correo.</summary>
    bool EmailVerified { get; }
}
