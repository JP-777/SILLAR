using Sillar.Core.Domain.Values;
using Sillar.Core.Settings;

namespace Sillar.Core.Tests;

/// <summary>Validación del valor contra el tipo declarado de la clave.</summary>
public class SettingValueValidatorTests
{
    [Theory]
    [InlineData(SettingValueType.Text, "Cualquier cosa")]
    [InlineData(SettingValueType.Number, "42")]
    [InlineData(SettingValueType.Number, "-3.5")]
    [InlineData(SettingValueType.Boolean, "true")]
    [InlineData(SettingValueType.Boolean, "SI")]
    [InlineData(SettingValueType.Boolean, "0")]
    [InlineData(SettingValueType.Url, "https://ejemplo.pe/local")]
    [InlineData(SettingValueType.Email, "contacto@ejemplo.pe")]
    [InlineData(SettingValueType.Json, "{\"horario\":\"9-18\"}")]
    public void Un_valor_que_encaja_con_su_tipo_se_acepta(string tipo, string valor)
    {
        Assert.Null(SettingValueValidator.Validate(tipo, valor));
    }

    [Theory]
    [InlineData(SettingValueType.Number, "muchos")]
    [InlineData(SettingValueType.Boolean, "quizás")]
    [InlineData(SettingValueType.Url, "ejemplo.pe")]
    [InlineData(SettingValueType.Url, "ftp://ejemplo.pe")]
    [InlineData(SettingValueType.Email, "arroba-no-hay")]
    [InlineData(SettingValueType.Json, "{esto no es json}")]
    public void Un_valor_que_no_encaja_se_rechaza(string tipo, string valor)
    {
        Assert.NotNull(SettingValueValidator.Validate(tipo, valor));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Un_valor_vacio_se_rechaza_sea_cual_sea_el_tipo(string? valor)
    {
        // Para retirar una configuración está is_active, no dejarla en blanco.
        Assert.NotNull(SettingValueValidator.Validate(SettingValueType.Text, valor));
    }

    [Fact]
    public void El_numero_se_interpreta_con_punto_decimal_no_con_coma()
    {
        // El valor se guarda como texto y no puede depender de la configuración
        // regional de la máquina que lo lea.
        Assert.Null(SettingValueValidator.Validate(SettingValueType.Number, "3.5"));
    }

    [Fact]
    public void El_motivo_del_rechazo_dice_que_se_esperaba()
    {
        var motivo = SettingValueValidator.Validate(SettingValueType.Number, "muchos");

        Assert.Contains("número", motivo);
    }

    [Theory]
    [InlineData("true", true)]
    [InlineData("SÍ", true)]
    [InlineData("1", true)]
    [InlineData("false", false)]
    [InlineData("no", false)]
    [InlineData("0", false)]
    public void Los_booleanos_reconocibles_se_interpretan(string valor, bool esperado)
    {
        Assert.Equal(esperado, SettingValueValidator.AsBoolean(valor));
    }
}
