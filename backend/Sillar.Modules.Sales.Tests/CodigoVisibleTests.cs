using Sillar.Modules.Sales.Data;

namespace Sillar.Modules.Sales.Tests;

/// <summary>
/// El formato del código visible del pedido.
/// </summary>
/// <remarks>
/// <b>No tocan la base, y eso es el punto.</b> El formato se decide en una función
/// pura precisamente para que su barrera se pueda provocar en milisegundos: una
/// barrera que solo se puede probar levantando medio sistema es una barrera que
/// nadie va a provocar.
/// </remarks>
public sealed class CodigoVisibleTests
{
    [Fact]
    public void El_codigo_visible_de_un_pedido_tiene_la_forma_P_2026_0147()
    {
        Assert.Equal("P-2026-0147", OrderCode.Componer("P", 2026, 147));
    }

    [Fact]
    public void La_etiqueta_del_nodo_va_delante_del_anio()
    {
        // La regla 2 de la ADR-016 exige la serie del nodo delante, y el orden no
        // es estético: es lo que permite leer de quién es la serie sin conocerla.
        var codigo = OrderCode.Componer("W", 2026, 1);
        Assert.StartsWith("W-", codigo, StringComparison.Ordinal);
        Assert.Equal("W-2026-0001", codigo);
    }

    [Fact]
    public void El_correlativo_se_rellena_hasta_cuatro_digitos()
    {
        Assert.Equal("P-2026-0001", OrderCode.Componer("P", 2026, 1));
        Assert.Equal("P-2026-0099", OrderCode.Componer("P", 2026, 99));
        Assert.Equal("P-2026-9999", OrderCode.Componer("P", 2026, 9999));
    }

    [Fact]
    public void Un_correlativo_de_cinco_cifras_no_se_recorta()
    {
        // Rellenar es para que se lea alineado, no para limitar. Si un año pasa de
        // 9999 pedidos, el código sigue siendo válido: truncarlo produciría dos
        // pedidos distintos con el mismo código, que es el fallo que el UNIQUE
        // descubriría tarde y el cliente descubriría antes.
        Assert.Equal("P-2026-10000", OrderCode.Componer("P", 2026, 10000));
    }

    [Fact]
    public void Sin_etiqueta_de_nodo_no_se_compone_ningun_codigo()
    {
        // La guarda está en la operación que compone, no en quien llama: ponerla en
        // el llamador protegería de ese llamador, y aquí protege de todos.
        foreach (var vacia in new[] { "", " ", "\t" })
        {
            var error = Assert.Throws<ArgumentException>(() => OrderCode.Componer(vacia, 2026, 1));
            Assert.Contains(SalesSettingsKeys.OrderSeriesLabel, error.Message, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void El_mensaje_de_la_etiqueta_ausente_dice_que_no_hay_valor_por_defecto()
    {
        // Es la mitad que importa del mensaje: quien lo lea no debe concluir que
        // basta con inventar una letra. «P» corresponde al nodo «principal», no es
        // universal, y una letra igual en dos nodos produce el mismo código dos
        // veces.
        var error = Assert.Throws<ArgumentException>(() => OrderCode.Componer("", 2026, 1));
        Assert.Contains("no tiene valor por defecto", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Un_correlativo_menor_que_uno_no_se_acepta()
    {
        // El contador entrega desde 1. Un cero significaría que alguien leyó
        // last_number sin incrementarlo.
        foreach (var invalido in new[] { 0, -1 })
        {
            Assert.Throws<ArgumentException>(() => OrderCode.Componer("P", 2026, invalido));
        }
    }

    [Fact]
    public void La_etiqueta_se_recorta_pero_no_se_transforma()
    {
        // Un espacio de más en la configuración no debe producir «P -2026-0001»,
        // pero tampoco se cambia de caja: la etiqueta es la que el nodo declaró.
        Assert.Equal("P-2026-0001", OrderCode.Componer("  P  ", 2026, 1));
        Assert.Equal("p-2026-0001", OrderCode.Componer("p", 2026, 1));
    }

    [Fact]
    public void Dos_anios_distintos_dan_codigos_distintos_con_el_mismo_correlativo()
    {
        // Es lo que hace posible el reinicio anual: el año forma parte del código,
        // así que reiniciar el correlativo no repite ningún código ya dictado.
        Assert.NotEqual(OrderCode.Componer("P", 2026, 1), OrderCode.Componer("P", 2027, 1));
    }

    [Fact]
    public void Dos_nodos_distintos_dan_codigos_distintos_con_la_misma_serie()
    {
        // Y es lo que hace que la continuidad sea de cada serie y no global: dos
        // nodos numerando cada uno desde 1 no colisionan, porque la etiqueta los
        // separa. SUNAT exige serie propia por punto de emisión.
        Assert.NotEqual(OrderCode.Componer("P", 2026, 1), OrderCode.Componer("Q", 2026, 1));
    }
}
