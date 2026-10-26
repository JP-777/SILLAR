namespace Sillar.Modules.Sales.Domain;

/// <summary>
/// La forma de un código de moneda ISO 4217: tres letras latinas mayúsculas.
/// </summary>
/// <remarks>
/// <para>
/// <b>Comprueba la forma, no la lista.</b> Copiar aquí la tabla de ISO 4217 ataría
/// el historial a una lista que cambia; la forma no cambia. Que la moneda sea la que
/// el negocio cobra lo decide la autoridad de moneda, que todavía no existe.
/// </para>
/// <para>
/// <b>No corrige.</b> «pen» no se convierte en «PEN»: quien escribe en minúsculas no
/// está escribiendo el código, y aceptarlo en silencio es la misma clase de favor que
/// suponer PEN cuando falta. La base vuelve a imponer la regla con <c>CHECK</c>,
/// comparando en la colación <c>"C"</c> — las dos mitades de la misma regla.
/// </para>
/// <para>
/// <b>Nulo no es un error: es «desconocida».</b> Los pedidos y pagos anteriores a la
/// columna no tienen moneda y no se les inventa una.
/// </para>
/// </remarks>
public static class CurrencyCode
{
    /// <summary>La expresión del <c>CHECK</c>, para que dominio y base no se separen.</summary>
    public const string CheckPattern = "^[A-Z]{3}$";

    /// <summary>Si el texto tiene la forma exacta de un código ISO 4217.</summary>
    public static bool TieneFormaIso(string? codigo)
        => codigo is { Length: 3 } && codigo.All(c => c is >= 'A' and <= 'Z');
}
