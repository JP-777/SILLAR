using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Sillar.Core.Authentication;
using Sillar.Core.Contracts;

namespace Sillar.Core.Tests;

/// <summary>
/// Garantiza que los claims de la identidad administrativa no puedan
/// confundirse con los de otra población autenticada.
/// </summary>
public class AdminClaimIsolationTests
{
    [Fact]
    public void Claims_administrativos_tienen_namespace_propio()
    {
        Assert.Equal("sillar:admin:session_id", AdminSessionClaims.SessionId);
        Assert.Equal("sillar:admin:csrf_hash", CsrfEndpointFilter.ClaimType);
    }

    [Fact]
    public void Claim_csrf_generico_anterior_no_es_claim_administrativo()
    {
        var principal = Principal(
            new Claim("sillar:csrf_hash", "hash-de-prueba"));

        Assert.Null(principal.FindFirst(CsrfEndpointFilter.ClaimType));
    }

    [Fact]
    public void Claim_csrf_administrativo_si_se_resuelve()
    {
        const string hash = "hash-de-prueba";

        var principal = Principal(
            new Claim(CsrfEndpointFilter.ClaimType, hash));

        Assert.Equal(
            hash,
            principal.FindFirst(CsrfEndpointFilter.ClaimType)?.Value);
    }

    [Fact]
    public void CurrentAdmin_ignora_session_id_generico_anterior()
    {
        var sessionId = Guid.CreateVersion7();

        var currentAdmin = CreateCurrentAdmin(
            new Claim("sillar:session_id", sessionId.ToString()));

        Assert.Null(currentAdmin.SessionId);
    }

    [Fact]
    public void CurrentAdmin_lee_session_id_administrativo()
    {
        var sessionId = Guid.CreateVersion7();

        var currentAdmin = CreateCurrentAdmin(
            new Claim(AdminSessionClaims.SessionId, sessionId.ToString()));

        Assert.Equal(sessionId, currentAdmin.SessionId);
    }

    private static ClaimsPrincipal Principal(params Claim[] claims)
        => new(new ClaimsIdentity(claims, "prueba"));

    private static CurrentAdmin CreateCurrentAdmin(params Claim[] claims)
    {
        var context = new DefaultHttpContext
        {
            User = Principal(claims)
        };

        var accessor = new HttpContextAccessor
        {
            HttpContext = context
        };

        return new CurrentAdmin(accessor);
    }
}
