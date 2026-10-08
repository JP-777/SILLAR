using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Sillar.Core.Contracts;
using Sillar.Modules.ServiceOrders.Contracts;
using Sillar.Modules.Tracking.Application;
using Sillar.Modules.Tracking.Dtos;
using Sillar.Shared.Paging;

namespace Sillar.Modules.Tracking.Endpoints;

/// <summary>API administrativa de M06 Seguimiento.</summary>
internal static class TrackingEndpoints
{
    private const string Tag = "Seguimiento";

    /// <summary>Registra exclusivamente las siete rutas administrativas ratificadas de M06.</summary>
    internal static IEndpointRouteBuilder MapTrackingEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/admin/tracking")
            .WithTags(Tag)
            .RequireAuthorization(AdminRole.Editor)
            .AddEndpointFilter<CsrfEndpointFilter>();

        group.MapGet("/board", GetBoard)
            .WithName("GetTrackingBoard")
            .WithSummary("Obtiene el tablero abierto o la vista paginada de terminadas, agrupados por los estados publicados por M05b.")
            .Produces<TrackingBoardResponse>(StatusCodes.Status200OK);

        group.MapGet("/orders/{serviceOrderId:guid}", GetDetail)
            .WithName("GetTrackingOrderDetail")
            .WithSummary("Obtiene el detalle de la orden y separa los datos de M05b de los editables de M06.")
            .Produces<TrackingOrderDetailResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);

        group.MapPut("/orders/{serviceOrderId:guid}/priority", SetPriority)
            .WithName("SetTrackingPriority")
            .WithSummary("Fija o elimina la prioridad manual y el anclaje de una tarjeta.")
            .Produces<TrackingMutationResponse>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .Produces(StatusCodes.Status404NotFound);

        group.MapPut("/orders/{serviceOrderId:guid}/due", SetDue)
            .WithName("SetTrackingDue")
            .WithSummary("Fija o limpia el plazo interno de una orden.")
            .Produces<TrackingMutationResponse>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .Produces(StatusCodes.Status404NotFound);

        group.MapPost("/orders/{serviceOrderId:guid}/notes", AddNote)
            .WithName("AddTrackingNote")
            .WithSummary("Añade una nota interna de seguimiento.")
            .Produces<TrackingNoteResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .Produces(StatusCodes.Status404NotFound);

        group.MapDelete("/notes/{noteId:guid}", DeactivateNote)
            .WithName("DeactivateTrackingNote")
            .WithSummary("Da de baja lógicamente una nota interna de seguimiento.")
            .RequireAuthorization(AdminRole.Admin)
            .Produces<TrackingNoteResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);

        group.MapPut("/orders/{serviceOrderId:guid}/status", TransitionStatus)
            .WithName("TransitionTrackingOrderStatus")
            .WithSummary("Delega en M05b una transición condicionada al estado que vio el cliente.")
            .Produces<TransitionTrackingStatusResponse>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .Produces(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        return endpoints;
    }

    private static async Task<IResult> GetBoard(
        string? scope,
        int? page,
        int? pageSize,
        TrackingApplicationService service,
        CancellationToken cancellationToken)
    {
        var parsedScope = scope?.Trim().ToLowerInvariant() switch
        {
            null or "" or "open" => ServiceOrderScope.Open,
            "closed" => ServiceOrderScope.Closed,
            _ => (ServiceOrderScope?)null
        };

        if (parsedScope is null)
        {
            return Results.ValidationProblem(
                new Dictionary<string, string[]>
                {
                    ["scope"] = ["scope solo admite 'open' o 'closed'."]
                },
                title: "La vista del tablero no es válida.");
        }

        return Results.Ok(await service.GetBoardAsync(
            parsedScope.Value,
            PageRequest.Of(page, pageSize),
            cancellationToken));
    }

    private static async Task<IResult> GetDetail(
        Guid serviceOrderId,
        TrackingApplicationService service,
        CancellationToken cancellationToken)
        => await service.GetDetailAsync(serviceOrderId, cancellationToken) is { } detail
            ? Results.Ok(detail)
            : Results.Problem(
                title: "Esa orden ya no está disponible. Vuelve al tablero para ver las actuales.",
                statusCode: StatusCodes.Status404NotFound);

    private static async Task<IResult> SetPriority(
        Guid serviceOrderId,
        SetTrackingPriorityRequest request,
        TrackingApplicationService service,
        CancellationToken cancellationToken)
        => OwnResult(await service.SetPriorityAsync(serviceOrderId, request, cancellationToken), Results.Ok);

    private static async Task<IResult> SetDue(
        Guid serviceOrderId,
        SetTrackingDueRequest request,
        TrackingApplicationService service,
        CancellationToken cancellationToken)
        => OwnResult(await service.SetDueAsync(serviceOrderId, request, cancellationToken), Results.Ok);

    private static async Task<IResult> AddNote(
        Guid serviceOrderId,
        AddTrackingNoteRequest request,
        TrackingApplicationService service,
        CancellationToken cancellationToken)
        => OwnResult(
            await service.AddNoteAsync(serviceOrderId, request, cancellationToken),
            note => Results.Created($"/api/admin/tracking/orders/{serviceOrderId}", note));

    private static async Task<IResult> DeactivateNote(
        Guid noteId,
        TrackingApplicationService service,
        CancellationToken cancellationToken)
        => OwnResult(await service.DeactivateNoteAsync(noteId, cancellationToken), Results.Ok);

    private static async Task<IResult> TransitionStatus(
        Guid serviceOrderId,
        TransitionTrackingStatusRequest request,
        TrackingApplicationService service,
        CancellationToken cancellationToken)
        => TransitionResult(await service.TransitionAsync(
            serviceOrderId,
            request,
            cancellationToken));

    internal static IResult TransitionResult(
        ServiceOrderOperation<TransitionTrackingStatusResponse> result)
        => result.Outcome switch
        {
            ServiceOrderOutcome.Ok => Results.Ok(result.Value),
            ServiceOrderOutcome.NotFound => Results.Problem(
                title: "Esa orden ya no está disponible. Vuelve al tablero para ver las actuales.",
                statusCode: StatusCodes.Status404NotFound),
            ServiceOrderOutcome.Conflict => Results.Problem(
                title: result.Error,
                statusCode: StatusCodes.Status409Conflict),
            _ => Results.ValidationProblem(
                new Dictionary<string, string[]> { ["status"] = [result.Error!] },
                title: result.Error)
        };

    private static IResult OwnResult<T>(TrackingOperation<T> operation, Func<T, IResult> success)
        => operation.Outcome switch
        {
            TrackingOutcome.Ok => success(operation.Value!),
            TrackingOutcome.NotFound => Results.Problem(
                title: operation.Error,
                statusCode: StatusCodes.Status404NotFound),
            _ => Results.ValidationProblem(
                new Dictionary<string, string[]> { [operation.Field!] = [operation.Error!] },
                title: "Los datos de seguimiento no son válidos.")
        };
}
