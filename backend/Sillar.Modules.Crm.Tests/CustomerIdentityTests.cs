using Microsoft.AspNetCore.Http;
using Sillar.Modules.Crm.Authentication;
using Sillar.Modules.Crm.Contracts;

namespace Sillar.Modules.Crm.Tests;

public class CustomerIdentityTests
{
    [Fact]
    public void Identidad_de_cliente_tiene_namespace_cookie_y_esquema_propios()
    {
        Assert.Equal("sillar_tienda", CustomerSessionCookie.Name);
        Assert.Equal(
            "SillarCustomerSession",
            CustomerSessionAuthenticationHandler.SchemeName);

        Assert.Equal(
            "sillar:customer:session_id",
            CustomerSessionClaims.SessionId);

        Assert.Equal(
            "sillar:customer:csrf_hash",
            CustomerCsrfEndpointFilter.ClaimType);

        Assert.Equal(
            "crm:customer",
            CustomerAuthorization.PolicyName);
    }

    [Fact]
    public void Cookie_de_cliente_conserva_las_barreras_del_spec()
    {
        var options = CustomerSessionCookie.Options();

        Assert.True(options.HttpOnly);
        Assert.True(options.Secure);
        Assert.Equal(SameSiteMode.Strict, options.SameSite);
        Assert.Equal("/", options.Path);
        Assert.Null(options.MaxAge);
        Assert.Null(options.Expires);
    }

    [Fact]
    public void Cookie_csrf_de_cliente_es_legible_y_conserva_barreras()
    {
        Assert.Equal(
            "sillar_tienda_csrf",
            CustomerCsrfCookie.Name);

        var options = CustomerCsrfCookie.Options();

        Assert.False(options.HttpOnly);
        Assert.True(options.Secure);
        Assert.Equal(SameSiteMode.Strict, options.SameSite);
        Assert.Equal("/", options.Path);
        Assert.True(options.IsEssential);
        Assert.Null(options.MaxAge);
        Assert.Null(options.Expires);
    }

    [Fact]
    public void Cookie_csrf_no_reemplaza_la_cookie_de_sesion()
    {
        Assert.NotEqual(
            CustomerSessionCookie.Name,
            CustomerCsrfCookie.Name);

        Assert.True(CustomerSessionCookie.Options().HttpOnly);
        Assert.False(CustomerCsrfCookie.Options().HttpOnly);
    }

    [Fact]
    public void CurrentCustomer_ignora_claims_administrativos()
    {
        var context = new DefaultHttpContext();

        context.User = new System.Security.Claims.ClaimsPrincipal(
            new System.Security.Claims.ClaimsIdentity(
            [
                new("sillar:admin:session_id", "7"),
                new("sillar:admin:csrf_hash", "hash-admin"),
                new(System.Security.Claims.ClaimTypes.Email, "admin@sillar.test")
            ],
            "SillarAdminSession"));

        var accessor = new HttpContextAccessor
        {
            HttpContext = context
        };

        var customer = new CurrentCustomer(accessor);

        Assert.Null(customer.CustomerId);
        Assert.Null(customer.Email);
        Assert.False(customer.EmailVerified);
        Assert.Null(customer.AccountId);
        Assert.Null(customer.SessionId);
    }

    [Fact]
    public void CurrentCustomer_lee_solo_claims_de_cliente()
    {
        var customerId = Guid.CreateVersion7();

        var context = new DefaultHttpContext();

        context.User = new System.Security.Claims.ClaimsPrincipal(
            new System.Security.Claims.ClaimsIdentity(
            [
                new(CustomerSessionClaims.CustomerId, customerId.ToString()),
                new(CustomerSessionClaims.AccountId, "31"),
                new(CustomerSessionClaims.SessionId, "52"),
                new(CustomerSessionClaims.Email, "cliente@sillar.test"),
                new(CustomerSessionClaims.EmailVerified, "true")
            ],
            CustomerSessionAuthenticationHandler.SchemeName));

        var accessor = new HttpContextAccessor
        {
            HttpContext = context
        };

        var customer = new CurrentCustomer(accessor);

        Assert.Equal(customerId, customer.CustomerId);
        Assert.Equal("cliente@sillar.test", customer.Email);
        Assert.True(customer.EmailVerified);
        Assert.Equal(31, customer.AccountId);
        Assert.Equal(52, customer.SessionId);
    }
}
