using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using Sillar.Core.Contracts;
using Sillar.Core.Contracts.Email;

namespace Sillar.Core.Email;

/// <summary>Envío SMTP configurado desde core.site_settings.</summary>
internal sealed class SmtpEmailSender(
    ISettingsReader settings,
    IAuditWriter audit) : IEmailSender
{
    public async Task<EmailSendResult> SendAsync(
        OutgoingEmail message,
        CancellationToken cancellationToken)
    {
        var server = settings.Get(EmailSettingsKeys.Server);
        var portText = settings.Get(EmailSettingsKeys.Port);
        var from = settings.Get(EmailSettingsKeys.From);
        var password = Environment.GetEnvironmentVariable(
            EmailSettingsKeys.PasswordEnvironmentVariable);

        if (string.IsNullOrWhiteSpace(server)
            || server == "PENDIENTE_DEFINIR")
        {
            return await FailAsync(
                message,
                "Falta configurar smtp_server.",
                cancellationToken);
        }

        if (!int.TryParse(portText, out var port)
            || port is < 1 or > 65535)
        {
            return await FailAsync(
                message,
                "smtp_port no contiene un puerto válido.",
                cancellationToken);
        }

        if (string.IsNullOrWhiteSpace(from)
            || from == "PENDIENTE_DEFINIR")
        {
            return await FailAsync(
                message,
                "Falta configurar smtp_from.",
                cancellationToken);
        }

        if (string.IsNullOrWhiteSpace(password))
        {
            return await FailAsync(
                message,
                $"Falta la variable de entorno {EmailSettingsKeys.PasswordEnvironmentVariable}.",
                cancellationToken);
        }

        try
        {
            var mime = new MimeMessage();
            mime.From.Add(MailboxAddress.Parse(from));
            mime.To.Add(MailboxAddress.Parse(message.Recipient));
            mime.Subject = message.Subject;
            mime.Body = new TextPart("plain")
            {
                Text = message.TextBody
            };

            using var client = new SmtpClient();

            await client.ConnectAsync(
                server,
                port,
                SecureSocketOptions.Auto,
                cancellationToken);

            // El remitente es también el usuario SMTP. Si una instalación
            // necesita credenciales distintas, será una ampliación explícita
            // del contrato y no un secreto nuevo escondido en la base.
            await client.AuthenticateAsync(
                from,
                password,
                cancellationToken);

            await client.SendAsync(
                mime,
                cancellationToken);

            await client.DisconnectAsync(
                true,
                cancellationToken);

            await audit.WriteAsync(
                new AuditEntry(AuditAction.EmailSend)
                {
                    ModuleCode = message.ModuleCode,
                    EntityType = "email",
                    EntityId = message.Kind,
                    Summary =
                        $"Correo '{message.Kind}' a '{message.Recipient}': enviado."
                },
                cancellationToken);

            return new EmailSendResult(true);
        }
        catch (Exception exception)
        {
            return await FailAsync(
                message,
                exception.Message,
                cancellationToken);
        }
    }

    private async Task<EmailSendResult> FailAsync(
        OutgoingEmail message,
        string error,
        CancellationToken cancellationToken)
    {
        await audit.WriteAsync(
            new AuditEntry(AuditAction.EmailSend)
            {
                ModuleCode = message.ModuleCode,
                EntityType = "email",
                EntityId = message.Kind,
                Summary =
                    $"Correo '{message.Kind}' a '{message.Recipient}': falló. {error}"
            },
            cancellationToken);

        return new EmailSendResult(false, error);
    }
}
