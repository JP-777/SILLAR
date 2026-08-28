namespace Sillar.Core.Contracts.Email;

/// <summary>Correo saliente solicitado por un módulo.</summary>
public sealed record OutgoingEmail(
    string Recipient,
    string Subject,
    string TextBody,
    string Kind,
    string ModuleCode);

/// <summary>Resultado observable de un intento de envío.</summary>
public sealed record EmailSendResult(
    bool Success,
    string? Error = null);

/// <summary>Capacidad compartida de correo saliente.</summary>
/// <remarks>
/// El envío es inmediato y ocurre después de persistir el hecho que lo motivó.
/// No hay cola ni reintentos automáticos. Un fallo SMTP no revierte registro,
/// verificación, recuperación ni ninguna otra transacción del módulo llamador.
/// El cuerpo nunca se escribe en auditoría porque puede contener tokens.
/// </remarks>
public interface IEmailSender
{
    Task<EmailSendResult> SendAsync(
        OutgoingEmail message,
        CancellationToken cancellationToken);
}
