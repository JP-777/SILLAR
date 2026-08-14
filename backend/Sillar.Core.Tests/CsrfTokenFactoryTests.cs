using Sillar.Core.Authentication;

namespace Sillar.Core.Tests;

/// <summary>
/// Derivación del token CSRF (ADR-012).
/// </summary>
/// <remarks>
/// Claves fijas escritas aquí, sin base de datos: lo que se comprueba es que la
/// derivación sea una función pura de la instalación y de la sesión. De ahí sale
/// todo lo demás — que <c>/csrf</c> sea idempotente y que un reinicio no rompa
/// las sesiones vivas.
/// </remarks>
public class CsrfTokenFactoryTests
{
    private static readonly Guid Instalacion = new("0192f3a1-4c2b-7d8e-9f01-a2b3c4d5e6f7");
    private static readonly Guid OtraInstalacion = new("0192f3a1-4c2b-7d8e-9f01-a2b3c4d5e6f8");
    private static readonly Guid Sesion = new("019fff01-9306-74f0-9bd7-9047a1cffc84");
    private static readonly Guid OtraSesion = new("019fff01-9306-74f0-9bd7-9047a1cffc85");

    [Fact]
    public void La_misma_sesion_da_siempre_el_mismo_token()
    {
        var factory = new CsrfTokenFactory(Instalacion);

        // Es la razón de ser de todo esto: dos llamadas a /csrf desde dos
        // pestañas devuelven lo mismo y ninguna invalida a la otra.
        Assert.Equal(factory.Create(Sesion), factory.Create(Sesion));
    }

    [Fact]
    public void Reiniciar_el_proceso_no_cambia_el_token_de_una_sesion_viva()
    {
        // Dos instancias distintas del factory representan dos arranques del
        // host. La clave sale de core.installation, no de un valor generado al
        // arrancar, así que sobrevive al reinicio.
        var antes = new CsrfTokenFactory(Instalacion);
        var despues = new CsrfTokenFactory(Instalacion);

        Assert.Equal(antes.Create(Sesion), despues.Create(Sesion));
    }

    [Fact]
    public void Dos_sesiones_distintas_dan_tokens_distintos()
    {
        var factory = new CsrfTokenFactory(Instalacion);

        Assert.NotEqual(factory.Create(Sesion), factory.Create(OtraSesion));
    }

    [Fact]
    public void Dos_instalaciones_distintas_dan_tokens_distintos_para_la_misma_sesion()
    {
        // Restaurar una copia de seguridad en otra instalación no traslada los
        // tokens: cada instalación tiene su propia clave.
        Assert.NotEqual(
            new CsrfTokenFactory(Instalacion).Create(Sesion),
            new CsrfTokenFactory(OtraInstalacion).Create(Sesion));
    }

    [Fact]
    public void Una_clave_de_instalacion_que_solo_difiere_en_un_bit_da_otro_token()
    {
        // Instalacion y OtraInstalacion se diferencian en el último dígito: la
        // derivación no puede arrastrar parecidos entre claves parecidas.
        var uno = new CsrfTokenFactory(Instalacion).Create(Sesion);
        var otro = new CsrfTokenFactory(OtraInstalacion).Create(Sesion);

        Assert.Equal(uno.Length, otro.Length);
        Assert.NotEqual(uno, otro);
    }

    [Fact]
    public void El_token_no_revela_la_sesion_ni_la_clave_de_instalacion()
    {
        var token = new CsrfTokenFactory(Instalacion).Create(Sesion);

        Assert.DoesNotContain(Sesion.ToString(), token);
        Assert.DoesNotContain(Sesion.ToString("N"), token);
        Assert.DoesNotContain(Instalacion.ToString(), token);
        Assert.DoesNotContain(Instalacion.ToString("N"), token);
    }

    [Fact]
    public void El_token_viaja_en_una_cabecera_sin_escapar_nada()
    {
        var token = new CsrfTokenFactory(Instalacion).Create(Sesion);

        Assert.DoesNotContain('+', token);
        Assert.DoesNotContain('/', token);
        Assert.DoesNotContain('=', token);
        // HMAC-SHA256 son 32 bytes; en base64url sin relleno, 43 caracteres.
        Assert.Equal(43, token.Length);
    }

    [Fact]
    public void El_hash_que_se_guarda_no_contiene_el_token()
    {
        // En core.admin_sessions.csrf_token_hash sigue viviendo el SHA-256, igual
        // que antes de la corrección: la tabla no cambió.
        var token = new CsrfTokenFactory(Instalacion).Create(Sesion);

        Assert.DoesNotContain(token, SessionTokens.Hash(token));
        Assert.True(SessionTokens.Matches(token, SessionTokens.Hash(token)));
    }

    [Fact]
    public void La_etiqueta_de_derivacion_esta_versionada()
    {
        // Subirla a v2 es la salida para invalidar todos los tokens sin tocar
        // installation_key, que identifica la instalación.
        Assert.Equal("sillar-csrf-v1", CsrfTokenFactory.DerivationInfo);
    }

    [Fact]
    public void Una_clave_de_instalacion_vacia_se_rechaza()
    {
        // Construirlo antes de leer core.installation es el error que hay que
        // hacer ruidoso: con la base vacía, el Guid llega en blanco.
        Assert.Throws<ArgumentException>(() => new CsrfTokenFactory(Guid.Empty));
    }
}
