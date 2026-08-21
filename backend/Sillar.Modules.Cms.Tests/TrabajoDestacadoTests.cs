using Sillar.Modules.Cms.Domain;

namespace Sillar.Modules.Cms.Tests;

public sealed class TrabajoDestacadoTests
{
    [Fact]
    public void Sin_imagen_el_trabajo_esta_incompleto()
    {
        var project = new FeaturedProject
        {
            Title = "Trabajo sin imagen",
            ImageId = null,
            AltText = null
        };

        Assert.False(FeaturedProjectRules.IsComplete(project, null));
    }

    [Fact]
    public void Con_medio_inactivo_el_trabajo_esta_incompleto()
    {
        var project = new FeaturedProject
        {
            Title = "Trabajo con medio dado de baja",
            ImageId = Guid.NewGuid(),
            AltText = "Descripción del trabajo"
        };

        Assert.False(FeaturedProjectRules.IsComplete(project, null));
    }

    [Fact]
    public void Con_imagen_activa_y_texto_alternativo_el_trabajo_esta_completo()
    {
        var project = new FeaturedProject
        {
            Title = "Trabajo completo",
            ImageId = Guid.NewGuid(),
            AltText = "Descripción del trabajo"
        };

        Assert.True(FeaturedProjectRules.IsComplete(project, "/media/2026/08/imagen.webp"));
    }
}
