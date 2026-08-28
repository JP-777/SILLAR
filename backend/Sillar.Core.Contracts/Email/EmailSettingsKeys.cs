namespace Sillar.Core.Contracts.Email;

/// <summary>Claves compartidas para configurar SMTP.</summary>
public static class EmailSettingsKeys
{
    public const string Server = "smtp_server";
    public const string Port = "smtp_port";
    public const string From = "smtp_from";

    /// <summary>
    /// La contraseña nunca vive en core.site_settings.
    /// </summary>
    public const string PasswordEnvironmentVariable = "SILLAR_SMTP_PASSWORD";

    public static readonly string[] All = [Server, Port, From];

    public static bool IsMailSetting(string key)
        => All.Contains(key, StringComparer.OrdinalIgnoreCase);
}
