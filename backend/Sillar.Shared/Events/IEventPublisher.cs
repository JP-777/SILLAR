namespace Sillar.Shared.Events;

/// <summary>Publica un evento de dominio en el bus interno.</summary>
/// <remarks>
/// El bus es la única vía por la que un módulo se entera de lo que pasa en otro
/// sin conocerlo. Quien publica no sabe si alguien escucha, y hoy nadie escucha:
/// M10 Reportes se alimentará de aquí cuando exista.
/// </remarks>
public interface IEventPublisher
{
    /// <summary>Entrega el evento a los manejadores registrados, si los hay.</summary>
    Task PublishAsync<TEvent>(TEvent domainEvent, CancellationToken cancellationToken)
        where TEvent : notnull;
}
