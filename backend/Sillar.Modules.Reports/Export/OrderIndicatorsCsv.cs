using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Sillar.Modules.Reports.Orders;
using Sillar.Modules.Sales.Contracts;

namespace Sillar.Modules.Reports.Export;

/// <summary>
/// El CSV de indicadores de pedidos, compuesto en el servidor con el mismo
/// formato que el de la pantalla (<c>orderIndicatorsCsv.ts</c>), cohorte A.
/// </summary>
/// <remarks>
/// UTF-8 con BOM, coma, CRLF y comillas según RFC 4180. Toda celda de texto que
/// empiece por = + - @ (o sus formas de ancho completo, también tras espacios),
/// tabulador o salto se neutraliza con un apóstrofo. Sin identificadores,
/// clientes ni importes. El número de filas sale de las filas escritas, no de
/// lo que diga quien lo pide.
/// </remarks>
internal static partial class OrderIndicatorsCsv
{
    /// <summary>Archivo listo para auditar y entregar.</summary>
    internal sealed record Prepared(byte[] Content, int DataRows, string FileName);

    private const string Eol = "\r\n";
    private const string SourceSales = "Módulo de ventas";
    private static readonly string[] Header =
        ["Cohorte", "Inicio (incluido)", "Fin (excluido)", "Zona horaria", "Observado", "Fuente", "Indicador", "Pedidos"];

    /// <summary>La misma frase que ven el cliente y el personal (SPEC de M03 §9.0).</summary>
    internal static readonly IReadOnlyDictionary<string, string> StateLabels = new Dictionary<string, string>
    {
        [OrderStatus.PendingPayment] = "Pendiente de pago",
        [OrderStatus.PaymentToVerify] = "Pago por verificar",
        [OrderStatus.Preparing] = "En preparación",
        [OrderStatus.ReadyForPickup] = "Listo para recoger",
        [OrderStatus.Delivered] = "Entregado",
        [OrderStatus.Expired] = "Vencido",
        [OrderStatus.Cancelled] = "Cancelado",
    };

    /// <summary>
    /// Compone el archivo. Una cohorte que no cuadra no produce archivo: lanza
    /// <see cref="SourceIncoherentException"/>.
    /// </summary>
    public static Prepared Build(OrderIndicatorsResult data, string zoneId, TimeZoneInfo zone)
    {
        var (observed, measures) = Verified(data);

        var start = Stamp(data.StartInclusive, zone);
        var end = Stamp(data.EndExclusive, zone);
        var rows = measures
            .Select(m => Row(["A", start, end, zoneId, Stamp(observed, zone), SourceSales, m.Label, m.Value]))
            .ToArray();

        var text = "﻿" + string.Join(Eol, [Row([.. Header]), .. rows]) + Eol;
        var compact = (DateTimeOffset instant) => Stamp(instant, zone).Replace("-", "").Replace(":", "").Replace(" ", "");
        return new(
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetBytes(text),
            rows.Length,
            $"reportes-pedidos-{compact(data.StartInclusive)}-{compact(data.EndExclusive)}.csv");
    }

    /// <summary>
    /// D8 en el servidor: estados conocidos y sin repetir, suma por estado =
    /// creados = activos + dados de baja, vencidos = estado <c>expired</c>.
    /// </summary>
    private static (DateTimeOffset Observed, (string Label, long Value)[] Measures) Verified(OrderIndicatorsResult data)
    {
        if (data is not
            {
                SourceStatus: OrderIndicatorsSourceStatus.Available, ObservedAtUtc: { } observed,
                I01CreatedOrders: { } created, I03CurrentStatusCounts: { } byStatus,
                I04CurrentlyExpiredOrders: { } expired, ActiveOrders: { } active, InactiveOrders: { } inactive,
            })
        {
            throw new SourceIncoherentException("Cohorte incompleta.");
        }
        if (byStatus.Any(s => !StateLabels.ContainsKey(s.Status) || s.Count < 0)
            || byStatus.Select(s => s.Status).Distinct().Count() != byStatus.Count
            || new[] { created, expired, active, inactive }.Any(n => n < 0))
        {
            throw new SourceIncoherentException("Estado desconocido, repetido o recuento negativo.");
        }
        var total = MonetaryFacts.Total(byStatus.Select(s => s.Count));
        if (total != created || MonetaryFacts.Total([active, inactive]) != created
            || expired != (byStatus.SingleOrDefault(s => s.Status == OrderStatus.Expired)?.Count ?? 0))
        {
            throw new SourceIncoherentException("Los indicadores no cuadran entre sí.");
        }

        // Las filas de la vista, en su orden. Un estado que ventas no informó no
        // se escribe: nunca como cero.
        (string, long)[] measures =
        [
            ("Pedidos creados", created),
            ("Actualmente vencidos", expired),
            ("Activos", active),
            ("Dados de baja", inactive),
            .. OrderStatus.All
                .Select(code => byStatus.SingleOrDefault(s => s.Status == code))
                .OfType<ReportOrderStatusCount>()
                .Select(s => ($"Estado actual: {StateLabels[s.Status]}", s.Count)),
        ];
        return (observed, measures);
    }

    private static string Stamp(DateTimeOffset instant, TimeZoneInfo zone)
        => TimeZoneInfo.ConvertTime(instant, zone).ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);

    private static string Row(object[] cells) => string.Join(",", cells.Select(Cell));

    /// <summary>Neutraliza la inyección de fórmulas y aplica las comillas de RFC 4180.</summary>
    internal static string Cell(object value)
    {
        if (value is long number) return number.ToString(CultureInfo.InvariantCulture);
        var text = (string)value;
        var safe = FormulaStart().IsMatch(text) ? $"'{text}" : text;
        return safe.IndexOfAny(['"', ',', '\r', '\n']) >= 0 ? $"\"{safe.Replace("\"", "\"\"")}\"" : safe;
    }

    [GeneratedRegex(@"^[\s 　]*[=+\-@＝＋－＠]|^[\t\r\n]", RegexOptions.CultureInvariant)]
    private static partial Regex FormulaStart();
}
