using System.Security.Cryptography;
using BCryptNet = BCrypt.Net.BCrypt;

namespace Sillar.Modules.Crm.Authentication;

/// <summary>BCrypt exclusivo de las credenciales de cliente.</summary>
internal sealed class CustomerPasswordHasher
{
    public const int WorkFactor = 12;

    private readonly string _decoyHash = BCryptNet.HashPassword(
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)),
        WorkFactor);

    public string Hash(string password)
        => BCryptNet.HashPassword(password, WorkFactor);

    public bool Verify(string password, string hash)
    {
        try
        {
            return BCryptNet.Verify(password, hash);
        }
        catch (BCrypt.Net.SaltParseException)
        {
            return false;
        }
    }

    /// <summary>
    /// Paga el mismo BCrypt cuando el correo no existe para no revelar cuentas
    /// mediante diferencias de tiempo.
    /// </summary>
    public void VerifyDecoy(string password)
        => BCryptNet.Verify(password, _decoyHash);
}
