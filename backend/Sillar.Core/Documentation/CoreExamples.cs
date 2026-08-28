using Sillar.Core.Dtos;
using Sillar.Shared.Platform;

namespace Sillar.Core.Documentation;

/// <summary>
/// Los cuerpos de ejemplo de CORE, en Swagger.
/// </summary>
/// <remarks>
/// <para>
/// El criterio es el mismo que en el resto: <b>¿podría alguien que no conoce
/// SILLAR copiar esto y que le funcione?</b> Un cuerpo con <c>"string"</c> en
/// cada campo no sirve, y es lo que el generador produce solo.
/// </para>
/// <para>
/// <b>Ninguna contraseña de ejemplo existe en ningún sitio.</b> Enseñan la
/// forma que la política admite —larga, de varias palabras, sin relación con
/// el nombre ni el correo (<c>PasswordPolicy</c>)— y dicen en su propio texto
/// que hay que cambiarlas. Las primeras que se escribieron aquí eran las del
/// arnés e2e, que además habían quedado creadas en una base de demostración:
/// un ejemplo que alguien puede copiar no puede ser una contraseña que exista.
/// Y el correo es de un dominio de ejemplo reservado, no de un negocio real
/// (ADR-008).
/// </para>
/// </remarks>
public sealed class CoreExamples : ISchemaExamples
{
    /// <inheritdoc />
    public IReadOnlyDictionary<Type, string> Examples => Cuerpos;

    private static readonly Dictionary<Type, string> Cuerpos = new()
    {
        [typeof(LoginRequest)] = """
            {
              "email": "administracion@ejemplo.test",
              "password": "la-contrasena-de-esta-instalacion"
            }
            """,

        [typeof(ChangePasswordRequest)] = """
            {
              "currentPassword": "la-que-tienes-ahora",
              "newPassword": "cambia-esto-por-una-tuya-de-varias-palabras"
            }
            """,

        [typeof(CreateAdminUserRequest)] = """
            {
              "fullName": "Encargada De Turno",
              "email": "turno@ejemplo.test",
              "password": "cambia-esto-por-una-tuya-de-varias-palabras",
              "role": "editor",
              "phone": "+51 900 000 000"
            }
            """,

        [typeof(UpdateAdminUserRequest)] = """
            {
              "fullName": "Encargada De Turno",
              "role": "admin",
              "phone": "+51 900 000 000",
              "isActive": true
            }
            """,

        // `isPublic` decide si el ajuste viaja al sitio público: el nombre del
        // negocio sí, una clave de integración no.
        [typeof(UpdateSettingRequest)] = """
            {
              "value": "Librería y Bazar de Ejemplo",
              "isPublic": true
            }
            """,

        [typeof(TestEmailRequest)] = """
            {
              "recipient": "administracion@ejemplo.test"
            }
            """,
    };
}
