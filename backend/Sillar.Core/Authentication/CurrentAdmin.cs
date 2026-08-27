using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Sillar.Core.Contracts;

namespace Sillar.Core.Authentication;

/// <summary>Lee del principal de la petición quién la está haciendo.</summary>
internal sealed class CurrentAdmin(IHttpContextAccessor accessor) : ICurrentAdmin
{
    /// <inheritdoc />
    public int? AdminUserId
        => int.TryParse(Find(ClaimTypes.NameIdentifier), out var id) ? id : null;

    /// <inheritdoc />
    public string? Email => Find(ClaimTypes.Email);

    /// <inheritdoc />
    public string? Role => Find(ClaimTypes.Role);

    /// <inheritdoc />
    public bool IsInRole(string role) => RoleHierarchy.Satisfies(Role, role);

    /// <summary>Identificador de la sesión en curso, para revocarla o conservarla.</summary>
    public Guid? SessionId
        => Guid.TryParse(Find(AdminSessionClaims.SessionId), out var id) ? id : null;

    private string? Find(string claim) => accessor.HttpContext?.User.FindFirst(claim)?.Value;
}
