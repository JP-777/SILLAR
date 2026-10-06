using System.Collections.Concurrent;

namespace Sillar.Modules.B2B.Solicitudes;

/// <summary>Cuántas solicitudes admite una cuenta en una ventana de tiempo.</summary>
/// <param name="Maximo">Solicitudes aceptadas dentro de la ventana.</param>
/// <param name="Ventana">Duración de la ventana.</param>
/// <remarks>
/// <b>Los valores son un parámetro comercial, no una decisión de M07</b>
/// (<c>PLAN-DE-PRUEBAS-M07.md</c> §2): se leen de configuración y las pruebas
/// se escriben contra el parámetro, no contra un número.
/// </remarks>
public sealed record LimiteDeSolicitudes(int Maximo, TimeSpan Ventana)
{
    /// <summary>Valores por defecto mientras nadie configure otros.</summary>
    public static readonly LimiteDeSolicitudes PorDefecto = new(5, TimeSpan.FromHours(1));
}

/// <summary>Lo que responde el límite: si pasa, y si no, cuándo volver.</summary>
public readonly record struct DecisionDeLimite(bool Permitida, TimeSpan EsperarHasta);

/// <summary>
/// Límite de ritmo de las solicitudes <b>por cuenta de cliente</b>, no por IP.
/// </summary>
/// <remarks>
/// <para>
/// La SPEC lo daba por hecho —«el ritmo se limita contra la cuenta, igual que
/// hace CORE en el acceso»—, y no existía: <c>LockoutPolicy</c> es un bloqueo
/// por intentos de acceso, no un límite de escrituras (enmienda del 26/09).
/// </para>
/// <para>
/// <b>Por cuenta y no por IP, a propósito.</b> El precedente de M04 para su
/// contacto sin cuenta cuenta por IP (<c>ContactMessageService.cs:35-36</c>).
/// Aquí toda escritura está autenticada, así que la identidad es la cuenta; y
/// en un colegio o una oficina, muchas cuentas comparten una misma salida a
/// internet. Una cuenta agotada no puede bloquear a otra.
/// </para>
/// <para>
/// La decisión es pura —recibe el instante— y el estado vive en memoria del
/// proceso, que basta con una instancia por instalación (ADR-001). Las dos
/// formas de solicitud comparten el mismo cupo.
/// </para>
/// </remarks>
public sealed class LimitePorCuenta(LimiteDeSolicitudes limite)
{
    private readonly ConcurrentDictionary<Guid, Queue<DateTimeOffset>> porCuenta = new();

    /// <summary>Consume una unidad de cupo si la hay.</summary>
    public DecisionDeLimite Intentar(Guid cuenta, DateTimeOffset ahora)
    {
        var marcas = porCuenta.GetOrAdd(cuenta, _ => new Queue<DateTimeOffset>());

        lock (marcas)
        {
            while (marcas.Count > 0 && ahora - marcas.Peek() >= limite.Ventana)
            {
                marcas.Dequeue();
            }

            if (marcas.Count >= limite.Maximo)
            {
                return new DecisionDeLimite(false, marcas.Peek() + limite.Ventana - ahora);
            }

            marcas.Enqueue(ahora);
            return new DecisionDeLimite(true, TimeSpan.Zero);
        }
    }
}
