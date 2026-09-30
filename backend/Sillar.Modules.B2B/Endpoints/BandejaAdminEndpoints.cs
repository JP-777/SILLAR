using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Sillar.Core.Contracts;
using Sillar.Modules.B2B.Bandeja;

namespace Sillar.Modules.B2B.Endpoints;

/// <summary>
/// Las rutas de administración de M07 (SPEC §6). Mínimo <c>editor</c>; las bajas,
/// <c>admin</c>. CSRF en todas; toda escritura deja auditoría <c>b2b</c> que
/// nombra la fila. La creación y el ciclo de las cotizaciones llegan cuando se
/// decida la letra de serie (pregunta 3 de <c>ESCALADAS-M07.md</c>).
/// </summary>
public static class BandejaAdminEndpoints
{
    private const string Tag = "Solicitudes B2B — Panel";
    private static readonly string[] EstadosDeCotizacion = ["borrador", "enviada", "aprobada", "pagada", "anulada"];

    /// <summary>Monta las rutas del panel.</summary>
    public static IEndpointRouteBuilder MapBandejaAdminEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var admin = endpoints.MapGroup("/api/admin/b2b")
            .WithTags(Tag)
            .RequireAuthorization(AdminRole.Editor)
            .AddEndpointFilter<CsrfEndpointFilter>();

        admin.MapGet("/special-orders", ListarPersonalizaciones)
            .WithName("B2bAdminListSpecialOrders").WithSummary("Bandeja de personalizaciones, con filtro por estado.")
            .Produces<IReadOnlyList<PersonalizacionEnBandeja>>().ProducesValidationProblem();
        admin.MapGet("/special-orders/{id:int}", ObtenerPersonalizacion)
            .WithName("B2bAdminGetSpecialOrder").WithSummary("Detalle de una personalización, con notas internas.")
            .Produces<PersonalizacionDetalle>().Produces(StatusCodes.Status404NotFound);
        admin.MapPut("/special-orders/{id:int}/status", EstadoPersonalizacion)
            .WithName("B2bAdminSetSpecialOrderStatus").WithSummary("Cambia el estado de una personalización.")
            .Produces<PersonalizacionDetalle>().ProducesValidationProblem().Produces(StatusCodes.Status404NotFound).ProducesProblem(StatusCodes.Status409Conflict);
        admin.MapPut("/special-orders/{id:int}/notes", NotasPersonalizacion)
            .WithName("B2bAdminSetSpecialOrderNotes").WithSummary("Reescribe las notas internas de una personalización.")
            .Produces<PersonalizacionDetalle>().Produces(StatusCodes.Status404NotFound);
        admin.MapPut("/special-orders/{id:int}/relink", Reenlazar)
            .WithName("B2bAdminRelinkSpecialOrder").WithSummary("Ata la personalización a un producto activo y refresca su instantánea.")
            .Produces<PersonalizacionDetalle>().Produces(StatusCodes.Status404NotFound).ProducesProblem(StatusCodes.Status409Conflict);
        admin.MapDelete("/special-orders/{id:int}", BajaPersonalizacion)
            .RequireAuthorization(AdminRole.Admin)
            .WithName("B2bAdminDeleteSpecialOrder").WithSummary("Da de baja una personalización, sin borrarla.")
            .Produces<PersonalizacionDetalle>().Produces(StatusCodes.Status404NotFound);

