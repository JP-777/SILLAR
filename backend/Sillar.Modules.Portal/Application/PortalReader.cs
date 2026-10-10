using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Sillar.Modules.Crm.Contracts;
using Sillar.Modules.Sales.Contracts;
using Sillar.Modules.Tracking.Contracts;

namespace Sillar.Modules.Portal.Application;

internal sealed record PortalProfile(string FullName, string Email, string? Phone);
internal sealed record PortalSection<T>(string State, IReadOnlyList<T> Items);
internal sealed record PortalOverview(
    PortalProfile Profile,
    PortalSection<CustomerOrderSummary> Orders,
    PortalSection<CustomerTrackingSummary> Work);

internal enum PortalWorkOutcome { Found, NotFound, Unavailable, Error, Unauthorized }
internal sealed record PortalWorkResult(PortalWorkOutcome Outcome, CustomerTrackingDetail? Detail = null);

/// <summary>
/// Compone exclusivamente contratos públicos de módulos activos, nunca DbContexts ajenos.
/// El cliente procede del esquema de sesión de M04, no de un parámetro HTTP.
/// </summary>
internal sealed class PortalReader(
    ICurrentCustomer current,
    ICustomerIdentityReader identities,
    IServiceProvider serviceProvider,
    ILogger<PortalReader> logger)
{
    private const int MaximumResults = 20;

    public async Task<PortalOverview?> GetOverviewAsync(int? requestedLimit, CancellationToken cancellationToken)
    {
        if (current.CustomerId is not { } customerId || customerId == Guid.Empty)
        {
            return null;
        }

        var customer = await identities.GetAsync(customerId, cancellationToken);
        if (customer is null)
        {
            // La sesión puede haberse invalidado después de autenticarse.
            return null;
        }

        var limit = Math.Clamp(requestedLimit ?? MaximumResults, 1, MaximumResults);
        var orders = await ReadOrdersAsync(customerId, limit, cancellationToken);
        var work = await ReadWorkAsync(customerId, limit, cancellationToken);

        return new PortalOverview(
            new PortalProfile(customer.FullName, customer.Email, customer.Phone),
            orders,
            work);
    }

    public async Task<PortalWorkResult> GetWorkAsync(string visibleCode, CancellationToken cancellationToken)
    {
        if (current.CustomerId is not { } customerId || customerId == Guid.Empty)
        {
            return new PortalWorkResult(PortalWorkOutcome.Unauthorized);
        }

        if (string.IsNullOrWhiteSpace(visibleCode) || visibleCode.Length > 80)
        {
            return new PortalWorkResult(PortalWorkOutcome.NotFound);
        }

        ICustomerTrackingProgress? provider;
        try
        {
            provider = serviceProvider.GetService<ICustomerTrackingProgress>();
        }
        catch (Exception)
        {
            logger.LogWarning("M08: no se pudo resolver el proveedor de seguimiento.");
            return new PortalWorkResult(PortalWorkOutcome.Error);
        }

        if (provider is null)
        {
            return new PortalWorkResult(PortalWorkOutcome.Unavailable);
        }

        try
        {
            // M06 debe volver a comprobar propiedad mediante M05b incluso en detalle.
            var result = await provider.GetForCustomerAsync(customerId, visibleCode, cancellationToken);
            return result is null
                ? new PortalWorkResult(PortalWorkOutcome.NotFound)
                : new PortalWorkResult(PortalWorkOutcome.Found, result);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            // No registrar parámetros, códigos ni contenido de excepciones con datos personales.
            logger.LogWarning("M08: proveedor de seguimiento falló en lectura de detalle.");
            return new PortalWorkResult(PortalWorkOutcome.Error);
        }
    }

    private async Task<PortalSection<CustomerOrderSummary>> ReadOrdersAsync(
        Guid customerId, int limit, CancellationToken cancellationToken)
    {
        try
        {
            var provider = serviceProvider.GetService<ICustomerOrderHistory>();
            if (provider is null)
            {
                return new PortalSection<CustomerOrderSummary>("unavailable", []);
            }

            var rows = await provider.ObtenerPedidosDeAsync(customerId, limit, cancellationToken);
            return new PortalSection<CustomerOrderSummary>(rows.Count == 0 ? "empty" : "available", rows);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            logger.LogWarning("M08: proveedor de pedidos no disponible durante la lectura.");
            return new PortalSection<CustomerOrderSummary>("error", []);
        }
    }

    private async Task<PortalSection<CustomerTrackingSummary>> ReadWorkAsync(
        Guid customerId, int limit, CancellationToken cancellationToken)
    {
        try
        {
            var provider = serviceProvider.GetService<ICustomerTrackingProgress>();
            if (provider is null)
            {
                return new PortalSection<CustomerTrackingSummary>("unavailable", []);
            }

            var rows = await provider.ListForCustomerAsync(customerId, limit, cancellationToken);
            return new PortalSection<CustomerTrackingSummary>(rows.Count == 0 ? "empty" : "available", rows);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            logger.LogWarning("M08: proveedor de seguimiento no disponible durante la lectura.");
            return new PortalSection<CustomerTrackingSummary>("error", []);
        }
    }
}
