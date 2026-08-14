namespace Sillar.Core.Domain.Values;

/// <summary>Roles de los usuarios administradores.</summary>
/// <remarks>
/// Tres roles fijos (ADR-010). Los permisos granulares por módulo se difieren
/// hasta que exista un caso real que los pida.
/// </remarks>
public static class AdminRole
{
    /// <summary>Todo, incluida la gestión de usuarios y de módulos.</summary>
    public const string SuperAdmin = "super_admin";

    /// <summary>Administración del contenido y la configuración del negocio.</summary>
    public const string Admin = "admin";

    /// <summary>Edición de contenido y carga de archivos.</summary>
    public const string Editor = "editor";

    /// <summary>Todos los valores admitidos.</summary>
    public static readonly string[] All = [SuperAdmin, Admin, Editor];
}
