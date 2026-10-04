namespace Sillar.Modules.Sales.Domain;

/// <summary>
/// La fotografía de quién realizó una actuación: <b>tres datos congelados que
/// forman una unidad.</b>
/// </summary>
/// <remarks>
/// <para>
/// Existe como tipo propio, puro y sin base de datos, por la misma razón que
/// <c>OrderCode</c>: <b>una barrera que solo se puede provocar levantando medio
/// sistema es una barrera que nadie va a provocar</b>. Aquí la coherencia de la
/// atribución se prueba en milisegundos, y la base la vuelve a imponer con
/// <c>CHECK</c> — las dos mitades de la misma regla.
/// </para>
/// <para>
/// <b>Dato de bitácora, no puntero.</b> Ninguno de los tres es una clave foránea
/// hacia <c>core.admin_users</c> y ninguno se resuelve con un <c>JOIN</c>: esa
/// tabla no se replica y las de atribución sí, y la ADR-018 prohíbe que una fila
/// que viaja referencie a una que se queda — sin avisar cuando se incumple.
/// </para>
/// <para>
/// <b>El nodo que lleva dentro es el de la CUENTA, no el de la actuación.</b> El de
/// la actuación es el <c>OriginNode</c> de la fila que guarda esta atribución, lo
/// sella el <c>DbContext</c> y <b>los dos pueden diferir</b>: una cuenta del nodo A
/// puede registrar una actuación desde el nodo B. Por eso este tipo <b>no conoce</b>
/// el nodo de la instalación ni el del pedido: si pudiera alcanzarlos, alguien
/// acabaría derivando uno del otro.
/// </para>
/// </remarks>
/// <param name="Name">
/// Nombre visible del trabajador, congelado al actuar. Sobrevive a que la cuenta se
/// renombre o se dé de baja.
/// </param>
/// <param name="LocalId">
/// Identificador del trabajador <b>dentro de su nodo de pertenencia</b>. Solo es
/// único ahí: el 7 de un nodo no es el 7 de otro.
/// </param>
/// <param name="HomeNode">
/// Nodo al que pertenece la <b>cuenta</b> — el universo contra el que
/// <paramref name="LocalId"/> se interpreta. <b>No es el nodo donde se actuó.</b>
/// </param>
public sealed record StaffAttribution(string Name, int LocalId, string HomeNode)
{
    /// <summary>
    /// Construye una atribución humana, comprobando que los tres datos están.
    /// </summary>
    /// <remarks>
    /// <b>La guarda está aquí, en la operación que construye</b>, y no en cada
    /// llamador: ponerla en el llamador protegería de ese llamador, y aquí protege
    /// de todos, incluidos los que todavía no existen.
    /// </remarks>
    /// <exception cref="ArgumentException">
    /// Si falta cualquiera de los tres, o si alguno es un valor ficticio: nombre en
    /// blanco, identificador no positivo o nodo en blanco. <b>Una atribución parcial
    /// es peor que ninguna, porque parece completa.</b>
    /// </exception>
    public static StaffAttribution De(string? name, int? localId, string? homeNode)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException(
                "Una actuación humana necesita el nombre del trabajador, congelado. " +
                "Ni «Sistema» ni un guion: un trabajador ficticio no se distingue de " +
                "uno real dentro de un año.",
                nameof(name));
        }

        if (localId is null or < 1)
        {
            throw new ArgumentException(
                "Una actuación humana necesita el identificador local del trabajador, " +
                "y tiene que ser positivo: el cero sería un trabajador ficticio con " +
                "apariencia de real.",
                nameof(localId));
        }

        if (string.IsNullOrWhiteSpace(homeNode))
        {
            throw new ArgumentException(
                "Una actuación humana necesita el nodo al que pertenece la cuenta: sin " +
                "él el identificador local es un entero sin universo. Y no se rellena " +
                "con el nodo donde se actuó — son dos hechos distintos que pueden diferir.",
                nameof(homeNode));
        }

        return new StaffAttribution(name.Trim(), localId.Value, homeNode.Trim());
    }

    /// <summary>
    /// Lee una atribución que puede no existir: la de una actuación del sistema.
    /// </summary>
    /// <remarks>
    /// <b>Los tres nulos significan «lo hizo el sistema»</b> —el vencimiento del
    /// plazo de pago lo provoca el tiempo, no una persona— y devuelve <c>null</c>.
    /// Cualquier combinación parcial es un error, no una atribución degradada.
    /// </remarks>
    /// <exception cref="ArgumentException">Si están unos y faltan otros.</exception>
    public static StaffAttribution? DeOpcional(string? name, int? localId, string? homeNode)
    {
        var presentes =
            (string.IsNullOrWhiteSpace(name) ? 0 : 1) +
            (localId is null ? 0 : 1) +
            (string.IsNullOrWhiteSpace(homeNode) ? 0 : 1);

        if (presentes == 0)
        {
            return null;
        }

        if (presentes < 3)
        {
            throw new ArgumentException(
                "Una atribución va con sus tres datos o con ninguno. Faltan unos y " +
                "están otros, y eso no es una actuación del sistema ni una humana: es " +
                "media atribución, que es peor que ninguna porque parece completa.");
        }

        return De(name, localId, homeNode);
    }
}
