using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Sillar.Modules.Crm.Authentication;
using Sillar.Modules.Crm.Contracts;
using Sillar.Modules.Crm.Dtos;
using Sillar.Modules.Crm.Profiles;

namespace Sillar.Modules.Crm.Endpoints;

/// <summary>Perfil y direcciones del cliente autenticado.</summary>
public static class CustomerProfileEndpoints
{
    private const string Prefix = "/api/customer/profile";
    private const string Tag = "Perfil de cliente";

    public static IEndpointRouteBuilder MapCustomerProfileEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        var profile = endpoints.MapGroup(Prefix)
            .WithTags(Tag)
            .RequireAuthorization(CustomerAuthorization.PolicyName)
            .AddEndpointFilter<CustomerCsrfEndpointFilter>();

        profile.MapGet("", (Delegate)GetProfile)
            .WithName("CustomerProfileGet")
            .WithSummary("Devuelve el perfil propio y sus direcciones activas.")
            .Produces<CustomerProfileResponse>(StatusCodes.Status200OK);

        profile.MapPut("", (Delegate)UpdateProfile)
            .WithName("CustomerProfileUpdate")
            .WithSummary("Actualiza los datos propios del cliente.")
            .WithDescription(
                "Cambiar el correo vuelve a dejarlo pendiente de verificación.")
            .Produces<CustomerProfileResponse>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status409Conflict);

        profile.MapPost("/addresses", (Delegate)CreateAddress)
            .WithName("CustomerAddressCreate")
            .WithSummary("Añade una dirección al perfil.")
            .Produces<CustomerAddressResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem();

        profile.MapPut("/addresses/{customerAddressId:guid}", (Delegate)UpdateAddress)
            .WithName("CustomerAddressUpdate")
            .WithSummary("Edita una dirección propia.")
            .Produces<CustomerAddressResponse>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound);

        profile.MapPost(
                "/addresses/{customerAddressId:guid}/preferred",
                (Delegate)SetPreferred)
            .WithName("CustomerAddressSetPreferred")
            .WithSummary("Marca una dirección propia como preferida.")
            .Produces<CustomerAddressResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        profile.MapDelete("/addresses/{customerAddressId:guid}", (Delegate)DeleteAddress)
            .WithName("CustomerAddressDelete")
            .WithSummary("Da de baja una dirección propia.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return endpoints;
    }

    private static async Task<IResult> GetProfile(
        CurrentCustomer current,
        CustomerProfileService profiles,
        CancellationToken cancellationToken)
    {
        if (current.CustomerId is not { } customerId)
        {
            return Results.Unauthorized();
        }

        var profile = await profiles.GetAsync(
            customerId,
            cancellationToken);

        return profile is null
            ? Results.NotFound()
            : Results.Ok(profile);
    }

    private static async Task<IResult> UpdateProfile(
        UpdateCustomerProfileRequest request,
        CurrentCustomer current,
        CustomerProfileService profiles,
        CancellationToken cancellationToken)
    {
        if (current.CustomerId is not { } customerId)
        {
            return Results.Unauthorized();
        }

        var errors = ValidateProfile(request);

        if (errors.Count > 0)
        {
            return Results.ValidationProblem(
                errors,
                title: "Revisa los datos del perfil.");
        }

        var result = await profiles.UpdateAsync(
            customerId,
            request,
            cancellationToken);

        return result.Outcome switch
        {
            CustomerProfileUpdateOutcome.Updated =>
                Results.Ok(result.Profile),

            CustomerProfileUpdateOutcome.EmailConflict =>
                Results.Problem(
                    title: "Ese correo ya pertenece a otra ficha.",
                    statusCode: StatusCodes.Status409Conflict),

            CustomerProfileUpdateOutcome.DocumentConflict =>
                Results.Problem(
                    title: "Ese documento ya pertenece a otra ficha.",
                    statusCode: StatusCodes.Status409Conflict),

            _ => Results.NotFound()
        };
    }

    private static async Task<IResult> CreateAddress(
        SaveCustomerAddressRequest request,
        CurrentCustomer current,
        CustomerProfileService profiles,
        CancellationToken cancellationToken)
    {
        if (current.CustomerId is not { } customerId)
        {
            return Results.Unauthorized();
        }

        var errors = ValidateAddress(request);

        if (errors.Count > 0)
        {
            return Results.ValidationProblem(
                errors,
                title: "Revisa la dirección.");
        }

        var address = await profiles.CreateAddressAsync(
            customerId,
            request,
            cancellationToken);

        return address is null
            ? Results.NotFound()
            : Results.Created(
                $"{Prefix}/addresses/{address.CustomerAddressId}",
                address);
    }

    private static async Task<IResult> UpdateAddress(
        Guid customerAddressId,
        SaveCustomerAddressRequest request,
        CurrentCustomer current,
        CustomerProfileService profiles,
        CancellationToken cancellationToken)
    {
        if (current.CustomerId is not { } customerId)
        {
            return Results.Unauthorized();
        }

        var errors = ValidateAddress(request);

        if (errors.Count > 0)
        {
            return Results.ValidationProblem(
                errors,
                title: "Revisa la dirección.");
        }

        var address = await profiles.UpdateAddressAsync(
            customerId,
            customerAddressId,
            request,
            cancellationToken);

        return address is null
            ? Results.NotFound()
            : Results.Ok(address);
    }

    private static async Task<IResult> SetPreferred(
        Guid customerAddressId,
        CurrentCustomer current,
        CustomerProfileService profiles,
        CancellationToken cancellationToken)
    {
        if (current.CustomerId is not { } customerId)
        {
            return Results.Unauthorized();
        }

        var address = await profiles.SetPreferredAsync(
            customerId,
            customerAddressId,
            cancellationToken);

        return address is null
            ? Results.NotFound()
            : Results.Ok(address);
    }

    private static async Task<IResult> DeleteAddress(
        Guid customerAddressId,
        CurrentCustomer current,
        CustomerProfileService profiles,
        CancellationToken cancellationToken)
    {
        if (current.CustomerId is not { } customerId)
        {
            return Results.Unauthorized();
        }

        return await profiles.DeleteAddressAsync(
            customerId,
            customerAddressId,
            cancellationToken)
            ? Results.NoContent()
            : Results.NotFound();
    }

    private static Dictionary<string, string[]> ValidateProfile(
        UpdateCustomerProfileRequest request)
    {
        var errors = new Dictionary<string, string[]>();
        var fullName = request.FullName?.Trim() ?? string.Empty;
        var email = request.Email?.Trim() ?? string.Empty;
        var documentType = Optional(request.DocumentType)?.ToLowerInvariant();
        var documentNumber = Optional(request.DocumentNumber);

        if (fullName.Length == 0)
        {
            errors["nombre"] = ["El nombre es obligatorio."];
        }

        if (email.Length == 0
            || email.Length > 150
            || !System.Net.Mail.MailAddress.TryCreate(email, out _))
        {
            errors["correo"] = ["Ingresa un correo válido."];
        }

        if ((documentType is null) != (documentNumber is null))
        {
            errors["documento"] =
                ["Tipo y número de documento deben enviarse juntos."];
        }
        else if (documentType is not null
                 && documentType is not "dni" and not "ruc")
        {
            errors["documento"] =
                ["El tipo de documento debe ser dni o ruc."];
        }

        return errors;
    }

    private static Dictionary<string, string[]> ValidateAddress(
        SaveCustomerAddressRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(request.AddressLine))
        {
            errors["direccion"] =
                ["La línea principal de la dirección es obligatoria."];
        }

        return errors;
    }

    private static string? Optional(string? value)
        => string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();
}
