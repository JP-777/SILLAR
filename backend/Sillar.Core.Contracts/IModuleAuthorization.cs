namespace Sillar.Core.Contracts;

/// <summary>Autoridad local por operación. Relee la sesión y los permisos; no conserva positivos en caché.</summary>
public interface IModuleAuthorization
{
    /// <summary>Autoriza la operación o lanza un rechazo explícito antes de leer o modificar datos del módulo.</summary>
    Task<AuthorizedAdmin> RequireAsync(string moduleCode, string operationCode, CancellationToken cancellationToken);
}

/// <summary>Fotografía de la persona en el instante de autorizar. No es una referencia a una cuenta viva.</summary>
public sealed record AuthorizedAdmin(int LocalId, string DisplayName, string HomeNode, string Email, string Role);

/// <summary>Rechazo de autoridad, separado de una avería del lector de negocio.</summary>
public sealed class ModuleAccessDeniedException(int statusCode, string message) : Exception(message)
{
    /// <summary>401 sin sesión; 403 sin permiso; 404 sin módulo disponible; 503 si falta autoridad.</summary>
    public int StatusCode { get; } = statusCode;
}

/// <summary>La ausencia del productor nunca concede acceso. La guarda se llama desde la operación.</summary>
public static class ModuleOperationAccess
{
    public static Task<AuthorizedAdmin> RequireAsync(IServiceProvider services, string moduleCode,
        string operationCode, CancellationToken cancellationToken)
        => services.GetService(typeof(IModuleAuthorization)) is IModuleAuthorization authority
            ? authority.RequireAsync(moduleCode, operationCode, cancellationToken)
            : throw new ModuleAccessDeniedException(503, "No se pudo comprobar el permiso para esta operación.");
}