        admin.MapGet("/institution-requests", ListarVolumen)
            .WithName("B2bAdminListInstitutionRequests").WithSummary("Bandeja de solicitudes de volumen, con filtro por estado.")
            .Produces<IReadOnlyList<VolumenEnBandeja>>().ProducesValidationProblem();
        admin.MapGet("/institution-requests/{id:int}", ObtenerVolumen)
            .WithName("B2bAdminGetInstitutionRequest").WithSummary("Detalle de una solicitud de volumen, con notas internas.")
            .Produces<VolumenDetalle>().Produces(StatusCodes.Status404NotFound);
        admin.MapPut("/institution-requests/{id:int}/status", EstadoVolumen)
            .WithName("B2bAdminSetInstitutionRequestStatus").WithSummary("Cambia el estado de una solicitud de volumen.")
            .Produces<VolumenDetalle>().ProducesValidationProblem().Produces(StatusCodes.Status404NotFound).ProducesProblem(StatusCodes.Status409Conflict);
        admin.MapPut("/institution-requests/{id:int}/notes", NotasVolumen)
            .WithName("B2bAdminSetInstitutionRequestNotes").WithSummary("Reescribe las notas internas de una solicitud de volumen.")
            .Produces<VolumenDetalle>().Produces(StatusCodes.Status404NotFound);
        admin.MapDelete("/institution-requests/{id:int}", BajaVolumen)
            .RequireAuthorization(AdminRole.Admin)
            .WithName("B2bAdminDeleteInstitutionRequest").WithSummary("Da de baja una solicitud de volumen, sin borrarla.")
            .Produces<VolumenDetalle>().Produces(StatusCodes.Status404NotFound);

        admin.MapGet("/quotes", ListarCotizaciones)
            .WithName("B2bAdminListQuotes").WithSummary("Bandeja de cotizaciones, con filtro por estado.")
            .Produces<IReadOnlyList<CotizacionEnBandeja>>().ProducesValidationProblem();
        admin.MapGet("/quotes/{id:int}", ObtenerCotizacion)
            .WithName("B2bAdminGetQuote").WithSummary("Detalle de una cotización con sus líneas y su precio de catálogo.")
            .Produces<CotizacionDetalle>().Produces(StatusCodes.Status404NotFound);

