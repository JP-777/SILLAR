using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Sillar.Core.Contracts;
using Sillar.Modules.ServiceOrders.Application;
using Sillar.Modules.ServiceOrders.Contracts;
using Sillar.Modules.ServiceOrders.Services;
using Sillar.Shared.Paging;

namespace Sillar.Modules.ServiceOrders.Endpoints;

/// <summary>API administrativa de M05b Servicios — Órdenes.</summary>
public static class ServiceOrderEndpoints
{
    private const string Tag = "Servicios — Órdenes";

    /// <summary>Monta exclusivamente las rutas administrativas de M05b.</summary>
    public static IEndpointRouteBuilder MapServiceOrderEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/admin/service-orders", List)
            .WithTags(Tag)
            .RequireAuthorization(AdminRole.Editor)
            .AddEndpointFilter<CsrfEndpointFilter>()
            .WithName("ListServiceOrders")
            .WithSummary("Lista órdenes de servicio con filtros y paginación.")
            .Produces<PagedResult<ServiceOrderAdminSummary>>()
            .ProducesValidationProblem()
            .Produces(StatusCodes.Status403Forbidden);

        endpoints.MapPost("/api/admin/service-orders", Create)
            .WithTags(Tag)
            .RequireAuthorization(AdminRole.Editor)
            .AddEndpointFilter<CsrfEndpointFilter>()
            .WithName("CreateServiceOrder")
            .WithSummary("Recibe una orden con una o más líneas y congela sus snapshots.")
            .Produces<ServiceOrderAdminDetail>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .Produces(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .Produces(StatusCodes.Status403Forbidden);

        var admin = endpoints
            .MapGroup("/api/admin/service-orders")
            .WithTags(Tag)
            .RequireAuthorization(AdminRole.Editor)
            .AddEndpointFilter<CsrfEndpointFilter>();

        admin.MapGet("/{id:guid}", Get)
            .WithName("GetServiceOrder")
            .WithSummary("Obtiene el detalle histórico completo de una orden.")
            .Produces<ServiceOrderAdminDetail>()
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status403Forbidden);

