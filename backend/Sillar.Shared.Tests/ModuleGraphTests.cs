using Sillar.Shared.Modularity;
using static Sillar.Shared.Tests.Instalacion;

namespace Sillar.Shared.Tests;

/// <summary>
/// Validación del grafo declarado en el código: el paso 2 del arranque.
/// </summary>
/// <remarks>
/// Lo que falla aquí no depende de la instalación ni de la licencia: es un error
/// de cómo está escrito el producto, y el host aborta antes de tocar la base de
/// datos. Por eso estas pruebas no necesitan entorno.
/// </remarks>
public class ModuleGraphTests
{
    [Fact]
    public void Un_grafo_correcto_es_valido()
    {
        var resultado = ModuleGraph.Validate([
            Core(),
            Modulo("catalog"),
            Modulo("crm", orden: 2),
            Modulo("sales", duras: ["catalog"], blandas: ["crm"], orden: 3)
        ]);

        Assert.True(resultado.IsValid);
        Assert.Empty(resultado.Errors);
    }

    [Fact]
    public void El_orden_deja_cada_modulo_detras_de_sus_dependencias()
    {
        var resultado = ModuleGraph.Validate([
            Modulo("sales", duras: ["catalog"], orden: 3),
            Modulo("catalog", orden: 1),
            Core()
        ]);

        var orden = resultado.InstallationOrder.Select(modulo => modulo.Code).ToList();

        Assert.Equal(["core", "catalog", "sales"], orden);
    }

    [Fact]
    public void El_orden_no_depende_de_como_lleguen_los_modulos()
    {
        IModule[] unos = [Core(), Modulo("catalog"), Modulo("crm", orden: 2), Modulo("sales", duras: ["catalog"], orden: 3)];
        IModule[] otros = [Modulo("sales", duras: ["catalog"], orden: 3), Modulo("crm", orden: 2), Core(), Modulo("catalog")];

        Assert.Equal(
            ModuleGraph.Validate(unos).InstallationOrder.Select(modulo => modulo.Code),
            ModuleGraph.Validate(otros).InstallationOrder.Select(modulo => modulo.Code));
    }

    [Fact]
    public void Sin_ningun_modulo_no_se_puede_arrancar()
    {
        var resultado = ModuleGraph.Validate([]);

        Assert.False(resultado.IsValid);
        Assert.Contains(resultado.Errors, error => error.Contains("ningún módulo"));
    }

    [Fact]
    public void Sin_CORE_no_se_puede_arrancar()
    {
        var resultado = ModuleGraph.Validate([new FakeModule("catalog", hard: ["core"])]);

        Assert.False(resultado.IsValid);
        Assert.Contains(resultado.Errors, error => error.Contains("Falta el módulo 'core'"));
    }

    [Fact]
    public void Una_dependencia_dura_hacia_un_modulo_inexistente_aborta()
    {
        var resultado = ModuleGraph.Validate([Core(), Modulo("tracking", duras: ["services"])]);

        Assert.False(resultado.IsValid);
        Assert.Contains(resultado.Errors, error => error.Contains("'tracking'") && error.Contains("'services'"));
    }

    [Fact]
    public void Un_ciclo_aborta_e_indica_el_camino()
    {
        var resultado = ModuleGraph.Validate([
            Core(),
            Modulo("uno", duras: ["dos"]),
            Modulo("dos", duras: ["uno"])
        ]);

        Assert.False(resultado.IsValid);
        var ciclo = Assert.Single(resultado.Errors, error => error.Contains("Ciclo"));
        Assert.Contains("→", ciclo);
    }

    [Fact]
    public void Un_ciclo_a_traves_de_una_dependencia_blanda_tambien_aborta()
    {
        // Una dependencia blanda no exige que el otro esté instalado, pero sigue
        // siendo una dirección declarada: en círculo no vale.
        var resultado = ModuleGraph.Validate([
            Core(),
            Modulo("uno", blandas: ["dos"]),
            Modulo("dos", duras: ["uno"])
        ]);

        Assert.False(resultado.IsValid);
        Assert.Contains(resultado.Errors, error => error.Contains("Ciclo"));
    }

