using Sillar.Core.Authentication;

namespace Sillar.Core.Tests;

/// <summary>Generación y comparación de los secretos de sesión.</summary>
public class SessionTokenTests
{
    [Fact]
    public void Dos_tokens_seguidos_no_se_parecen()
    {
        Assert.NotEqual(SessionTokens.CreateSessionToken(), SessionTokens.CreateSessionToken());
    }

    [Fact]
    public void El_token_de_sesion_lleva_256_bits()
    {
        var token = SessionTokens.CreateSessionToken();

        // 32 bytes en base64url sin relleno son 43 caracteres.
        Assert.Equal(43, token.Length);
    }

    [Fact]
    public void El_token_viaja_en_una_cookie_y_una_cabecera_sin_escapar_nada()
    {
        var token = SessionTokens.CreateSessionToken();

        Assert.DoesNotContain('+', token);
        Assert.DoesNotContain('/', token);
        Assert.DoesNotContain('=', token);
    }

    [Fact]
    public void El_hash_no_contiene_el_token()
    {
        var token = SessionTokens.CreateSessionToken();

        Assert.DoesNotContain(token, SessionTokens.Hash(token));
    }

    [Fact]
    public void El_hash_cabe_en_la_columna()
    {
        Assert.True(SessionTokens.Hash(SessionTokens.CreateSessionToken()).Length <= 255);
    }

    [Fact]
    public void Un_token_coincide_con_su_propio_hash()
    {
        var token = SessionTokens.CreateCsrfToken();

        Assert.True(SessionTokens.Matches(token, SessionTokens.Hash(token)));
    }

    [Fact]
    public void El_token_de_otra_sesion_no_coincide()
    {
        var ajeno = SessionTokens.CreateCsrfToken();

        Assert.False(SessionTokens.Matches(ajeno, SessionTokens.Hash(SessionTokens.CreateCsrfToken())));
    }

    [Theory]
    [InlineData(null, "algo")]
    [InlineData("algo", null)]
    [InlineData("", "algo")]
    [InlineData("algo", "")]
    public void Sin_token_o_sin_hash_no_hay_coincidencia(string? token, string? hash)
    {
        // Importa porque una sesión sin token CSRF guardado no debe aceptar
        // cualquier petición por el hecho de que ambos lados estén vacíos.
        Assert.False(SessionTokens.Matches(token, hash));
    }
}
