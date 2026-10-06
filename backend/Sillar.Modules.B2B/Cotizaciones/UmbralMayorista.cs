using System.Globalization;
using Sillar.Core.Contracts;

namespace Sillar.Modules.B2B.Cotizaciones;

/// <summary>Cómo quedó la evaluación mayorista de una cotización.</summary>
/// <param name="Estado">
/// <c>configuracion_pendiente</c> · <c>no_evaluable</c> · <c>alcanza</c> · <c>no_alcanza</c>.
/// </param>
/// <param name="Umbral">El importe configurado, si lo hay.</param>
/// <param name="ImporteDeLista">Suma a precio de lista de las líneas de catálogo, si se puede calcular.</param>
/// <param name="Motivo">La frase para el personal.</param>
public sealed record EvaluacionMayorista(string Estado, decimal? Umbral, decimal? ImporteDeLista, string Motivo);

/// <summary>
/// El umbral mayorista (SPEC regla 4; clave ratificada por JP el 30/09/2026).
/// </summary>
/// <remarks>
/// <para>
/// <b>Es un importe, no una cantidad</b>, y se compara contra el <b>precio de
/// lista</b>, nunca contra el ya descontado. <b>Solo se lee por
/// <see cref="ISettingsReader"/></b>: <c>core.site_settings</c> es de CORE y M07
/// no la toca.
/// </para>
/// <para>
/// <b>No aplica ningún descuento.</b> La SPEC hace del umbral un criterio del
/// personal al cotizar; esto solo le dice si se alcanza. Y si la clave no está
/// configurada —ausente, <c>PENDIENTE_DEFINIR</c> o no numérica— lo dice así, en
/// vez de inventar un importe. Por eso se lee el texto y no <c>Get&lt;decimal&gt;</c>:
/// ese devuelve 0 cuando no puede convertir, y 0 se leería como «todo alcanza».
/// </para>
/// </remarks>
public sealed class UmbralMayorista(ISettingsReader ajustes)
{
    public const string Clave = "b2b_wholesale_threshold_amount";

    /// <summary>Evalúa unas líneas: (precio de lista a la fecha, cantidad, es de catálogo).</summary>
    public EvaluacionMayorista Evaluar(IReadOnlyCollection<(decimal? PrecioDeLista, int Cantidad, bool DeCatalogo)> lineas)
    {
        var crudo = ajustes.Get(Clave);
        if (string.IsNullOrWhiteSpace(crudo)
            || !decimal.TryParse(crudo, NumberStyles.Number, CultureInfo.InvariantCulture, out var umbral)
            || umbral < 0)
        {
            return new("configuracion_pendiente", null, null,
                "El umbral mayorista todavía no está configurado. Defínelo en Configuración para que el panel pueda decir si esta cotización lo alcanza; mientras tanto, es criterio tuyo.");
        }

        if (lineas.Count == 0 || lineas.Any(l => !l.DeCatalogo || l.PrecioDeLista is null))
        {
            return new("no_evaluable", umbral, null,
                "Hay líneas sin precio de catálogo, así que el umbral no se puede calcular: decide con tu criterio (SPEC regla 4).");
        }

        var importe = lineas.Sum(l => l.PrecioDeLista!.Value * l.Cantidad);
        return importe >= umbral
            ? new("alcanza", umbral, importe, "A precio de lista, esta cotización alcanza el umbral mayorista.")
            : new("no_alcanza", umbral, importe, "A precio de lista, esta cotización no alcanza el umbral mayorista.");
    }
}
