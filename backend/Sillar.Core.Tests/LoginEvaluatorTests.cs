using Sillar.Core.Authentication;
using Sillar.Core.Domain.Values;

namespace Sillar.Core.Tests;

/// <summary>
/// La secuencia de inicio de sesión de la entrega 2 §4.
/// </summary>
/// <remarks>
/// Estas pruebas vigilan un orden, no un cálculo. El código sigue compilando
/// perfectamente si alguien mueve la comprobación del bloqueo delante de la
/// verificación de la contraseña; lo único que cambia es que el sistema empieza
/// a contarle a un desconocido qué cuentas existen y están bloqueadas.
/// </remarks>
public class LoginEvaluatorTests
{
    private static readonly DateTimeOffset Ahora = new(2026, 8, 14, 10, 0, 0, TimeSpan.FromHours(-5));

    [Fact]
    public void Un_correo_que_no_existe_devuelve_401_y_gasta_el_tiempo_del_senuelo()
    {
        var hasher = new FakePasswordHasher();

        var resultado = LoginEvaluator.Evaluate(null, "la que sea", hasher, Ahora);

        Assert.Equal(LoginOutcome.UnknownEmail, resultado.Outcome);
        Assert.Equal(1, hasher.DecoyVerifications);
    }

    [Fact]
    public void El_senuelo_no_se_calcula_cuando_la_cuenta_existe()
    {
        // Ahí ya se paga el coste de una verificación real: calcular además el
        // señuelo duplicaría el tiempo de todos los accesos correctos.
        var hasher = new FakePasswordHasher();

        LoginEvaluator.Evaluate(Cuenta(), "la correcta", hasher, Ahora);

        Assert.Equal(0, hasher.DecoyVerifications);
        Assert.Equal(1, hasher.RealVerifications);
    }

    [Fact]
    public void Una_contrasena_incorrecta_devuelve_401()
    {
        var resultado = LoginEvaluator.Evaluate(Cuenta(), "la mala", new FakePasswordHasher(passwordMatches: false), Ahora);

        Assert.Equal(LoginOutcome.WrongPassword, resultado.Outcome);
    }

    [Fact]
    public void Con_la_contrasena_correcta_y_la_cuenta_bloqueada_devuelve_423()
    {
        var desbloqueo = Ahora.AddMinutes(10);

        var resultado = LoginEvaluator.Evaluate(
            Cuenta(lockedUntil: desbloqueo),
            "la correcta",
            new FakePasswordHasher(),
            Ahora);

        Assert.Equal(LoginOutcome.Locked, resultado.Outcome);
        Assert.Equal(desbloqueo, resultado.LockedUntil);
    }

    [Fact]
    public void Con_la_contrasena_incorrecta_y_la_cuenta_bloqueada_devuelve_401_y_no_423()
    {
        // Es la prueba que fija el orden de los pasos 3 y 5. Si el bloqueo se
        // mirara antes de verificar la contraseña, cualquiera podría saber qué
        // cuentas existen bloqueándolas a propósito y leyendo el 423.
        var resultado = LoginEvaluator.Evaluate(
            Cuenta(lockedUntil: Ahora.AddMinutes(10)),
            "la mala",
            new FakePasswordHasher(passwordMatches: false),
            Ahora);

        Assert.Equal(LoginOutcome.WrongPassword, resultado.Outcome);
        Assert.Null(resultado.LockedUntil);
    }

    [Fact]
    public void Un_bloqueo_ya_vencido_no_impide_entrar()
    {
        var resultado = LoginEvaluator.Evaluate(
            Cuenta(lockedUntil: Ahora.AddMinutes(-1)),
            "la correcta",
            new FakePasswordHasher(),
            Ahora);

        Assert.Equal(LoginOutcome.Granted, resultado.Outcome);
    }

    [Fact]
    public void Una_cuenta_desactivada_devuelve_401_generico()
    {
        var resultado = LoginEvaluator.Evaluate(
            Cuenta(isActive: false),
            "la correcta",
            new FakePasswordHasher(),
            Ahora);

        // Ni 403 ni un mensaje que diga «cuenta desactivada»: quien la
        // desactivó no tiene por qué anunciarlo a quien intenta entrar.
        Assert.Equal(LoginOutcome.Inactive, resultado.Outcome);
    }

    [Fact]
    public void Una_cuenta_desactivada_y_bloqueada_devuelve_423_porque_el_bloqueo_se_mira_antes()
    {
        var resultado = LoginEvaluator.Evaluate(
            Cuenta(isActive: false, lockedUntil: Ahora.AddMinutes(10)),
            "la correcta",
            new FakePasswordHasher(),
            Ahora);

        Assert.Equal(LoginOutcome.Locked, resultado.Outcome);
    }

    [Fact]
    public void Con_todo_en_orden_concede_el_acceso()
    {
        var resultado = LoginEvaluator.Evaluate(Cuenta(), "la correcta", new FakePasswordHasher(), Ahora);

        Assert.Equal(LoginOutcome.Granted, resultado.Outcome);
        Assert.Null(resultado.LockedUntil);
    }

    private static AdminUserCredentials Cuenta(
        bool isActive = true,
        DateTimeOffset? lockedUntil = null,
        int failedLoginCount = 0)
        => new(
            AdminUserId: 1,
            Email: "persona@ejemplo.pe",
            FullName: "Nombre Apellido",
            Role: AdminRole.Admin,
            PasswordHash: "$2a$12$noesunhashreal",
            IsActive: isActive,
            LockedUntil: lockedUntil,
            FailedLoginCount: failedLoginCount);
}
