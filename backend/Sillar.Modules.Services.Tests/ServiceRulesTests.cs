using Sillar.Modules.Services.Domain;
using Sillar.Modules.Services.Services;
namespace Sillar.Modules.Services.Tests;
public sealed class ServiceRulesTests
{
    [Fact] public void Precio_nulo_es_valido_para_consultar() => Assert.Null(ServiceRules.Validate("Anillado", "Cotización", null, null, null, null));
    [Fact] public void Precio_cero_es_valido_y_distinto_de_nulo() => Assert.Null(ServiceRules.Validate("Servicio gratuito", "Sin costo", null, 0m, null, null));
    [Fact] public void Precio_negativo_se_rechaza() => Assert.Contains("negativo", ServiceRules.Validate("Servicio", "Texto", null, -1m, null, null));
    [Fact] public void Publicar_desde_borrador_es_valido() => Assert.True(ServiceRules.CanTransition(PublicationState.Draft, PublicationState.Published));
    [Fact] public void Restaurar_archivado_no_se_inventa() => Assert.False(ServiceRules.CanTransition(PublicationState.Archived, PublicationState.Draft));
    [Fact] public void Slug_quita_tildes_y_normaliza_separadores() => Assert.Equal("impresion-laser-a4", ServiceRules.Slugify("Impresión láser A4"));
    [Fact] public void Imagen_exige_texto_alternativo() => Assert.NotNull(ServiceRules.Validate("Servicio", "Texto", null, null, Guid.NewGuid(), null));
}
