using Sillar.Core.Contracts;

namespace Sillar.Core.Authentication;

/// <summary>Jerarquía de roles: <c>super_admin</c> &gt; <c>admin</c> &gt; <c>editor</c>.</summary>
/// <remarks>
/// Exigir <c>admin</c> acepta también a <c>super_admin</c>. No son permisos
/// granulares —eso llegará cuando exista un caso real que lo pida—, solo un
/// orden.
/// </remarks>
public static class RoleHierarchy
{
    // De menor a mayor. La posición en este arreglo es el nivel.
    private static readonly string[] Ranked = [AdminRole.Editor, AdminRole.Admin, AdminRole.SuperAdmin];

    /// <summary>Indica si un rol satisface lo que se exige.</summary>
    /// <param name="role">Rol del usuario.</param>
    /// <param name="required">Rol mínimo exigido.</param>
    public static bool Satisfies(string? role, string required)
    {
        var actual = LevelOf(role);
        var needed = LevelOf(required);

        return actual >= 0 && needed >= 0 && actual >= needed;
    }

    /// <summary>Nivel del rol, o -1 si no se reconoce.</summary>
    public static int LevelOf(string? role)
        => role is null ? -1 : Array.FindIndex(Ranked, known => known == role);
}
