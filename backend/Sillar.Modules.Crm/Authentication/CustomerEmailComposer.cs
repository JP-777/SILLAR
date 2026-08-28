using Sillar.Core.Contracts.Email;

namespace Sillar.Modules.Crm.Authentication;

internal static class CustomerEmailComposer
{
    public static OutgoingEmail Verification(
        CustomerIssuedToken issued,
        string baseUrl)
    {
        var link =
            $"{baseUrl.TrimEnd('/')}/verificar-correo?token={Uri.EscapeDataString(issued.Token)}";

        return new OutgoingEmail(
            issued.Recipient,
            "Verifica tu correo",
            $"""
            Hola {issued.FullName}.

            Para verificar tu correo abre este enlace:
            {link}

            El enlace caduca y solo puede usarse una vez.
            """,
            "email_verification",
            CrmModule.ModuleCode);
    }

    public static OutgoingEmail PasswordReset(
        CustomerIssuedToken issued,
        string baseUrl)
    {
        var link =
            $"{baseUrl.TrimEnd('/')}/restablecer-contrasena?token={Uri.EscapeDataString(issued.Token)}";

        return new OutgoingEmail(
            issued.Recipient,
            "Restablece tu contraseña",
            $"""
            Hola {issued.FullName}.

            Para restablecer tu contraseña abre este enlace:
            {link}

            Si no solicitaste este cambio, ignora este correo.
            El enlace caduca y solo puede usarse una vez.
            """,
            "password_reset",
            CrmModule.ModuleCode);
    }

    public static OutgoingEmail Invitation(
        CustomerIssuedToken issued,
        string baseUrl)
    {
        var link =
            $"{baseUrl.TrimEnd('/')}/activar-cuenta?token={Uri.EscapeDataString(issued.Token)}";

        return new OutgoingEmail(
            issued.Recipient,
            "Activa tu cuenta",
            $"""
            Hola {issued.FullName}.

            El negocio creó una invitación para tu cuenta:
            {link}

            El enlace caduca y solo puede usarse una vez.
            """,
            "invitation",
            CrmModule.ModuleCode);
    }
}
