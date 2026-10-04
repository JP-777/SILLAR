using Sillar.Modules.Crm.Contracts;
using Sillar.Modules.Sales.Domain;
using Sillar.Modules.Sales.Pedidos;

namespace Sillar.Modules.Sales.Tests;

/// <summary>
/// D1 — congelar al cliente en el pedido.
/// </summary>
/// <remarks>
/// Con un doble del contrato de M04, no con su implementación: lo que se comprueba
/// aquí es la <b>decisión de M03</b> —quién puede comprar y qué se congela—, no que
/// M04 traduzca bien a SQL. Eso es suyo y está acreditado en su propia corrida.
/// </remarks>
public sealed class CongelarClienteTests
{
    private static readonly Guid Cliente = Guid.CreateVersion7();

    private static CustomerOrderContactSnapshot Snapshot(bool verificado = true) => new(
        Cliente, "Ana Quispe", "ana@ejemplo.pe", "987654321", "dni", "12345678", verificado);

    /// <summary>Doble del contrato: devuelve lo que se le diga.</summary>
    private sealed class M04Dice(CustomerOrderContactSnapshot? respuesta) : ICustomerSnapshotReader
    {
        public int VecesLlamadoSinDireccion { get; private set; }

        public Task<CustomerOrderContactSnapshot?> GetForOrderAsync(
            Guid customerId, CancellationToken cancellationToken)
        {
            VecesLlamadoSinDireccion++;
            return Task.FromResult(respuesta);
        }

        public Task<CustomerOrderSnapshot?> GetForOrderAsync(
            Guid customerId, Guid customerAddressId, CancellationToken cancellationToken)
            => throw new InvalidOperationException(
                "M03 es solo recojo en tienda: no debe pedir la variante con dirección.");
    }

    [Fact]
    public async Task Un_cliente_verificado_se_congela_con_sus_seis_datos()
    {
        var r = await new CongeladorDeCliente(new M04Dice(Snapshot()))
            .ObtenerParaRecojoAsync(Cliente, TestContext.Current.CancellationToken);

        Assert.True(r.Permitido);
        Assert.Equal(MotivoDeRechazo.Ninguno, r.Motivo);

        var i = r.Instantanea!;
        Assert.Equal(Cliente, i.CustomerId);
        Assert.Equal("Ana Quispe", i.FullName);
        Assert.Equal("ana@ejemplo.pe", i.Email);
        Assert.Equal("987654321", i.Phone);
        Assert.Equal("dni", i.DocumentType);
        Assert.Equal("12345678", i.DocumentNumber);
    }

    [Fact]
    public async Task Se_usa_la_sobrecarga_SIN_direccion()
    {
        // Solo recojo en tienda: pedir la variante con dirección haría que un cliente
        // sin dirección guardada no pudiera comprar, que era la carencia que el
        // contrato 1.1.0 vino a resolver. El doble revienta si se pide la otra.
        var m04 = new M04Dice(Snapshot());

        await new CongeladorDeCliente(m04)
            .ObtenerParaRecojoAsync(Cliente, TestContext.Current.CancellationToken);

        Assert.Equal(1, m04.VecesLlamadoSinDireccion);
    }

    [Fact]
    public async Task Un_correo_sin_verificar_no_compra()
    {
        // R-07: sin verificar se puede entrar y mirar; comprar, no.
        var r = await new CongeladorDeCliente(new M04Dice(Snapshot(verificado: false)))
            .ObtenerParaRecojoAsync(Cliente, TestContext.Current.CancellationToken);

        Assert.False(r.Permitido);
        Assert.Equal(MotivoDeRechazo.CorreoSinVerificar, r.Motivo);
        Assert.Null(r.Instantanea);
    }

    [Fact]
    public async Task Si_M04_devuelve_null_no_se_inventa_la_causa()
    {
        // M04 no distingue si la ficha no existe, está de baja, está bloqueada o no
        // tiene cuenta. M03 tiene UN motivo para los cuatro, a propósito.
        var r = await new CongeladorDeCliente(new M04Dice(null))
            .ObtenerParaRecojoAsync(Cliente, TestContext.Current.CancellationToken);

        Assert.False(r.Permitido);
        Assert.Equal(MotivoDeRechazo.LaCuentaNoPuedeComprar, r.Motivo);
    }

    [Fact]
    public void Los_motivos_de_rechazo_no_nombran_ninguna_causa_que_M04_no_garantice()
    {
        // La barrera de lenguaje: si alguien añade «FichaDeBaja» o «CuentaBloqueada»,
        // esta prueba se pone roja. M04 no entrega esa información.
        var nombres = Enum.GetNames<MotivoDeRechazo>();

        Assert.Equal(["Ninguno", "LaCuentaNoPuedeComprar", "CorreoSinVerificar"], nombres);
    }

    [Fact]
    public async Task Congelar_copia_los_seis_campos_al_pedido()
    {
        var r = await new CongeladorDeCliente(new M04Dice(Snapshot()))
            .ObtenerParaRecojoAsync(Cliente, TestContext.Current.CancellationToken);

        var pedido = new Order
        {
            OrderCode = "P-2026-0001",
            CustomerFullName = "",
            CustomerEmail = "",
            Status = "pending_payment"
        };

        r.Instantanea!.Congelar(pedido);

        Assert.Equal(Cliente, pedido.CustomerId);
        Assert.Equal("Ana Quispe", pedido.CustomerFullName);
        Assert.Equal("ana@ejemplo.pe", pedido.CustomerEmail);
        Assert.Equal("987654321", pedido.CustomerPhone);
        Assert.Equal("dni", pedido.CustomerDocumentType);
        Assert.Equal("12345678", pedido.CustomerDocumentNumber);
    }

    [Fact]
    public async Task El_pedido_no_recibe_ninguna_direccion()
    {
        // Solo recojo: Order no tiene columna de dirección y la instantánea tampoco
        // la trae. Si alguien añadiera una, esta prueba lo dice.
        var r = await new CongeladorDeCliente(new M04Dice(Snapshot()))
            .ObtenerParaRecojoAsync(Cliente, TestContext.Current.CancellationToken);

        var campos = r.Instantanea!.GetType().GetProperties().Select(p => p.Name).ToArray();
        Assert.DoesNotContain(campos, c => c.Contains("Address", StringComparison.Ordinal));

        var delPedido = typeof(Order).GetProperties().Select(p => p.Name).ToArray();
        Assert.DoesNotContain(delPedido, c => c.Contains("Address", StringComparison.Ordinal));
    }
}
