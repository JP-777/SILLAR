using Sillar.Modules.Catalog.Services;

namespace Sillar.Modules.Catalog.Tests;

/// <summary>Detección de ciclos al reasignar el padre de una categoría (SPEC regla 10).</summary>
public class CategoryTreeTests
{
    private static readonly Guid Ropa = Guid.NewGuid();
    private static readonly Guid Deportiva = Guid.NewGuid();
    private static readonly Guid Zapatillas = Guid.NewGuid();

    [Fact]
    public void Asignar_una_raiz_nueva_no_es_ciclo()
    {
        var padres = new Dictionary<Guid, Guid?> { [Ropa] = null };

        Assert.False(CategoryTree.CreatesCycle(padres, Ropa, null));
    }

    [Fact]
    public void Asignar_un_padre_sin_relacion_no_es_ciclo()
    {
        var padres = new Dictionary<Guid, Guid?> { [Ropa] = null, [Deportiva] = null };

        Assert.False(CategoryTree.CreatesCycle(padres, Deportiva, Ropa));
    }

    [Fact]
    public void Ser_padre_de_si_misma_es_ciclo()
    {
        var padres = new Dictionary<Guid, Guid?> { [Ropa] = null };

        Assert.True(CategoryTree.CreatesCycle(padres, Ropa, Ropa));
    }

    [Fact]
    public void Un_ciclo_largo_tambien_se_detecta()
    {
        // Ropa → Deportiva → Zapatillas. Convertir a Ropa en hija de Zapatillas
        // cerraría el círculo, aunque no son padre e hija directas.
        var padres = new Dictionary<Guid, Guid?>
        {
            [Ropa] = null,
            [Deportiva] = Ropa,
            [Zapatillas] = Deportiva
        };

        Assert.True(CategoryTree.CreatesCycle(padres, Ropa, Zapatillas));
    }

    [Fact]
    public void Mover_una_hoja_a_otra_rama_no_es_ciclo()
    {
        var padres = new Dictionary<Guid, Guid?>
        {
            [Ropa] = null,
            [Deportiva] = null,
            [Zapatillas] = Ropa
        };

        Assert.False(CategoryTree.CreatesCycle(padres, Zapatillas, Deportiva));
    }
}
