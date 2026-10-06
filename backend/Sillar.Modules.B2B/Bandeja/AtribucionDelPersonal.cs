using Sillar.Core.Contracts;

namespace Sillar.Modules.B2B.Bandeja;

/// <summary>
/// La fotografía de quién realizó una actuación: <b>tres datos congelados que forman
/// una unidad</b> (R-14).
/// </summary>
/// <remarks>
/// <para>
/// <b>Dato de bitácora, no puntero.</b> Ninguno de los tres es una clave foránea hacia
/// <c>core.admin_users</c> y ninguno se resuelve con un <c>JOIN</c>: esa tabla no se
/// replica, y aunque M07 tampoco replique hoy, la regla del proyecto es no cruzar esa
/// línea — y el dato que hace falta dentro de un año es <b>quién cobró</b>, que
/// sobrevive a que la cuenta se dé de baja o se renombre.
/// </para>
/// <para>
/// <b>El nodo que lleva dentro es el de la CUENTA, no el de la actuación.</b> Son
/// hechos distintos y pueden diferir; este tipo no conoce el nodo de la instalación a
/// propósito, para que nadie derive uno del otro.
/// </para>
/// <para>
/// <b>Es la segunda vez que este tipo se escribe en el producto</b> —M03 tiene su
/// <c>StaffAttribution</c>— y no se comparte porque un módulo no importa de otro: el
/// sitio sería <c>Sillar.Shared</c>, que es costura de Integración. Queda reportado,
/// no decidido aquí.
/// </para>
/// </remarks>
/// <param name="Name">Nombre visible, congelado al actuar.</param>
/// <param name="LocalId">Identificador del trabajador dentro de su nodo de pertenencia.</param>
/// <param name="HomeNode">Nodo al que pertenece la cuenta.</param>
public sealed record AtribucionDelPersonal(string Name, int LocalId, string HomeNode)
{
    /// <summary>
    /// La atribución de quien hace la petición, o <c>null</c> si su sesión no da los
    /// tres datos.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Devuelve <c>null</c> en vez de inventar.</b> La versión anterior registraba
    /// <c>u.Email ?? "desconocido"</c>: usaba el correo como nombre visible —que no es
    /// un nombre— y, si faltaba, escribía un trabajador que no existe. R-14 prohíbe las
    /// dos cosas, y «desconocido» es la peor, porque dentro de un año nadie lo
    /// distingue de una persona real.
    /// </para>
    /// <para>
    /// Quien llama decide qué hacer con el <c>null</c>: lo que no puede es rellenarlo.
    /// </para>
    /// </remarks>
    public static AtribucionDelPersonal? De(ICurrentAdmin admin)
        => admin is { AdminUserId: > 0 and { } localId }
           && !string.IsNullOrWhiteSpace(admin.DisplayName)
           && !string.IsNullOrWhiteSpace(admin.HomeNode)
            ? new AtribucionDelPersonal(admin.DisplayName.Trim(), localId, admin.HomeNode.Trim())
            : null;
}
