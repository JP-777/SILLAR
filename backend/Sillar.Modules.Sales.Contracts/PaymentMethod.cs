namespace Sillar.Modules.Sales.Contracts;

/// <summary>
/// Medios de pago que M03 admite <b>hoy</b>.
/// </summary>
/// <remarks>
/// <para>
/// <b>La lista contiene solo lo ratificado, y eso es deliberado.</b> El registro
/// histórico de <c>docs/PENDIENTES.md</c> dice que la tienda abre cobrando con
/// «Yape y efectivo», pero el encargo del 26 de septiembre de 2026 nombra solo
/// Yape y <b>no resuelve expresamente el efectivo</b>. Queda escalado (SPEC §0.3
/// (c)).
/// </para>
/// <para>
/// <b>Por qué solo Yape y no los dos por si acaso:</b> añadir un valor a un
/// <c>CHECK</c> después es una migración barata; <b>quitar uno que ya tiene filas,
/// no</b>. La base permite exactamente lo aprobado, y no pre-autoriza una
/// modalidad de cobro que nadie decidió.
/// </para>
/// <para>
/// La tarjeta llega con M11 y no se adelanta nada de ella.
/// </para>
/// </remarks>
public static class PaymentMethod
{
    /// <summary>Yape, con confirmación manual por una persona.</summary>
    public const string Yape = "yape";

    /// <summary>Los valores admitidos hoy. Alimenta el <c>CHECK</c> de la migración.</summary>
    public static readonly string[] All = [Yape];
}
