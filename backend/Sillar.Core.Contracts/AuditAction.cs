namespace Sillar.Core.Contracts;

/// <summary>Acciones que se registran en <c>core.audit_log</c>.</summary>
public static class AuditAction
{
    /// <summary>Alta de un registro.</summary>
    public const string Create = "create";

    /// <summary>Modificación de un registro.</summary>
    public const string Update = "update";

    /// <summary>Baja de un registro, lógica o física.</summary>
    public const string Delete = "delete";

    /// <summary>Activación de un módulo.</summary>
    public const string Activate = "activate";

    /// <summary>Desactivación de un módulo.</summary>
    public const string Deactivate = "deactivate";

    /// <summary>Acceso correcto.</summary>
    public const string Login = "login";

    /// <summary>Intento de acceso fallido.</summary>
    public const string LoginFailed = "login_failed";

    /// <summary>Cierre de sesión.</summary>
    public const string Logout = "logout";

    /// <summary>Finalización del modo instalación.</summary>
    public const string Setup = "setup";

    /// <summary>Intento de envío de correo.</summary>
    public const string EmailSend = "email_send";

    /// <summary>Todos los valores admitidos.</summary>
    public static readonly string[] All =
        [Create, Update, Delete, Activate, Deactivate, Login, LoginFailed, Logout, Setup, EmailSend];
}
