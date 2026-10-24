namespace Sillar.Core.Contracts;

/// <summary>
/// Procedencia de la instalación: de qué instalación, de qué nodo y de qué versión
/// sale un dato. Fase A, candidato.
/// </summary>
/// <remarks>
/// <para>
/// Es el sello que pueden llevar un informe exportado (M10), una evidencia
/// recibida (M11) o una cuota (M12). Solo con hechos que CORE ya tiene:
/// <c>core.installation</c>, el nodo configurado en <c>Sillar:Node:Code</c> y la
/// versión del ensamblado que corre.
/// </para>
/// <para>
/// <b>Observada, no firmada.</b> Dice lo que esta instalación leyó de sí misma en
/// <see cref="InstallationProvenanceResult.ObservedAt"/>. No es una prueba
/// criptográfica ni resiste a quien controle la base o la configuración.
/// </para>
/// <para>
/// <b>Si falta un hecho, no hay procedencia.</b> No se rellena con el nodo por
/// defecto, ni con una clave vacía, ni eligiendo entre dos instalaciones. Si el
/// token del llamador se cancela, se propaga <see cref="OperationCanceledException"/>;
/// cualquier otro fallo de lectura se responde como
/// <see cref="ProvenanceFailure.AuthorityUnavailable"/>.
/// </para>
/// </remarks>
public interface IInstallationProvenance
{
    /// <summary>Lee la procedencia vigente, en un solo instante.</summary>
    Task<InstallationProvenanceResult> ReadAsync(CancellationToken cancellationToken);
}

/// <summary>Los hechos de procedencia, todos presentes.</summary>
/// <param name="InstallationKey">Identidad interna persistida. NO es un identificador público ni una credencial.</param>
/// <param name="NodeCode">El nodo configurado, que es el que se escribe en <c>origin_node</c>. Nunca el valor por defecto.</param>
/// <param name="InstalledProductVersion">La versión registrada en <c>core.installation</c> al instalar.</param>
/// <param name="RunningProductVersion">La versión del ensamblado que atiende ahora. Puede diferir de la instalada tras una actualización.</param>
/// <param name="Source">De dónde salen los hechos.</param>
public sealed record InstallationProvenance(
    Guid InstallationKey,
    string NodeCode,
    string InstalledProductVersion,
    string RunningProductVersion,
    string Source);

/// <summary>Por qué no hay procedencia.</summary>
public enum ProvenanceFailure
{
    /// <summary>No se pudo leer: fallo del proveedor, tiempo agotado o cancelación interna.</summary>
    AuthorityUnavailable,

    /// <summary>No hay fila en <c>core.installation</c>.</summary>
    InstallationMissing,

    /// <summary>Clave de instalación vacía, o más de una instalación.</summary>
    InstallationIdentityInvalid,

    /// <summary>La instalación no terminó su configuración inicial.</summary>
    InstallationIncomplete,

    /// <summary><c>Sillar:Node:Code</c> no está configurado: el nodo en uso sería el de por defecto.</summary>
    NodeNotConfigured,

    /// <summary>El nodo configurado no coincide con el que el proceso estampa en las filas.</summary>
    NodeInconsistent,
}

/// <summary>La procedencia, o por qué no la hay. Exactamente una de las dos.</summary>
/// <param name="Provenance">Los hechos, si están todos.</param>
/// <param name="Failure">La causa, si falta alguno.</param>
/// <param name="ObservedAt">El único instante de la observación.</param>
public sealed record InstallationProvenanceResult(
    InstallationProvenance? Provenance,
    ProvenanceFailure? Failure,
    DateTimeOffset ObservedAt)
{
    /// <summary>Verdadero si hay procedencia completa.</summary>
    public bool IsObserved => Provenance is not null;

    /// <summary>Procedencia completa.</summary>
    public static InstallationProvenanceResult Observed(InstallationProvenance provenance, DateTimeOffset observedAt)
        => new(provenance, null, observedAt);

    /// <summary>Sin procedencia.</summary>
    public static InstallationProvenanceResult Unavailable(ProvenanceFailure failure, DateTimeOffset observedAt)
        => new(null, failure, observedAt);
}