        return endpoints;
    }

    /// <summary>Lista las personalizaciones; con <c>status</c>, solo las de ese estado.</summary>
    private static Task<IResult> ListarPersonalizaciones(string? status, BandejaService s, CancellationToken ct)
        => ListarConFiltro(status, st => s.ListarPersonalizacionesAsync(st, ct));

    /// <summary>Devuelve una personalización con sus notas internas.</summary>
    private static async Task<IResult> ObtenerPersonalizacion(int id, BandejaService s, CancellationToken ct)
        => await s.ObtenerPersonalizacionAsync(id, ct) is { } v ? Results.Ok(v) : Results.NotFound();

    /// <summary>Cambia el estado de una personalización según la regla 5, y lo audita.</summary>
    private static async Task<IResult> EstadoPersonalizacion(int id, CambiarEstadoRequest r, BandejaService s, IAuditWriter a, ICurrentAdmin u, CancellationToken ct)
        => await Escritura(await s.CambiarEstadoPersonalizacionAsync(id, r.Status, ct), a, u, AuditAction.Update, "special_order_lead", id,
            n => $"La {n} pasó a {TransicionesDeSolicitud.Nombre(r.Status!)}.", ct);

    /// <summary>Reescribe las notas internas de una personalización, y lo audita.</summary>
    private static async Task<IResult> NotasPersonalizacion(int id, NotasRequest r, BandejaService s, IAuditWriter a, ICurrentAdmin u, CancellationToken ct)
        => await Escritura(await s.NotasPersonalizacionAsync(id, r.StaffNotes, ct), a, u, AuditAction.Update, "special_order_lead", id,
            n => $"Notas internas de la {n} actualizadas.", ct);

    /// <summary>Reenlaza una personalización a un producto activo, y lo audita.</summary>
    private static async Task<IResult> Reenlazar(int id, ReenlazarRequest r, BandejaService s, IAuditWriter a, ICurrentAdmin u, CancellationToken ct)
        => await Escritura(await s.ReenlazarAsync(id, r.ProductId, ct), a, u, AuditAction.Update, "special_order_lead", id,
            n => $"La {n} quedó reenlazada a su producto.", ct);

    /// <summary>Baja lógica de una personalización (solo <c>admin</c>), y lo audita.</summary>
    private static async Task<IResult> BajaPersonalizacion(int id, BandejaService s, IAuditWriter a, ICurrentAdmin u, CancellationToken ct)
        => await Escritura(await s.BajaPersonalizacionAsync(id, ct), a, u, AuditAction.Delete, "special_order_lead", id,
            n => $"Baja lógica de la {n}.", ct);

    /// <summary>Lista las solicitudes de volumen; con <c>status</c>, solo las de ese estado.</summary>
    private static Task<IResult> ListarVolumen(string? status, BandejaService s, CancellationToken ct)
        => ListarConFiltro(status, st => s.ListarVolumenAsync(st, ct));

    /// <summary>Devuelve una solicitud de volumen con sus notas internas.</summary>
    private static async Task<IResult> ObtenerVolumen(int id, BandejaService s, CancellationToken ct)
        => await s.ObtenerVolumenAsync(id, ct) is { } v ? Results.Ok(v) : Results.NotFound();

    /// <summary>Cambia el estado de una solicitud de volumen según la regla 5, y lo audita.</summary>
    private static async Task<IResult> EstadoVolumen(int id, CambiarEstadoRequest r, BandejaService s, IAuditWriter a, ICurrentAdmin u, CancellationToken ct)
        => await Escritura(await s.CambiarEstadoVolumenAsync(id, r.Status, ct), a, u, AuditAction.Update, "institution_request", id,
            n => $"La {n} pasó a {TransicionesDeSolicitud.Nombre(r.Status!)}.", ct);

    /// <summary>Reescribe las notas internas de una solicitud de volumen, y lo audita.</summary>
    private static async Task<IResult> NotasVolumen(int id, NotasRequest r, BandejaService s, IAuditWriter a, ICurrentAdmin u, CancellationToken ct)
        => await Escritura(await s.NotasVolumenAsync(id, r.StaffNotes, ct), a, u, AuditAction.Update, "institution_request", id,
            n => $"Notas internas de la {n} actualizadas.", ct);

    /// <summary>Baja lógica de una solicitud de volumen (solo <c>admin</c>), y lo audita.</summary>
    private static async Task<IResult> BajaVolumen(int id, BandejaService s, IAuditWriter a, ICurrentAdmin u, CancellationToken ct)
        => await Escritura(await s.BajaVolumenAsync(id, ct), a, u, AuditAction.Delete, "institution_request", id,
            n => $"Baja lógica de la {n}.", ct);

    /// <summary>Lista las cotizaciones; con <c>status</c>, solo las de ese estado.</summary>
    private static async Task<IResult> ListarCotizaciones(string? status, BandejaService s, CancellationToken ct)
    {
        if (status is not null && !EstadosDeCotizacion.Contains(status))
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["status"] = ["Ese estado no existe. Usa: borrador, enviada, aprobada, pagada o anulada."] });
        return Results.Ok(await s.ListarCotizacionesAsync(status, ct));
    }

    /// <summary>Devuelve una cotización con sus líneas y su precio de catálogo.</summary>
    private static async Task<IResult> ObtenerCotizacion(int id, BandejaService s, CancellationToken ct)
        => await s.ObtenerCotizacionAsync(id, ct) is { } v ? Results.Ok(v) : Results.NotFound();

    private static async Task<IResult> ListarConFiltro<T>(string? estado, Func<string?, Task<IReadOnlyList<T>>> listar)
    {
        if (estado is not null && !TransicionesDeSolicitud.EsEstado(estado))
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["status"] = ["Ese estado no existe. Usa: recibida, en_revision, cotizada, cerrada o rechazada."] });
        return Results.Ok(await listar(estado));
    }

    private static async Task<IResult> Escritura<T>(OperacionDeBandeja<T> op, IAuditWriter audit, ICurrentAdmin usuario,
        string accion, string entidad, int id, Func<string, string> resumen, CancellationToken ct)
    {
        switch (op.Resultado)
        {
            case ResultadoBandeja.Hecho:
                await audit.WriteAsync(new AuditEntry(accion)
                {
                    AdminUserId = usuario.AdminUserId,
                    AdminUserEmail = usuario.Email,
                    ModuleCode = B2BModule.ModuleCode,
                    EntityType = entidad,
                    EntityId = id.ToString(),
                    Summary = resumen(op.Nombre!),
                }, ct);
                return Results.Ok(op.Valor);
            case ResultadoBandeja.NoEncontrada:
                return Results.NotFound();
            case ResultadoBandeja.Invalida:
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["solicitud"] = [op.Motivo!] });
            default:
                return Results.Problem(title: op.Motivo, statusCode: StatusCodes.Status409Conflict);
        }
    }
}
