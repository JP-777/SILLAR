using System.Net.Mail;

namespace Sillar.Core.Authentication;

/// <summary>Validación del correo que sirve de identificador de acceso.</summary>
internal static class EmailAddress
{
    /// <summary>Longitud máxima, la de la columna.</summary>
    public const int MaxLength = 150;

    /// <summary>Comprueba que el correo sea utilizable como identificador.</summary>
    /// <remarks>
    /// Sin expresiones regulares heroicas: validar correos con una regexp es un
    /// clásico que siempre acaba rechazando direcciones legítimas. Basta con que
    /// el analizador del framework lo entienda y con que no traiga adornos como
    /// <c>Nombre &lt;correo@dominio&gt;</c>, que aquí no sirven.
    /// </remarks>
    public static bool IsValid(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var trimmed = value.Trim();

        return trimmed.Length <= MaxLength
            && !trimmed.Contains(' ')
            && MailAddress.TryCreate(trimmed, out var parsed)
            && parsed.Address == trimmed;
    }
}
