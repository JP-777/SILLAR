using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using Sillar.Core.Contracts;
using Sillar.Modules.Reports.Orders;

namespace Sillar.Modules.Reports.Endpoints;

/// <summary>Lectura administrativa de I01, I03 e I04 desde el contrato de M03.</summary>
public static class OrderIndicatorsEndpoints
{
    // El consumidor debe declarar el desplazamiento, no usar implícitamente
    // la zona del navegador, servidor ni instancia.
    private static readonly Regex ExplicitOffset = new(
        @"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}(?::\d{2}(?:\.\d{1,7})?)?(?:Z|[+-]\d{2}:\d{2})$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    /// <summary>Registra el GET protegido solo cuando M10 está activo.</summary>
    public static IEndpointRouteBuilder MapOrderIndicatorsEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var admin = endpoints.MapGroup("/api/admin/reporting")
            .WithTags("Reportes — Pedidos")
            .RequireAuthorization(AdminRole.Admin);

        admin.MapGet("/orders", ReadOrders)
            .WithName("ReportingAdminOrderIndicators")
            .WithSummary("Indicadores I01, I03 e I04 de la cohorte de pedidos entre inicio incluido y fin excluido.")
            .WithDescription("start y end son instantes ISO 8601 con Z o desplazamiento explícito. Mantiene los inactivos; no informa montos ni información personal.")
            .Produces<OrderIndicatorsView>(StatusCodes.Status200OK)
            .ProducesValidationProblem(StatusCodes.Status400BadRequest)
            // Ventas está activo pero falló: no hay cifra, ni cero ni fuente ausente.
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable)
            // Sin sesión de administración, 401; con rol editor, 403 (D-M10-05).
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden);

        return endpoints;
    }

    /// <summary>Consulta una cohorte con intervalo [start,end) y zona explícita.</summary>
    private static async Task<IResult> ReadOrders(
        HttpRequest request,
        IOrderIndicatorsReport report,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        var startText = request.Query["start"].ToString();
        var endText = request.Query["end"].ToString();
        if (!TryReadInstant(startText, out var start) || !TryReadInstant(endText, out var end))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["periodo"] = ["Indica inicio y fin ISO 8601, cada uno con zona horaria explícita (Z o ±HH:MM)."]
            });
        }

        if (end <= start)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["periodo"] = ["El fin debe ser posterior al inicio. El intervalo es [inicio, fin)."]
            });
        }

        // Un fallo real del lector no se convierte en 0 ni en fuente ausente: es
        // un 503 con su propia frase, y el detalle técnico va solo al registro.
        OrderIndicatorsResult data;
        try
        {
            data = await report.ReadAsync(start, end, cancellationToken);
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            loggerFactory.CreateLogger(typeof(OrderIndicatorsEndpoints)).LogError(
                exception, "La lectura agregada de pedidos falló para [{Start}, {End}).", start, end);
            return Results.Problem(
                statusCode: StatusCodes.Status503ServiceUnavailable,
                title: "El módulo de ventas no respondió, así que no se muestra ninguna cifra.",
                detail: "Esto no significa cero pedidos. Vuelve a consultar en unos minutos; si se repite, revisa el registro del servidor.");
        }

        var status = data.SourceStatus == OrderIndicatorsSourceStatus.SourceUnavailable
            ? "source_unavailable"
            : data.I01CreatedOrders == 0 ? "empty" : "with_data";

        return Results.Ok(new OrderIndicatorsView(
            status,
            data.StartInclusive,
            data.EndExclusive,
            data.ObservedAtUtc,
            data.I01CreatedOrders,
            data.I03CurrentStatusCounts,
            data.I04CurrentlyExpiredOrders,
            data.ActiveOrders,
            data.InactiveOrders));
    }

    private static bool TryReadInstant(string text, out DateTimeOffset instant)
    {
        instant = default;
        return ExplicitOffset.IsMatch(text)
            && DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out instant);
    }
}

/// <summary>
/// Proyección de solo lectura para pantalla: distingue fuente ausente, cohorte
/// vacía y cohorte con datos sin depender del formato JSON de un enum .NET.
/// </summary>
public sealed record OrderIndicatorsView(
    string Status,
    DateTimeOffset StartInclusive,
    DateTimeOffset EndExclusive,
    DateTimeOffset? ObservedAtUtc,
    long? I01CreatedOrders,
    IReadOnlyList<ReportOrderStatusCount>? I03CurrentStatusCounts,
    long? I04CurrentlyExpiredOrders,
    long? ActiveOrders,
    long? InactiveOrders);