    [Fact]
    public void Un_modulo_que_no_declara_CORE_aborta()
    {
        var resultado = ModuleGraph.Validate([Core(), new FakeModule("suelto")]);

        Assert.False(resultado.IsValid);
        Assert.Contains(resultado.Errors, error => error.Contains("'suelto'") && error.Contains("'core'"));
    }

    [Fact]
    public void Un_modulo_que_depende_de_si_mismo_aborta()
    {
        var resultado = ModuleGraph.Validate([Core(), Modulo("catalog", duras: ["catalog"])]);

        Assert.False(resultado.IsValid);
        Assert.Contains(resultado.Errors, error => error.Contains("de sí mismo"));
    }

    [Theory]
    [InlineData("Catalog")]      // mayúsculas: no vale como nombre de schema
    [InlineData("1catalog")]     // empieza por dígito
    [InlineData("cata-logo")]    // guion
    [InlineData("c")]            // demasiado corto
    [InlineData("")]
    [InlineData("catalogo_con_un_nombre_larguisimo_que_pasa_de_cuarenta")]
    public void Un_codigo_que_no_sirve_como_schema_aborta(string codigo)
    {
        var resultado = ModuleGraph.Validate([Core(), new FakeModule(codigo, hard: ["core"])]);

        Assert.False(resultado.IsValid);
    }

    [Fact]
    public void Dos_modulos_con_el_mismo_codigo_abortan()
    {
        var resultado = ModuleGraph.Validate([Core(), Modulo("catalog"), Modulo("catalog")]);

        Assert.False(resultado.IsValid);
        Assert.Contains(resultado.Errors, error => error.Contains("Dos módulos declaran el código"));
    }

    [Theory]
    [InlineData("1.0")]
    [InlineData("v1.0.0")]
    [InlineData("")]
    public void Una_version_mal_formada_aborta(string version)
    {
        var resultado = ModuleGraph.Validate([Core(), new FakeModule("catalog", hard: ["core"], version: version)]);

        Assert.False(resultado.IsValid);
        Assert.Contains(resultado.Errors, error => error.Contains("mayor.menor.parche"));
    }

    [Fact]
    public void Un_nombre_visible_vacio_aborta()
    {
        var resultado = ModuleGraph.Validate([Core(), new FakeModule("catalog", hard: ["core"], displayName: "  ")]);

        Assert.False(resultado.IsValid);
        Assert.Contains(resultado.Errors, error => error.Contains("nombre visible"));
    }

    [Fact]
    public void Una_descripcion_que_no_cabe_en_la_columna_aborta()
    {
        var resultado = ModuleGraph.Validate([
            Core(),
            new FakeModule("catalog", hard: ["core"], description: new string('x', 301))
        ]);

        Assert.False(resultado.IsValid);
        Assert.Contains(resultado.Errors, error => error.Contains("descripción"));
    }

    [Fact]
    public void Una_dependencia_blanda_inexistente_solo_avisa()
    {
        // Tolerar la ausencia es justamente lo que significa ser blanda. Aun así
        // se avisa, porque casi siempre es una errata.
        var resultado = ModuleGraph.Validate([Core(), Modulo("sales", blandas: ["crm"])]);

        Assert.True(resultado.IsValid);
        Assert.Contains(resultado.Warnings, aviso => aviso.Contains("'crm'"));
    }

    [Fact]
    public void Declarar_la_misma_dependencia_dura_y_blanda_avisa_y_manda_la_dura()
    {
        var resultado = ModuleGraph.Validate([
            Core(),
            Modulo("catalog"),
            Modulo("sales", duras: ["catalog"], blandas: ["catalog"], orden: 3)
        ]);

        Assert.True(resultado.IsValid);
        Assert.Contains(resultado.Warnings, aviso => aviso.Contains("dura y blanda"));
    }
}
