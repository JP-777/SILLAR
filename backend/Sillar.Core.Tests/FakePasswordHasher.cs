using Sillar.Core.Authentication;

namespace Sillar.Core.Tests;

/// <summary>
/// Verificador de contraseñas de mentira que anota lo que se le pide.
/// </summary>
/// <remarks>
/// Existe sobre todo para poder afirmar que el cálculo señuelo ocurrió. Ese paso
/// no cambia ninguna respuesta —solo consume tiempo—, así que sin una prueba que
/// lo vigile, alguien podría borrarlo en un refactor y el único síntoma sería
/// una fuga de información medible con un cronómetro.
/// </remarks>
internal sealed class FakePasswordHasher(bool passwordMatches = true) : IPasswordHasher
{
    /// <summary>Veces que se pidió el cálculo señuelo.</summary>
    public int DecoyVerifications { get; private set; }

    /// <summary>Veces que se verificó una contraseña real.</summary>
    public int RealVerifications { get; private set; }

    public string Hash(string password) => $"hash-de-{password}";

    public bool Verify(string password, string hash)
    {
        RealVerifications++;
        return passwordMatches;
    }

    public void VerifyDecoy(string password) => DecoyVerifications++;
}
