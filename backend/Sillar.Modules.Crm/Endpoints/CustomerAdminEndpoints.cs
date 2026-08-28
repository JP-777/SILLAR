using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Sillar.Core.Contracts;
using Sillar.Modules.Crm.Administration;
using Sillar.Modules.Crm.Dtos;

namespace Sillar.Modules.Crm.Endpoints;

/// <summary>Gestión de clientes desde el panel.</summary>
public static class CustomerAdminEndpoints
{
    private const string Prefix = "/api/admin/crm/customers";
    private const string Tag = "CRM — Clientes";

    public static IEndpointRouteBuilder MapCustomerAdminEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        var admin = endpoints.MapGroup(Prefix)
            .WithTags(Tag)
            .RequireAuthorization(AdminRole.Admin)
            .AddEndpointFilter<CsrfEndpointFilter>();

        admin.MapGet("", List)
            .WithName("ListAdminCustomers")
            .WithSummary("Lista y busca clientes.")
            .WithDescription(
                "Busca por nombre, correo o documento. Devuelve hasta 100 coincidencias.")
            .Produces<IReadOnlyList<AdminCustomerListItemResponse>>(
                StatusCodes.Status200OK);

        admin.MapGet("/{customerId:guid}", Get)
            .WithName("GetAdminCustomer")
            .WithSummary("Devuelve la ficha administrativa del cliente.")
            .Produces<AdminCustomerDetailResponse>(
                StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        admin.MapPost("", Create)
            .WithName("CreateAdminCustomer")
            .WithSummary("Crea una ficha de cliente sin cuenta.")
            .Produces<AdminCustomerDetailResponse>(
                StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status409Conflict);

        admin.MapPut("/{customerId:guid}", Update)
            .WithName("UpdateAdminCustomer")
            .WithSummary("Actualiza la ficha y sus notas internas.")
            .Produces<AdminCustomerDetailResponse>(
                StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        admin.MapDelete("/{customerId:guid}", Deactivate)
            .WithName("DeactivateAdminCustomer")
            .WithSummary("Da de baja al cliente y revoca sus sesiones.")
            .Produces<AdminCustomerDetailResponse>(
                StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        admin.MapPost("/{customerId:guid}/reactivate", Reactivate)
            .WithName("ReactivateAdminCustomer")
            .WithSummary("Reactiva una ficha de cliente.")
            .Produces<AdminCustomerDetailResponse>(
                StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        admin.MapPost("/{customerId:guid}/invite", Invite)
            .WithName("InviteAdminCustomer")
            .WithSummary("Invita a una ficha sin cuenta.")
            .WithDescription(
                "Emite un enlace de un solo uso. Un fallo SMTP no revierte la invitación.")
            .Produces<AdminCustomerInvitationResponse>(
                StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        return endpoints;
    }

    private static async Task<IResult> List(
        string? q,
        CustomerAdminService customers,
        CancellationToken cancellationToken)
        => Results.Ok(
            await customers.ListAsync(
                q,
                cancellationToken));

    private static async Task<IResult> Get(
        Guid customerId,
        CustomerAdminService customers,
        CancellationToken cancellationToken)
    {
        var customer = await customers.GetAsync(
            customerId,
            cancellationToken);

        return customer is null
            ? Results.NotFound()
            : Results.Ok(customer);
    }

    private static async Task<IResult> Create(
        CreateAdminCustomerRequest request,
        CustomerAdminService customers,
        ICurrentAdmin current,
        CancellationToken cancellationToken)
    {
        var result = await customers.CreateAsync(
            request,
            current.AdminUserId!.Value,
            current.Email!,
            cancellationToken);

        return result.Outcome switch
        {
            CustomerAdminOutcome.Ok =>
                Results.Created(
                    $"{Prefix}/{result.Customer!.CustomerId}",
                    result.Customer),

            CustomerAdminOutcome.Conflict =>
                Results.Problem(
                    title: result.Error,
                    statusCode: StatusCodes.Status409Conflict),

            _ => Invalid(result.Error!)
        };
    }

    private static async Task<IResult> Update(
        Guid customerId,
        UpdateAdminCustomerRequest request,
        CustomerAdminService customers,
        ICurrentAdmin current,
        CancellationToken cancellationToken)
    {
        var result = await customers.UpdateAsync(
            customerId,
            request,
            current.AdminUserId!.Value,
            current.Email!,
            cancellationToken);

        return result.Outcome switch
        {
            CustomerAdminOutcome.Ok =>
                Results.Ok(result.Customer),

            CustomerAdminOutcome.NotFound =>
                Results.NotFound(),

            CustomerAdminOutcome.Conflict =>
                Results.Problem(
                    title: result.Error,
                    statusCode: StatusCodes.Status409Conflict),

            _ => Invalid(result.Error!)
        };
    }

    private static async Task<IResult> Deactivate(
        Guid customerId,
        CustomerAdminService customers,
        ICurrentAdmin current,
        CancellationToken cancellationToken)
    {
        var result = await customers.DeactivateAsync(
            customerId,
            current.AdminUserId!.Value,
            current.Email!,
            cancellationToken);

        return result.Outcome == CustomerAdminOutcome.Ok
            ? Results.Ok(result.Customer)
            : Results.NotFound();
    }

    private static async Task<IResult> Reactivate(
        Guid customerId,
        CustomerAdminService customers,
        ICurrentAdmin current,
        CancellationToken cancellationToken)
    {
        var result = await customers.ReactivateAsync(
            customerId,
            current.AdminUserId!.Value,
            current.Email!,
            cancellationToken);

        return result.Outcome == CustomerAdminOutcome.Ok
            ? Results.Ok(result.Customer)
            : Results.NotFound();
    }

    private static async Task<IResult> Invite(
        Guid customerId,
        CustomerAdminService customers,
        ICurrentAdmin current,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var result = await customers.InviteAsync(
            customerId,
            BaseUrl(context),
            current.AdminUserId!.Value,
            current.Email!,
            cancellationToken);

        return result.Outcome switch
        {
            CustomerAdminOutcome.Ok =>
                Results.Ok(result.Invitation),

            CustomerAdminOutcome.NotFound =>
                Results.NotFound(),

            CustomerAdminOutcome.HasAccount
                or CustomerAdminOutcome.Inactive
                or CustomerAdminOutcome.Conflict =>
                Results.Problem(
                    title: result.Error,
                    statusCode: StatusCodes.Status409Conflict),

            _ => Results.Problem(
                title: "No se pudo emitir la invitación.",
                statusCode: StatusCodes.Status409Conflict)
        };
    }

    private static string BaseUrl(HttpContext context)
        => $"{context.Request.Scheme}://{context.Request.Host}";

    private static IResult Invalid(string error)
        => Results.ValidationProblem(
            new Dictionary<string, string[]>
            {
                ["cliente"] = [error]
            },
            title: "Los datos del cliente no son válidos.");
}
