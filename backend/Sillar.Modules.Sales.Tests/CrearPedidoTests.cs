using Sillar.Core.Contracts;
using Sillar.Modules.Catalog.Contracts;
using Sillar.Modules.Crm.Contracts;
using Sillar.Modules.Sales.Contracts;
using Sillar.Modules.Sales.Data;
using Sillar.Modules.Sales.Dtos;

namespace Sillar.Modules.Sales.Tests;

/// <summary>
/// Dobles de los contratos de M01, M04 y CORE para las pruebas de creación.
/// </summary>
/// <remarks>
/// Están aquí y no en cada archivo porque las usan las pruebas de lógica y las de
/// persistencia: dos copias serían dos sitios donde arreglar el mismo doble.
/// </remarks>
internal static class Dobles
{
    /// <summary>Un catálogo que responde lo que se le diga, variante por variante.</summary>
    internal sealed class CatalogoDice : ICatalogService
    {
        private readonly Dictionary<Guid, (bool Activo, ItemSnapshot? Snapshot)> items = [];

        public int VecesPreguntadoPorVendibilidad { get; private set; }

        public CatalogoDice Con(Guid itemId, bool activo, decimal? precio, string nombre = "Cuaderno A4")
        {
            items[itemId] = (
                activo,
                new ItemSnapshot(itemId, Guid.CreateVersion7(), nombre, "Verde", null, null, precio, "unidad"));
            return this;
        }

        public CatalogoDice SinItem(Guid itemId)
        {
            items[itemId] = (false, null);
            return this;
        }

        public Task<bool> ItemExisteYEstaActivoAsync(Guid itemId, CancellationToken ct)
        {
            VecesPreguntadoPorVendibilidad++;
            return Task.FromResult(items.TryGetValue(itemId, out var v) && v.Activo);
        }

        public Task<ItemSnapshot?> ObtenerItemAsync(Guid itemId, CancellationToken ct)
            => Task.FromResult(items.TryGetValue(itemId, out var v) ? v.Snapshot : null);

        public Task<ItemSnapshot?> BuscarPorCodigoAsync(string codigo, CancellationToken ct)
            => throw new InvalidOperationException("M03 no busca por código: eso es la caja.");

        public Task<IReadOnlyList<ItemSnapshot>> BuscarAsync(string texto, int limite, CancellationToken ct)
            => throw new InvalidOperationException("M03 no busca texto libre al crear un pedido.");

        public Task<IReadOnlyList<ItemSnapshot>> VariantesDeAsync(Guid productId, CancellationToken ct)
            => throw new InvalidOperationException("M03 no elige variantes al crear un pedido.");

        public Task<IReadOnlyList<ProductPickerItem>> BuscarParaSeleccionAsync(string texto, int limite, CancellationToken ct)
            => throw new InvalidOperationException("Eso es para elegir productos en un panel.");

        public Task<ProductPickerItem?> ObtenerParaSeleccionAsync(Guid productId, CancellationToken ct)
            => throw new InvalidOperationException("Eso es para releer una selección.");
    }

    /// <summary>Un M04 que devuelve el cliente que se le diga.</summary>
    internal sealed class M04Dice(CustomerOrderContactSnapshot? respuesta) : ICustomerSnapshotReader
    {
        public Task<CustomerOrderContactSnapshot?> GetForOrderAsync(Guid customerId, CancellationToken ct)
            => Task.FromResult(respuesta);

        public Task<CustomerOrderSnapshot?> GetForOrderAsync(Guid customerId, Guid customerAddressId, CancellationToken ct)
            => throw new InvalidOperationException("M03 es solo recojo: no pide la variante con dirección.");
    }

    /// <summary>Ajustes mínimos: etiqueta de serie y plazo.</summary>
    internal sealed class AjustesDice(string? etiqueta = "P", int horas = 48) : ISettingsReader
    {
        public string? Get(string key)
            => key == SalesSettingsKeys.OrderSeriesLabel ? etiqueta : null;

        public T? Get<T>(string key)
            => key == SalesSettingsKeys.PaymentDueHours && horas is T h ? h : default;

        public IReadOnlyDictionary<string, string> GetPublic() => new Dictionary<string, string>();
    }

    internal sealed class RelojFijo(DateTimeOffset ahora) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => ahora;
    }

    internal static CustomerOrderContactSnapshot Cliente(Guid id, bool verificado = true)
        => new(id, "Ana Quispe", "ana@ejemplo.pe", "987654321", "dni", "12345678", verificado);
}

/// <summary>
/// Lo que la petición de crear un pedido <b>no</b> puede llevar.
/// </summary>
/// <remarks>
/// R-09 dice que un precio o un total recibido del navegador es dato no confiable. La
/// forma fuerte de cumplirlo no es validarlo: es <b>no tener dónde ponerlo</b>. Estas
/// pruebas vigilan esa forma, porque un campo añadido «para la pantalla» la
/// desharía sin que nadie lo note.
/// </remarks>
public sealed class PeticionDePedidoTests
{
    [Fact]
    public void Una_linea_pedida_solo_lleva_variante_y_cantidad()
    {
        Assert.Equal(
            ["ItemId", "Quantity"],
            typeof(LineaPedida).GetProperties().Select(p => p.Name));
    }

    [Fact]
    public void La_peticion_no_tiene_donde_poner_un_precio_ni_un_total()
    {
        var campos = typeof(LineaPedida).GetProperties()
            .Concat(typeof(CrearPedidoPeticion).GetProperties())
            .Select(p => p.Name)
            .ToArray();

        foreach (var prohibido in new[] { "Price", "Precio", "Total", "Amount", "Importe" })
        {
            Assert.DoesNotContain(campos, c => c.Contains(prohibido, StringComparison.OrdinalIgnoreCase));
        }
    }

    [Fact]
    public void La_peticion_no_lleva_cliente_ni_direccion()
    {
        // El cliente sale de la sesión; y no hay dirección porque es solo recojo.
        var campos = typeof(CrearPedidoPeticion).GetProperties().Select(p => p.Name).ToArray();

        Assert.Equal(["Lines"], campos);
    }

    [Fact]
    public void Los_motivos_de_no_creacion_distinguen_a_consultar_de_no_vendible()
    {
        // Son dos frases distintas en pantalla y dos cosas distintas: una se cotiza,
        // la otra se quita del carrito. Fundirlas obligaría al cliente a adivinar.
        var nombres = Enum.GetNames<Sillar.Modules.Sales.Pedidos.MotivoDeNoCreacion>();

        Assert.Contains("ItemAConsultar", nombres);
        Assert.Contains("ItemNoVendible", nombres);
    }
}
