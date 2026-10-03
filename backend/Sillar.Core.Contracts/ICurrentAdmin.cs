namespace Sillar.Core.Contracts;

/// <summary>Quién está haciendo la petición.</summary>
/// <remarks>
/// Es la única forma que tienen los demás módulos de saberlo. Ningún módulo
/// consulta <c>core.admin_users</c>: ni la tabla ni la cookie ni el token le
/// pertenecen.
///
/// Sin sesión, todas las propiedades valen <c>null</c> y <see cref="IsInRole"/>
/// devuelve <c>false</c>. No lanza: preguntar quién eres cuando no eres nadie es
/// una pregunta legítima.
/// </remarks>
public interface ICurrentAdmin
{
    /// <summary>Identificador del administrador, o <c>null</c> si no hay sesión.</summary>
    int? AdminUserId { get; }

    /// <summary>Correo del administrador, o <c>null</c> si no hay sesión.</summary>
    string? Email { get; }

    /// <summary>Rol del administrador, o <c>null</c> si no hay sesión.</summary>
    string? Role { get; }

    /// <summary>
    /// Nombre visible del administrador (<c>full_name</c>), o <c>null</c> si no hay sesión.
    /// </summary>
    /// <remarks>
    /// Es el nombre para enseñar a una persona o para congelarlo en una
    /// fotografía. No es <see cref="Email"/>: el correo identifica, pero no es un nombre.
    /// </remarks>
    string? DisplayName { get; }

    /// <summary>
    /// Nodo al que pertenece la cuenta, o <c>null</c> si no hay sesión.
    /// </summary>
    /// <remarks>
    /// <see cref="AdminUserId"/> es un entero local: solo identifica a alguien
    /// junto a este nodo. Una fotografía del autor guardada en una tabla que se
    /// replica lleva los dos. No es el nodo de la instalación que atiende la
    /// petición, aunque hoy coincidan.
    /// </remarks>
    string? HomeNode { get; }

    /// <summary>
    /// Indica si el usuario alcanza el rol indicado, contando la jerarquía:
    /// un <c>super_admin</c> satisface también <c>admin</c> y <c>editor</c>.
    /// </summary>
    bool IsInRole(string role);
}
