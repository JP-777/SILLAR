namespace Sillar.Modules.Crm.Authentication;

/// <summary>Caducidades explícitas de los tokens de un solo uso.</summary>
internal static class CustomerTokenPolicy
{
    public static readonly TimeSpan EmailVerificationLifetime =
        TimeSpan.FromHours(24);

    public static readonly TimeSpan PasswordResetLifetime =
        TimeSpan.FromMinutes(30);

    public static readonly TimeSpan InvitationLifetime =
        TimeSpan.FromHours(72);
}
