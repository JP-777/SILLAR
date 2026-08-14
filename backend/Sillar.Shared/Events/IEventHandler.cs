namespace Sillar.Shared.Events;

/// <summary>Reacciona a un evento de dominio.</summary>
/// <typeparam name="TEvent">Evento que atiende.</typeparam>
/// <remarks>
/// Se registra en el contenedor y el bus lo encuentra solo. Un manejador que
/// falla no puede tumbar la operación que publicó el evento: el bus registra el
/// fallo y sigue con los demás.
/// </remarks>
public interface IEventHandler<in TEvent>
    where TEvent : notnull
{
    /// <summary>Atiende el evento.</summary>
    Task HandleAsync(TEvent domainEvent, CancellationToken cancellationToken);
}