        admin.MapPut("/{id:guid}", Update)
            .WithName("UpdateServiceOrder")
            .WithSummary("Edita contacto, compromiso y líneas en received o in_progress.")
            .Produces<ServiceOrderAdminDetail>()
            .ProducesValidationProblem()
            .Produces(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .Produces(StatusCodes.Status403Forbidden);

        admin.MapPost("/{id:guid}/take", Take)
            .WithName("TakeServiceOrder")
            .WithSummary("Asigna la orden a la cuenta administrativa actual.")
            .Produces<ServiceOrderAdminDetail>()
            .ProducesValidationProblem()
            .Produces(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .Produces(StatusCodes.Status403Forbidden);

        admin.MapPost("/{id:guid}/unassign", Unassign)
            .WithName("UnassignServiceOrder")
            .WithSummary("Libera una orden asignada a la propia cuenta.")
            .Produces<ServiceOrderAdminDetail>()
            .ProducesValidationProblem()
            .Produces(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .Produces(StatusCodes.Status403Forbidden);

        admin.MapPost("/{id:guid}/transition", Transition)
            .WithName("TransitionServiceOrder")
            .WithSummary("Ejecuta una transición autoritativa con expectedStatus.")
            .Produces<ServiceOrderTransitionResult>()
            .ProducesValidationProblem()
            .Produces(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .Produces(StatusCodes.Status403Forbidden);

        return endpoints;
    }

    private static async Task<IResult> List(
        string? status,
        int? assigneeAdminUserId,
        string? q,
        DateTimeOffset? receivedFrom,
        DateTimeOffset? receivedTo,
        int? page,
        int? pageSize,
        ServiceOrderApplicationService service,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(status)
            && !ServiceOrderStatuses.All.Any(
                value => value.Code == status))
        {
            return Results.ValidationProblem(
                new Dictionary<string, string[]>
                {
                    ["status"] =
                    [
                        "Ese estado no existe. Usa received, in_progress, ready, completed o cancelled."
                    ]
                },
                title: "El filtro de estado no es válido.");
        }

        if (assigneeAdminUserId is <= 0)
        {
            return Results.ValidationProblem(
                new Dictionary<string, string[]>
                {
                    ["assigneeAdminUserId"] =
                    [
                        "El identificador del responsable debe ser mayor que cero."
                    ]
                },
                title: "El filtro de responsable no es válido.");
        }

        if (receivedFrom is not null
            && receivedTo is not null
            && receivedFrom > receivedTo)
        {
            return Results.ValidationProblem(
                new Dictionary<string, string[]>
                {
                    ["receivedFrom"] =
                    [
                        "La fecha inicial no puede ser posterior a la fecha final."
                    ]
                },
                title: "El intervalo de recepción no es válido.");
        }

        var result = await service.ListAdminAsync(
            status,
            assigneeAdminUserId,
            q,
            receivedFrom,
            receivedTo,
            PageRequest.Of(page, pageSize),
            cancellationToken);

        return Results.Ok(result);
    }

    private static async Task<IResult> Get(
        Guid id,
        ServiceOrderApplicationService service,
        CancellationToken cancellationToken)
        => await service.GetAdminAsync(id, cancellationToken) is { } order
            ? Results.Ok(order)
            : Results.NotFound();

    private static async Task<IResult> Create(
        CreateServiceOrderRequest request,
        ServiceOrderApplicationService service,
        IAuditWriter audit,
        ICurrentAdmin current,
        CancellationToken cancellationToken)
    {
        var operation = await service.CreateAsync(
            request,
            cancellationToken);

        if (operation.Outcome != ServiceOrderAdminOutcome.Ok)
        {
            return Result(operation);
        }

        var order = operation.Value!;

        if (!operation.IsReplay)
        {
            await WriteAudit(
                audit,
                current,
                AuditAction.Create,
                order,
                $"Orden de servicio {order.VisibleCode} creada para {order.CustomerName}.",
                cancellationToken);
        }

        return Results.Created(
            $"/api/admin/service-orders/{order.ServiceOrderId}",
            order);
    }

    private static async Task<IResult> Update(
        Guid id,
        UpdateServiceOrderRequest request,
        ServiceOrderApplicationService service,
        IAuditWriter audit,
        ICurrentAdmin current,
        CancellationToken cancellationToken)
    {
        var operation = await service.UpdateAsync(
            id,
            request,
            cancellationToken);

        if (operation.Outcome != ServiceOrderAdminOutcome.Ok)
        {
            return Result(operation);
        }

        var order = operation.Value!;

        await WriteAudit(
            audit,
            current,
            AuditAction.Update,
            order,
            $"Orden de servicio {order.VisibleCode} actualizada.",
            cancellationToken);

        return Results.Ok(order);
    }

    private static async Task<IResult> Take(
        Guid id,
        ServiceOrderConcurrencyRequest request,
        ServiceOrderApplicationService service,
        IAuditWriter audit,
        ICurrentAdmin current,
        CancellationToken cancellationToken)
    {
        var operation = await service.TakeAsync(
            id,
            request.ExpectedUpdatedAt,
            cancellationToken);

        if (operation.Outcome != ServiceOrderAdminOutcome.Ok)
        {
            return Result(operation);
        }

        var order = operation.Value!;

        await WriteAudit(
            audit,
            current,
            AuditAction.Update,
            order,
            $"Orden de servicio {order.VisibleCode} asignada a {order.CurrentAssignee!.DisplayName}.",
            cancellationToken);

        return Results.Ok(order);
    }

    private static async Task<IResult> Unassign(
        Guid id,
        ServiceOrderConcurrencyRequest request,
        ServiceOrderApplicationService service,
        IAuditWriter audit,
        ICurrentAdmin current,
        CancellationToken cancellationToken)
    {
        var operation = await service.UnassignAsync(
            id,
            request.ExpectedUpdatedAt,
            cancellationToken);

        if (operation.Outcome != ServiceOrderAdminOutcome.Ok)
        {
            return Result(operation);
        }

        var order = operation.Value!;

        await WriteAudit(
            audit,
            current,
            AuditAction.Update,
            order,
            $"Orden de servicio {order.VisibleCode} quedó sin responsable.",
            cancellationToken);

        return Results.Ok(order);
    }

    private static async Task<IResult> Transition(
        Guid id,
        ServiceOrderTransitionRequest request,
        IServiceOrderTransitions transitions,
        ICurrentAdmin current,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.ExpectedStatus)
            || string.IsNullOrWhiteSpace(request.TargetStatus))
        {
            return Results.ValidationProblem(
                new Dictionary<string, string[]>
                {
                    ["transition"] =
                    [
                        "expectedStatus y targetStatus son obligatorios."
                    ]
                },
                title: "La transición no está completa.");
        }

        if (request.TargetStatus == ServiceOrderStatuses.Cancelled
            && !current.IsInRole(AdminRole.Admin))
        {
            return Results.Problem(
                title: "Cancelar una orden exige rol admin o superior.",
                statusCode: StatusCodes.Status403Forbidden);
        }

        var operation = await transitions.TransitionAsync(
            id,
            request.ExpectedStatus,
            request.TargetStatus,
            cancellationToken);

        if (operation.Outcome != ServiceOrderOutcome.Ok)
        {
            return operation.Outcome switch
            {
                ServiceOrderOutcome.NotFound =>
                    Results.NotFound(),

                ServiceOrderOutcome.Invalid =>
                    Results.Problem(
                        title: operation.Error,
                        statusCode: StatusCodes.Status409Conflict),

                ServiceOrderOutcome.Conflict =>
                    Results.Problem(
                        title: operation.Error,
                        statusCode: StatusCodes.Status409Conflict),

                _ =>
                    Results.Problem(
                        title: "No se pudo ejecutar la transición.",
                        statusCode: StatusCodes.Status409Conflict)
            };
        }

        return Results.Ok(operation.Value);
    }

