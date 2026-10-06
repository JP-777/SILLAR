using Sillar.Modules.B2B.Domain;

namespace Sillar.Modules.B2B.Bandeja;

/// <summary>
/// Qué estados siguen a cuál en una solicitud (SPEC §8, regla 5):
/// <c>recibida → en_revision → cotizada → cerrada</c>, y <c>rechazada</c> desde
/// cualquiera de las tres primeras.
/// </summary>
/// <remarks>
/// <para>
/// <b>Lectura de «desde cualquiera», reversible:</b> <c>cerrada</c> y
/// <c>rechazada</c> son finales. Reabrir una solicitud cerrada no está en la
/// SPEC; si hace falta, se añade aquí con su caso.
/// </para>
/// <para>
/// Pura, sin base: el servicio solo la consulta (<c>ANTES-DE-EMPEZAR-UN-MODULO.md</c> §2).
/// </para>
/// </remarks>
public static class TransicionesDeSolicitud
{
    private static readonly Dictionary<string, string[]> Siguientes = new()
    {
        [RequestStatus.Recibida] = [RequestStatus.EnRevision, RequestStatus.Rechazada],
        [RequestStatus.EnRevision] = [RequestStatus.Cotizada, RequestStatus.Rechazada],
        [RequestStatus.Cotizada] = [RequestStatus.Cerrada, RequestStatus.Rechazada],
        [RequestStatus.Cerrada] = [],
        [RequestStatus.Rechazada] = [],
    };

    /// <summary>Los cinco estados, en el orden en que se recorren.</summary>
    public static IReadOnlyList<string> Todos => [.. Siguientes.Keys];

    public static bool EsEstado(string? estado) => estado is not null && Siguientes.ContainsKey(estado);

    public static bool Permitida(string desde, string hasta)
        => Siguientes.TryGetValue(desde, out var destinos) && destinos.Contains(hasta);

    /// <summary>Cómo se lee el estado en una frase para una persona.</summary>
    public static string Nombre(string estado) => estado switch
    {
        RequestStatus.Recibida => "recibida",
        RequestStatus.EnRevision => "en revisión",
        RequestStatus.Cotizada => "cotizada",
        RequestStatus.Cerrada => "cerrada",
        RequestStatus.Rechazada => "rechazada",
        _ => estado,
    };

    /// <summary>La frase de conflicto: qué lo impide y qué se puede hacer.</summary>
    public static string PorQueNo(string desde, string hasta)
    {
        var siguientes = Siguientes.GetValueOrDefault(desde) ?? [];
        return siguientes.Length == 0
            ? $"La solicitud ya está {Nombre(desde)} y no admite más cambios de estado."
            : $"Una solicitud {Nombre(desde)} no puede pasar a {Nombre(hasta)}. Puede pasar a: {string.Join(" o ", siguientes.Select(Nombre))}.";
    }
}
