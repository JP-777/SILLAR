namespace Sillar.Core.Authentication;

/// <summary>Cálculo y verificación de hashes de contraseña.</summary>
public interface IPasswordHasher
{
    /// <summary>Calcula el hash de una contraseña.</summary>
    string Hash(string password);

    /// <summary>Comprueba una contraseña contra su hash.</summary>
    bool Verify(string password, string hash);

    /// <summary>
    /// Verifica la contraseña contra un hash señuelo, cuyo resultado se
    /// descarta.
    /// </summary>
    /// <remarks>
    /// Se invoca cuando el correo no existe. Sin este cálculo, la respuesta a un
    /// correo desconocido llega en microsegundos y la de uno registrado tarda lo
    /// que tarda BCrypt: ese margen, medido unas cuantas veces, revela qué
    /// correos están dados de alta.
    ///
    /// No devuelve nada a propósito. Si devolviera un booleano, alguien podría
    /// usarlo por error como si verificara algo.
    /// </remarks>
    void VerifyDecoy(string password);
}
