using Microsoft.AspNetCore.Http;
using Sillar.Modules.Crm.Contracts;

namespace Sillar.Modules.Crm.Authentication;

/// <summary>Lee únicamente la identidad de cliente de la petición actual.</summary>
internal sealed class CurrentCustomer(IHttpContextAccessor accessor) : ICurrentCustomer
{
    public Guid? CustomerId
        => Guid.TryParse(Find(CustomerSessionClaims.CustomerId), out var id)
            ? id
            : null;

    public string? Email => Find(CustomerSessionClaims.Email);

    public bool EmailVerified
        => bool.TryParse(
            Find(CustomerSessionClaims.EmailVerified),
            out var verified)
            && verified;

    /// <summary>Cuenta local a la que pertenece la sesión.</summary>
    internal int? AccountId
        => int.TryParse(Find(CustomerSessionClaims.AccountId), out var id)
            ? id
            : null;

    /// <summary>Fila de sesión local, usada posteriormente para logout.</summary>
    internal int? SessionId
        => int.TryParse(Find(CustomerSessionClaims.SessionId), out var id)
            ? id
            : null;

    private string? Find(string claim)
        => accessor.HttpContext?.User.FindFirst(claim)?.Value;
}
