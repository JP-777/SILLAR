using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Sillar.Modules.B2B.Endpoints;

namespace Sillar.Modules.B2B.Tests;

/// <summary>
/// Plan de pruebas §1 y §3 en su mitad declarativa: qué política exige cada
/// ruta, leída de los metadatos reales de los endpoints. La mitad por HTTP
/// (401/403 de verdad) es e2e y espera a que M07 se despliegue (C9).
/// </summary>
public sealed class PermisosDeRutasTests
{
    private static IReadOnlyList<(string Ruta, string Metodo, string[] Politicas)> Rutas()
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.Services.AddRouting();
        // Solo para que el enlazador reconozca estos tipos como servicios al
        // inferir parámetros: aquí se leen metadatos, ninguna ruta se ejecuta.
        foreach (var servicio in new[] { typeof(Sillar.Modules.B2B.Bandeja.BandejaService), typeof(Sillar.Modules.B2B.Solicitudes.SolicitudesService),
                     typeof(Sillar.Modules.B2B.Cotizaciones.CotizacionesService),
                     typeof(Sillar.Core.Contracts.IAuditWriter), typeof(Sillar.Core.Contracts.ICurrentAdmin), typeof(Sillar.Modules.Crm.Contracts.ICurrentCustomer) })
        {
            builder.Services.AddScoped(servicio, _ => throw new InvalidOperationException("no se ejecuta"));
        }
        var app = builder.Build();
        app.MapSolicitudesClienteEndpoints();
        app.MapBandejaAdminEndpoints();

        return [.. ((IEndpointRouteBuilder)app).DataSources.SelectMany(d => d.Endpoints).OfType<RouteEndpoint>()
            .Select(e => (
                Ruta: e.RoutePattern.RawText!,
                Metodo: e.Metadata.GetMetadata<Microsoft.AspNetCore.Routing.HttpMethodMetadata>()!.HttpMethods.Single(),
                Politicas: e.Metadata.GetOrderedMetadata<IAuthorizeData>().Select(a => a.Policy ?? "").ToArray()))];
    }

    [Fact]
    public void Las_cuatro_rutas_publicas_exigen_la_sesion_de_cliente_de_M04()
    {
        var publicas = Rutas().Where(r => r.Ruta.StartsWith("/api/b2b")).ToList();

        Assert.Equal(4, publicas.Count);
        Assert.All(publicas, r => Assert.Equal(["crm:customer"], r.Politicas));
    }

    [Fact]
    public void Todas_las_rutas_del_panel_exigen_al_menos_editor()
    {
        var panel = Rutas().Where(r => r.Ruta.StartsWith("/api/admin/b2b")).ToList();

        Assert.Equal(19, panel.Count);
        Assert.All(panel, r => Assert.Contains("editor", r.Politicas));
    }

    [Fact]
    public void Las_bajas_y_el_pago_exigen_admin_y_nada_mas_lo_exige()
    {
        var conAdmin = Rutas().Where(r => r.Politicas.Contains("admin")).Select(r => $"{r.Metodo} {r.Ruta}").Order().ToArray();

        Assert.Equal(
            ["DELETE /api/admin/b2b/institution-requests/{id:int}", "DELETE /api/admin/b2b/quotes/{id:int}",
             "DELETE /api/admin/b2b/special-orders/{id:int}", "PUT /api/admin/b2b/quotes/{id:int}/payment"],
            conAdmin);
    }
}
