using Sillar.Core.Authentication;

namespace Sillar.Core.Tests;

/// <summary>Política de contraseñas: longitud por encima de composición.</summary>
public class PasswordPolicyTests
{
    private const string Correo = "persona@ejemplo.pe";
    private const string Nombre = "Nombre Apellido";

    [Fact]
    public void Once_caracteres_se_rechazan()
    {
        var resultado = PasswordPolicy.Check("abcdefghijk", Correo, Nombre);

        Assert.False(resultado.IsValid);
        Assert.Contains("12", resultado.Error);
    }

    [Fact]
    public void Doce_caracteres_se_aceptan()
    {
        Assert.True(PasswordPolicy.Check("wxyzabcdefgh", Correo, Nombre).IsValid);
    }

    [Fact]
    public void No_se_exigen_mayusculas_ni_digitos_ni_simbolos()
    {
        // La recomendación del NIST: exigir composición produce contraseñas
        // peores y anotadas en un papel bajo el teclado.
        Assert.True(PasswordPolicy.Check("mesa lampara ventana", Correo, Nombre).IsValid);
    }

    [Theory]
    [InlineData("contrasena12")]
    [InlineData("CONTRASEÑA12")]
    [InlineData("administrador")]
    [InlineData("123456789012")]
    public void Las_contrasenas_comunes_se_rechazan(string contrasena)
    {
        Assert.False(PasswordPolicy.Check(contrasena, Correo, Nombre).IsValid);
    }

    [Fact]
    public void Una_contrasena_que_contiene_el_correo_se_rechaza()
    {
        Assert.False(PasswordPolicy.Check("persona@ejemplo.pe2026", Correo, Nombre).IsValid);
    }

    [Fact]
    public void Una_contrasena_que_contiene_la_parte_previa_a_la_arroba_se_rechaza()
    {
        Assert.False(PasswordPolicy.Check("persona-mostrador", Correo, Nombre).IsValid);
    }

    [Fact]
    public void Una_contrasena_que_contiene_el_nombre_se_rechaza()
    {
        Assert.False(PasswordPolicy.Check("laveranoapellido", Correo, Nombre).IsValid);
    }

    [Fact]
    public void La_comparacion_con_la_identidad_ignora_mayusculas()
    {
        Assert.False(PasswordPolicy.Check("XxAPELLIDOxx2026", Correo, Nombre).IsValid);
    }

    [Fact]
    public void Las_palabras_cortas_del_nombre_no_invalidan_media_lengua()
    {
        // Con umbral de tres letras, «Ana» rechazaría esta contraseña porque
        // 'ana' está dentro de 'ventana'. Y 'de' o 'la' dejarían fuera casi
        // todo el diccionario.
        var resultado = PasswordPolicy.Check("mesa lampara ventana", "otra@ejemplo.pe", "Ana de la Cruz");

        Assert.True(resultado.IsValid);
    }

    [Fact]
    public void El_apellido_si_se_comprueba_aunque_el_nombre_sea_corto()
    {
        Assert.False(PasswordPolicy.Check("mesa lampara cruzada", "otra@ejemplo.pe", "Ana de la Cruz").IsValid);
    }

    [Fact]
    public void Una_contrasena_vacia_se_rechaza()
    {
        Assert.False(PasswordPolicy.Check(null, Correo, Nombre).IsValid);
        Assert.False(PasswordPolicy.Check("", Correo, Nombre).IsValid);
    }

    [Fact]
    public void Mas_de_setenta_y_dos_caracteres_se_rechaza()
    {
        // BCrypt solo considera los primeros 72 bytes. Aceptar más daría la
        // falsa impresión de que la parte sobrante protege algo.
        Assert.False(PasswordPolicy.Check(new string('x', 73), Correo, Nombre).IsValid);
    }

    [Fact]
    public void El_motivo_del_rechazo_esta_en_espanol()
    {
        var resultado = PasswordPolicy.Check("corta", Correo, Nombre);

        Assert.NotNull(resultado.Error);
        Assert.Contains("contraseña", resultado.Error, StringComparison.OrdinalIgnoreCase);
    }
}