    private static IResult Result(
        ServiceOrderAdminOperation<ServiceOrderAdminDetail> operation)
        => operation.Outcome switch
        {
            ServiceOrderAdminOutcome.Ok =>
                Results.Ok(operation.Value),

            ServiceOrderAdminOutcome.NotFound =>
                Results.NotFound(),

            ServiceOrderAdminOutcome.Invalid =>
                Results.ValidationProblem(
                    new Dictionary<string, string[]>
                    {
                        ["serviceOrder"] =
                        [
                            operation.Error
                            ?? "Los datos de la orden no son válidos."
                        ]
                    },
                    title: "Los datos de la orden no son válidos."),

            ServiceOrderAdminOutcome.Conflict =>
                Results.Problem(
                    title: operation.Error,
                    statusCode: StatusCodes.Status409Conflict),

            _ =>
                Results.Problem(
                    title: "No se pudo procesar la orden.",
                    statusCode: StatusCodes.Status409Conflict)
        };

    private static Task WriteAudit(
        IAuditWriter audit,
        ICurrentAdmin current,
        string action,
        ServiceOrderAdminDetail order,
        string summary,
        CancellationToken cancellationToken)
        => audit.WriteAsync(
            new AuditEntry(action)
            {
                AdminUserId = current.AdminUserId,
                AdminUserEmail = current.Email,
                ModuleCode = ServiceOrdersModule.ModuleCode,
                EntityType = "service_order",
                EntityId = order.ServiceOrderId.ToString(),
                Summary = summary
            },
            cancellationToken);
}
