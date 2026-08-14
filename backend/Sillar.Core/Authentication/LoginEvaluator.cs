namespace Sillar.Core.Authentication;

/// <summary>Datos de acceso de un administrador, tal como se leen para entrar.</summary>
/// <remarks>
/// Es una copia de solo lectura de lo que hace falta para decidir. Mantiene la
/// decisión separada de la entidad y del acceso a datos, que es lo que permite
/// probar la secuencia completa sin base de datos.
/// </remarks>
public sealed record AdminUserCredentials(
    int AdminUserId,
    string Email,
    string FullName,
    string Role,
    string PasswordHash,
    bool IsActive,
    DateTimeOffset? LockedUntil,
    int FailedLoginCount);

/// <summary>Resultado de evaluar un intento de acceso.</summary>
public enum LoginOutcome
{
    /// <summary>No hay ninguna cuenta con ese correo. Responde 401.</summary>
    UnknownEmail,

    /// <summary>La contraseña no coincide. Responde 401 y suma un intento fallido.</summary>
    WrongPassword,

    /// <summary>La contraseña es correcta pero la cuenta está bloqueada. Responde 423.</summary>
    Locked,

    /// <summary>La contraseña es correcta pero la cuenta está desactivada. Responde 401.</summary>
    Inactive,

    /// <summary>Acceso concedido.</summary>
    Granted
}

/// <summary>Resultado de la evaluación, con el momento de desbloqueo si aplica.</summary>
public sealed record LoginResult(LoginOutcome Outcome, DateTimeOffset? LockedUntil = null);

/// <summary>
/// La secuencia de inicio de sesión de la entrega 2 §4, sin efectos de borde.
/// </summary>
/// <remarks>
/// Vive aparte del endpoint porque su valor está en el orden de los pasos, y el
/// orden se puede romper sin que nada deje de compilar. Aquí se puede probar
/// entero: las cuatro combinaciones de contraseña y estado de cuenta, y que el
/// cálculo señuelo ocurre cuando el correo no existe.
/// </remarks>
public static class LoginEvaluator
{
    /// <summary>Decide qué pasa con un intento de acceso.</summary>
    /// <param name="user">Cuenta encontrada por correo, o <c>null</c> si no hay ninguna.</param>
    /// <param name="password">Contraseña recibida.</param>
    /// <param name="hasher">Verificador de contraseñas.</param>
    /// <param name="now">Momento actual, para juzgar el bloqueo.</param>
    public static LoginResult Evaluate(
        AdminUserCredentials? user,
        string password,
        IPasswordHasher hasher,
        DateTimeOffset now)
    {
        // Paso 2. El correo no existe, pero se gasta el mismo tiempo que en una
        // verificación real antes de responder. No es opcional: sin esto, medir
        // el tiempo de respuesta revela qué correos están registrados.
        if (user is null)
        {
            hasher.VerifyDecoy(password);
            return new LoginResult(LoginOutcome.UnknownEmail);
        }

        // Paso 3 y 4. La contraseña se verifica ANTES de mirar el bloqueo.
        if (!hasher.Verify(password, user.PasswordHash))
        {
            return new LoginResult(LoginOutcome.WrongPassword);
        }

        // Paso 5. Solo quien acierta la contraseña se entera de que la cuenta
        // está bloqueada. Quien no la sabe recibe el mismo 401 de siempre y no
        // descubre nada; la dueña del negocio, en cambio, recibe una explicación
        // útil en lugar de un error opaco.
        if (user.LockedUntil is { } lockedUntil && lockedUntil > now)
        {
            return new LoginResult(LoginOutcome.Locked, lockedUntil);
        }

        // Paso 6. Una cuenta desactivada no dice que lo está: 401 genérico.
        if (!user.IsActive)
        {
            return new LoginResult(LoginOutcome.Inactive);
        }

        return new LoginResult(LoginOutcome.Granted);
    }
}
