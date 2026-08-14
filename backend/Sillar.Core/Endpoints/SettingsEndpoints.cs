using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Sillar.Core.Authentication;
using Sillar.Core.Contracts;
using Sillar.Core.Domain.Values;
using Sillar.Core.Dtos;
using Sillar.Core.Services;

namespace Sillar.Core.Endpoints;

/// <summary>Configuración del sitio, pública y administrativa.</summary>
public static class SettingsEndpoints
{
    /// <summary>Monta las rutas de configuración.</summary>
    public static IEndpointRouteBuilder MapSettingsEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/settings/public", GetPublic)
            .WithTags("Configuración")
            .WithName("GetPublicSettings")
            .WithSummary("Devuelve las configuraciones marcadas como públicas.")
            .WithDescription(
                "Público y sin sesión. Solo las claves con is_public = true, que vale false por " +
                "defecto: publicar un dato es siempre un acto deliberado.")
            .Produces<IReadOnlyDictionary<string, string>>(StatusCodes.Status200OK);

        var admin = endpoints.MapGroup("/api/admin/settings")
            .WithTags("Configuración")
            .RequireAuthorization(AdminRole.Admin)
            .AddEndpointFilter<CsrfEndpointFilter>();

        admin.MapGet("", List)
            .WithName("ListSettings")
            .WithSummary("Lista todas las configuraciones del sitio.")
            .WithDescription(
                "Incluye 'needsSetup', que marca las claves que siguen con el valor del seed y que el " +
                "negocio todavía no ha configurado.")
            .Produces<IReadOnlyList<SettingResponse>>(StatusCodes.Status200OK);

        admin.MapPut("/{key}", Update)
            .WithName("UpdateSetting")
            .WithSummary("Cambia el valor de una configuración.")
            .WithDescription(
                "Las claves no se crean ni se borran desde el API: una clave desconocida responde 404. " +
                "Cambiar 'isPublic' exige rol super_admin, aunque cambiar el valor solo exija admin.")
            .Produces<SettingResponse>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return endpoints;
    }

    /// <summary>Devuelve las configuraciones públicas.</summary>
    /// <param name="settings">Lector de configuración, servido desde la caché.</param>
    /// <returns>Pares clave-valor de las configuraciones publicadas.</returns>
    private static IResult GetPublic(ISettingsReader settings) => Results.Ok(settings.GetPublic());

    /// <summary>Lista todas las configuraciones.</summary>
    /// <param name="settings">Servicio de configuración.</param>
    /// <param name="cancellationToken">Cancelación de la petición.</param>
    /// <returns>Todas las claves con su valor, tipo, visibilidad y estado.</returns>
    private static async Task<IResult> List(
        SiteSettingService settings,
        CancellationToken cancellationToken)
        => Results.Ok(await settings.ListAsync(cancellationToken));

    /// <summary>Cambia una configuración.</summary>
    /// <param name="key">Clave a modificar. Debe existir.</param>
    /// <param name="request">Valor nuevo y, opcionalmente, la visibilidad.</param>
    /// <param name="settings">Servicio de configuración.</param>
    /// <param name="currentUser">Quién lo pide.</param>
    /// <param name="cancellationToken">Cancelación de la petición.</param>
    /// <returns>200 con la configuración, 400 si el valor no encaja, 403 si le falta rol, 404 si la clave no existe.</returns>
    private static async Task<IResult> Update(
        string key,
        UpdateSettingRequest request,
        SiteSettingService settings,
        CurrentUser currentUser,
        CancellationToken cancellationToken)
    {
        // La distinción de rol se resuelve aquí y no en la ruta porque depende
        // del cuerpo: un admin puede usar este endpoint, pero no para publicar.
        var result = await settings.UpdateAsync(
            key,
            request,
            canChangeVisibility: currentUser.IsInRole(AdminRole.SuperAdmin),
            currentUser.AdminUserId!.Value,
            currentUser.Email!,
            cancellationToken);

        return result.Outcome switch
        {
            SettingOutcome.Ok => Results.Ok(result.Setting),
            SettingOutcome.NotFound => Results.NotFound(),
            SettingOutcome.Forbidden => Results.Problem(
                title: result.Error,
                statusCode: StatusCodes.Status403Forbidden),
            _ => Results.ValidationProblem(
                new Dictionary<string, string[]> { ["valor"] = [result.Error!] },
                title: "El valor de la configuración no es válido.")
        };
    }
}
