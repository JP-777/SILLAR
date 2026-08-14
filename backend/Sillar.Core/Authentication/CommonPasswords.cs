namespace Sillar.Core.Authentication;

/// <summary>Lista corta de contraseñas comunes, incrustada en el código.</summary>
/// <remarks>
/// No pretende ser exhaustiva —para eso haría falta un diccionario de millones
/// de entradas y una descarga que mantener—. Corta las que aparecerían primero
/// si alguien probara a mano contra el panel de un negocio peruano, sobre todo
/// las que ya cumplen los doce caracteres y por eso pasarían el filtro de
/// longitud.
///
/// Se comparan sin distinguir mayúsculas ni tildes.
/// </remarks>
internal static class CommonPasswords
{
    private static readonly HashSet<string> Values = new(StringComparer.OrdinalIgnoreCase)
    {
        "123456789012",
        "1234567890123",
        "12345678901234",
        "111111111111",
        "000000000000",
        "abcdefghijkl",
        "qwertyuiopas",
        "qwertyuiop12",
        "asdfghjkl123",
        "password1234",
        "passwordpassword",
        "contrasena12",
        "contrasena123",
        "contrasena1234",
        "micontrasena",
        "administrador",
        "administrador1",
        "administrador123",
        "bienvenido12",
        "bienvenido123",
        "iloveyou1234",
        "letmein12345",
        "sillar123456",
        "sillaradmin1",
        "peru12345678",
        "lima12345678",
        "arequipa1234",
        "libreria1234",
        "papeleria123",
        "negocio12345"
    };

    /// <summary>Indica si la contraseña está en la lista.</summary>
    public static bool Contains(string password) => Values.Contains(Normalize(password));

    /// <summary>
    /// Quita las tildes para que <c>contraseña</c> se reconozca aunque se
    /// escriba <c>contrasena</c>.
    /// </summary>
    private static string Normalize(string value)
        => string.Concat(value.Normalize(System.Text.NormalizationForm.FormD)
            .Where(character => System.Globalization.CharUnicodeInfo.GetUnicodeCategory(character)
                is not System.Globalization.UnicodeCategory.NonSpacingMark));
}
