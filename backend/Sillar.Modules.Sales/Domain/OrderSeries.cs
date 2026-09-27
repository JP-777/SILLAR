namespace Sillar.Modules.Sales.Domain;

/// <summary>
/// El contador de una serie de códigos de pedido: una fila por
/// <c>(nodo, año)</c>.
/// </summary>
/// <remarks>
/// <para>
/// <b>No se replica, y esta vez la pregunta de la ADR-016 se responde con un
/// no rotundo:</b> ¿puede esta fila nacer en un nodo y tener que existir en otro?
/// No — y si existiera en los dos <b>dejaría de contar</b>, porque dos nodos
/// incrementándola sin coordinación entregarían el mismo número. El contador es
/// del nodo por naturaleza. Clave <c>integer GENERATED ALWAYS AS IDENTITY</c>.
/// </para>
/// <para>
/// <b>Ninguna tabla replicada la referencia</b>, que es lo que la ADR-018 exige
/// comprobar en la otra dirección: el pedido guarda su código como texto, no una
/// clave foránea hacia aquí. Si la tuviera, un pedido que viajara a otro nodo
/// apuntaría a un contador que allí es otro.
/// </para>
/// <para>
/// <b>Por qué una fila y no una secuencia de PostgreSQL.</b> <c>nextval</c> es
/// deliberadamente no transaccional —entrega el número fuera de la transacción
/// para no serializar a quien lo pide—, así que <b>una transacción revertida
/// consume el número igual</b> y el hueco queda para siempre. Con «sin huecos»
/// aprobado el 27 de septiembre de 2026, la secuencia queda excluida, no
/// penalizada. El coste de la fila es que se bloquea mientras la transacción vive,
/// y eso serializa la confirmación de pedidos <b>de una misma serie</b>: se acepta
/// a cambio de la continuidad.
/// </para>
/// <para>
/// <b>La serie reinicia cada año</b> —<c>P-2026-9999</c> pasa a
/// <c>P-2027-0001</c>—, por excepción expresa a la ADR-016 ratificada por el líder
/// técnico. Su tabla de la regla 2 admite renumerar «mientras no salte ni
/// reinicie»; aquí se conserva el nodo delante y <b>se excepciona solo el
/// reinicio</b>.
/// </para>
/// </remarks>
public class OrderSeries
{
    /// <summary>Identidad de la fila. Local del nodo, nunca se muestra.</summary>
    public int OrderSeriesId { get; set; }

    /// <summary>
    /// El nodo cuya serie es esta.
    /// </summary>
    /// <remarks>
    /// <b>Se llama <c>node_code</c> y no <c>origin_node</c> a propósito:</b> esta
    /// tabla no se replica, así que no lleva las cuatro columnas de replicación, y
    /// usar el nombre de la columna de sello haría creer que sí. Aquí el nodo es
    /// parte de la identidad de la serie, no la marca de dónde nació una fila que
    /// viaja.
    /// </remarks>
    public required string NodeCode { get; set; }

    /// <summary>El año de la serie.</summary>
    public int Year { get; set; }

    /// <summary>
    /// El último número entregado. Empieza en cero: el primer pedido recibe el 1.
    /// </summary>
    public int LastNumber { get; set; }

    /// <summary>Cuándo se creó la serie.</summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>Cuándo entregó su último número. La escribe el trigger.</summary>
    public DateTimeOffset UpdatedAt { get; set; }
}
