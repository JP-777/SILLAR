using System.Security.Cryptography;
using BCryptNet = BCrypt.Net.BCrypt;

namespace Sillar.Core.Authentication;

/// <summary>Hashes de contraseña con BCrypt.</summary>
public sealed class BCryptPasswordHasher : IPasswordHasher
{
    /// <summary>Factor de trabajo mínimo admitido.</summary>
    public const int MinimumWorkFactor = 12;

    private readonly int _workFactor;
    private readonly string _decoyHash;

    /// <summary>Crea el verificador con el factor de trabajo indicado.</summary>
    /// <param name="workFactor">Factor de trabajo de BCrypt. Nunca por debajo de 12.</param>
    public BCryptPasswordHasher(int workFactor = MinimumWorkFactor)
    {
        if (workFactor < MinimumWorkFactor)
        {
            throw new ArgumentOutOfRangeException(
                nameof(workFactor),
                workFactor,
                $"El factor de trabajo de BCrypt no puede bajar de {MinimumWorkFactor}.");
        }

        _workFactor = workFactor;

        // El señuelo se calcula aquí, una sola vez, a partir de un valor
        // aleatorio que nadie conoce y con el MISMO factor de trabajo que los
        // hashes reales. Si fuera una constante escrita en el código con factor
        // 12 y alguien subiera el factor, el señuelo pasaría a tardar menos que
        // una verificación real y volvería a abrirse el margen de tiempo que
        // este mecanismo existe para cerrar.
        //
        // Cuesta un cálculo de BCrypt al arrancar. Una vez.
        _decoyHash = BCryptNet.HashPassword(
            Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)),
            workFactor);
    }

    /// <inheritdoc />
    public string Hash(string password) => BCryptNet.HashPassword(password, _workFactor);

    /// <inheritdoc />
    public bool Verify(string password, string hash)
    {
        try
        {
            return BCryptNet.Verify(password, hash);
        }
        catch (BCrypt.Net.SaltParseException)
        {
            // Un hash corrupto en la base no debe tumbar el acceso con un error
            // del servidor: es una credencial que no sirve, y punto.
            return false;
        }
    }

    /// <inheritdoc />
    public void VerifyDecoy(string password) => BCryptNet.Verify(password, _decoyHash);
}
