using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Sillar.Core.Authentication;
using Sillar.Core.Contracts;
using Sillar.Core.Data;
using Sillar.Core.Dtos;
using Sillar.Core.Media;
using Sillar.Core.Services;
using Sillar.Shared.Paging;

namespace Sillar.Core.Endpoints;

/// <summary>Subida, listado y baja de archivos, y la ruta que los sirve.</summary>
public static class MediaEndpoints
{
    /// <summary>Monta las rutas de medios.</summary>
    public static IEndpointRouteBuilder MapMediaEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var admin = endpoints.MapGroup("/api/admin/media")
            .WithTags("Medios")
            .AddEndpointFilter<CsrfEndpointFilter>();

        admin.MapPost("", Upload)
            .RequireAuthorization(AdminRole.Editor)
            .DisableAntiforgery()
            .WithName("UploadMedia")
            .WithSummary("Sube un archivo.")
            .WithDescription(
                "multipart/form-data con el archivo, 'ownerModuleCode' obligatorio y 'altText' opcional. " +
                "El tipo se determina por el contenido, nunca por la extensión ni por el Content-Type " +
                "enviado. Solo JPEG, PNG y WebP. Requiere token CSRF: multipart no exime.")
            .Produces<MediaUploadResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status413PayloadTooLarge)
            .ProducesProblem(StatusCodes.Status415UnsupportedMediaType);

        admin.MapGet("", List)
            .RequireAuthorization(AdminRole.Editor)
            .WithName("ListMedia")
            .WithSummary("Lista los archivos, con filtros y paginación.")
            .Produces<PagedResult<MediaResponse>>(StatusCodes.Status200OK);

        admin.MapDelete("/{id:guid}", Delete)
            .RequireAuthorization(AdminRole.Admin)
            .WithName("DeleteMedia")
            .WithSummary("Da de baja un archivo.")
            .WithDescription(
                "Baja lógica: el binario se conserva, pero el archivo deja de servirse por la ruta pública. " +
                "La purga física del disco es una operación de mantenimiento.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound);

        // Fuera del API y sin sesión (ADR-011). No es UseStaticFiles: hace falta
        // consultar is_active para que un archivo dado de baja deje de servirse,
        // y el Content-Type tiene que salir de la fila, no adivinarse del nombre.
        endpoints.MapGet("/media/{year}/{month}/{storedName}", Serve)
            .WithTags("Medios")
            .WithName("ServeMedia")
            .WithSummary("Sirve un archivo.")
            .WithDescription("Público. Un archivo dado de baja responde 404.")
            // Los tres tipos concretos: el comodín 'image/*' no se admite aquí.
            .Produces(StatusCodes.Status200OK, contentType: ImageFormat.Jpeg.MimeType,
                additionalContentTypes: [ImageFormat.Png.MimeType, ImageFormat.Webp.MimeType])
            .Produces(StatusCodes.Status404NotFound);

        return endpoints;
    }

    /// <summary>Sube un archivo.</summary>
    /// <param name="request">Petición con el archivo y sus datos.</param>
    /// <param name="media">Servicio de medios.</param>
    /// <param name="currentUser">Quién sube.</param>
    /// <param name="cancellationToken">Cancelación de la petición.</param>
    /// <returns>201 con la ficha, 400 si faltan datos, 413 si pasa del tamaño, 415 si el contenido no sirve.</returns>
    private static async Task<IResult> Upload(
        HttpRequest request,
        MediaService media,
        CurrentAdmin currentUser,
        CancellationToken cancellationToken)
    {
        if (!request.HasFormContentType)
        {
            return Invalid("Se espera multipart/form-data con el archivo.");
        }

        IFormCollection form;

        try
        {
            form = await request.ReadFormAsync(cancellationToken);
        }
        catch (BadHttpRequestException exception)
            when (exception.StatusCode == StatusCodes.Status413PayloadTooLarge)
        {
            // Kestrel corta el cuerpo al pasar del límite, así que el archivo
            // nunca llega a leerse ni a escribirse. Lo que falta es traducir su
            // excepción a la respuesta que corresponde: sin esto, el cliente
            // recibe un 500 y parece un fallo del servidor.
            return TooLarge(maxUploadBytes: exception.Message);
        }

        var file = form.Files.GetFile("file") ?? form.Files.FirstOrDefault();

        if (file is null || file.Length == 0)
        {
            return Invalid("No se recibió ningún archivo en el campo 'file'.");
        }

        var ownerModuleCode = form["ownerModuleCode"].ToString();

        if (!media.IsKnownModule(ownerModuleCode))
        {
            return Invalid(
                $"'ownerModuleCode' es obligatorio y debe ser un módulo que el producto conozca. " +
                $"Recibido: '{ownerModuleCode}'.");
        }

        try
        {
            await using var content = file.OpenReadStream();

            var result = await media.UploadAsync(
                content,
                file.FileName,
                ownerModuleCode,
                form["altText"].ToString(),
                currentUser.AdminUserId!.Value,
                currentUser.Email!,
                cancellationToken);

            return Results.Created(result.Url, result);
        }
        catch (MediaRejectedException exception)
        {
            return Results.Problem(
                title: exception.Reason,
                statusCode: exception.TooLarge
                    ? StatusCodes.Status413PayloadTooLarge
                    : StatusCodes.Status415UnsupportedMediaType);
        }
    }

    /// <summary>Lista los archivos.</summary>
    /// <param name="media">Servicio de medios.</param>
    /// <param name="ownerModuleCode">Filtra por el módulo que lo subió.</param>
    /// <param name="isOrphan">Filtra por archivos cuyo módulo ya no existe.</param>
    /// <param name="mimeType">Filtra por tipo real.</param>
    /// <param name="from">Desde esta fecha.</param>
    /// <param name="to">Hasta esta fecha.</param>
    /// <param name="includeInactive">Incluye los dados de baja.</param>
    /// <param name="page">Número de página, empezando en 1.</param>
    /// <param name="pageSize">Elementos por página.</param>
    /// <param name="cancellationToken">Cancelación de la petición.</param>
    /// <returns>Una página de archivos, del más reciente al más antiguo.</returns>
    private static async Task<IResult> List(
        MediaService media,
        string? ownerModuleCode,
        bool? isOrphan,
        string? mimeType,
        DateTimeOffset? from,
        DateTimeOffset? to,
        bool? includeInactive,
        int? page,
        int? pageSize,
        CancellationToken cancellationToken)
    {
        var filter = new MediaFilter(ownerModuleCode, isOrphan, mimeType, from, to, includeInactive ?? false);

        return Results.Ok(await media.ListAsync(filter, PageRequest.Of(page, pageSize), cancellationToken));
    }

    /// <summary>Da de baja un archivo.</summary>
    /// <param name="id">Identificador del archivo.</param>
    /// <param name="media">Servicio de medios.</param>
    /// <param name="currentUser">Quién lo da de baja.</param>
    /// <param name="cancellationToken">Cancelación de la petición.</param>
    /// <returns>204 si se dio de baja, 404 si no existía o ya estaba de baja.</returns>
    private static async Task<IResult> Delete(
        Guid id,
        MediaService media,
        CurrentAdmin currentUser,
        CancellationToken cancellationToken)
        => await media.DeleteAsync(id, currentUser.AdminUserId!.Value, currentUser.Email!, cancellationToken)
            ? Results.NoContent()
            : Results.NotFound();

    /// <summary>Sirve el binario de un archivo.</summary>
    /// <param name="year">Año de la ruta.</param>
    /// <param name="month">Mes de la ruta.</param>
    /// <param name="storedName">Nombre generado del archivo.</param>
    /// <param name="database">Contexto de datos.</param>
    /// <param name="storage">Almacenamiento, para resolver la ruta física.</param>
    /// <param name="options">Configuración de medios.</param>
    /// <param name="response">Respuesta en curso, para las cabeceras.</param>
    /// <param name="cancellationToken">Cancelación de la petición.</param>
    /// <returns>El archivo con su tipo almacenado, o 404 si no existe o está de baja.</returns>
    private static async Task<IResult> Serve(
        string year,
        string month,
        string storedName,
        CoreDbContext database,
        MediaStorage storage,
        IOptions<MediaOptions> options,
        HttpResponse response,
        CancellationToken cancellationToken)
    {
        // Se busca por el nombre generado, que es único e indexado, y se
        // comprueba que la ruta completa coincida. Así la ruta de la URL no
        // decide nada por sí sola.
        var asset = await database.MediaAssets
            .AsNoTracking()
            .Where(candidate => candidate.StoredName == storedName && candidate.IsActive)
            .Select(candidate => new { candidate.RelativePath, candidate.MimeType })
            .FirstOrDefaultAsync(cancellationToken);

        if (asset is null || asset.RelativePath != $"{year}/{month}/{storedName}")
        {
            return Results.NotFound();
        }

        var path = storage.PhysicalPath(asset.RelativePath);

        if (!File.Exists(path))
        {
            return Results.NotFound();
        }

        // Impide que el navegador reinterprete el contenido y decida por su
        // cuenta que esto es otra cosa.
        response.Headers.XContentTypeOptions = "nosniff";

        // El nombre es irrepetible: el contenido de esta ruta no cambiará nunca,
        // así que se puede cachear para siempre.
        response.Headers.CacheControl = "public, max-age=31536000, immutable";

        // El tipo almacenado, verificado al subir. Nunca el adivinado del nombre.
        return Results.File(path, asset.MimeType, enableRangeProcessing: true);
    }

    private static IResult Invalid(string error) => Results.ValidationProblem(
        new Dictionary<string, string[]> { ["archivo"] = [error] },
        title: "No se pudo subir el archivo.");

    private static IResult TooLarge(string maxUploadBytes) => Results.Problem(
        title: "El archivo pasa del tamaño máximo permitido.",
        detail: maxUploadBytes,
        statusCode: StatusCodes.Status413PayloadTooLarge);
}
