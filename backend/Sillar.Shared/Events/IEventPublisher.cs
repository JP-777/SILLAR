namespace Sillar.Shared.Events;

/// <summary>Publica un evento de dominio en el bus interno.</summary>
/// <remarks>
/// El bus es la única vía por la que un módulo se entera de lo que pasa en otro
/// sin conocerlo, y <b>quien publica no sabe si alguien escucha</b> — ni le
/// hace falta.
/// <para>
/// Ya escucha alguien: M02 se suscribe a <c>ProductoActualizado</c> para
/// refrescar los productos que tiene destacados en la portada. Este comentario
/// decía «hoy nadie escucha» y se quedó atrás — <b>un comentario caducado
/// miente con más autoridad que el código</b>, porque nadie lo comprueba.
/// </para>
/// <para>
/// <b>Lo que no promete:</b> ni cola, ni reintentos, ni orden garantizado
/// entre manejadores. La entrega es síncrona respecto a quien publica, así que
/// un manejador lento retrasa la respuesta. Que hoy se despachen en serie es
/// un detalle de <see cref="InProcessEventBus"/>, <b>no una garantía</b>: quien
/// necesite serializar, que lo haga por su cuenta.
/// </para>
/// </remarks>
public interface IEventPublisher
{
    /// <summary>Entrega el evento a los manejadores registrados, si los hay.</summary>
    Task PublishAsync<TEvent>(TEvent domainEvent, CancellationToken cancellationToken)
        where TEvent : notnull;
}
