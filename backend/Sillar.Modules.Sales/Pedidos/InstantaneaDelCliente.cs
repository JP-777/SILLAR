using Sillar.Modules.Crm.Contracts;
using Sillar.Modules.Sales.Domain;

namespace Sillar.Modules.Sales.Pedidos;

/// <summary>
/// Por qué un cliente no puede comprar ahora mismo.
/// </summary>
/// <remarks>
/// <b>Deliberadamente pobre, y es la decisión.</b> El contrato de M04 devuelve
/// <c>null</c> <b>sin distinguir el motivo</b> —ficha inexistente, de baja, bloqueada
/// o sin cuenta— y M03 no lo adivina. Solo hay dos casos porque solo hay dos cosas
/// que M03 sabe de verdad: que la cuenta no puede comprar, o que su correo no está
/// verificado.
/// </remarks>
public enum MotivoDeRechazo
{
    /// <summary>Ninguno: el cliente puede comprar.</summary>
    Ninguno = 0,

    /// <summary>
    /// M04 devolvió <c>null</c>. <b>No se sabe por qué, y no se inventa.</b>
    /// </summary>
    LaCuentaNoPuedeComprar,

    /// <summary>El correo no está verificado. R-07.</summary>
    CorreoSinVerificar
}

/// <summary>
/// El resultado de intentar congelar al cliente en un pedido.
/// </summary>
/// <param name="Instantanea">Los datos congelados, o <c>null</c> si se rechazó.</param>
/// <param name="Motivo">Por qué se rechazó.</param>
public sealed record ResultadoDeInstantanea(
    InstantaneaDelCliente? Instantanea,
    MotivoDeRechazo Motivo)
{
    /// <summary>Si se puede seguir adelante con el pedido.</summary>
    public bool Permitido => Instantanea is not null;
}

/// <summary>
/// Los datos del cliente tal como el pedido los conserva: <b>una copia, no una
/// referencia</b>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Sin dirección.</b> SILLAR WEB v1 es solo recojo en tienda, así que no hay a
/// dónde enviar que congelar. Por eso se consume la sobrecarga de
/// <see cref="ICustomerSnapshotReader"/> <b>que no pide dirección</b>: un cliente sin
/// ninguna dirección guardada compra igual.
/// </para>
/// <para>
/// <b>Es una foto.</b> Editar después el perfil del cliente no altera un pedido ya
/// hecho, y una lectura nueva devolvería el estado de hoy, que ya no es lo que
/// ocurrió. Por eso M03 guarda su propia copia en <c>sales.orders</c>.
/// </para>
/// </remarks>
public sealed record InstantaneaDelCliente(
    Guid CustomerId,
    string FullName,
    string Email,
    string? Phone,
    string? DocumentType,
    string? DocumentNumber)
{
    /// <summary>
    /// Copia los datos congelados sobre un pedido.
    /// </summary>
    /// <remarks>
    /// Está aquí y no en quien llama para que no haya dos sitios donde recordar los
    /// seis campos. Un campo que se olvide al copiar deja un pedido con un hueco que
    /// ya no se puede rellenar: el dato de entonces se perdió.
    /// </remarks>
    public void Congelar(Order pedido)
    {
        pedido.CustomerId = CustomerId;
        pedido.CustomerFullName = FullName;
        pedido.CustomerEmail = Email;
        pedido.CustomerPhone = Phone;
        pedido.CustomerDocumentType = DocumentType;
        pedido.CustomerDocumentNumber = DocumentNumber;
    }
}
