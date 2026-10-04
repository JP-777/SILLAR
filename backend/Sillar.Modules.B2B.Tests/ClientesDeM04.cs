using Sillar.Modules.Crm.Contracts;

namespace Sillar.Modules.B2B.Tests;

/// <summary>
/// M04 contestando por contrato: las fichas que le pasas son las activas, y nada más
/// existe.
/// </summary>
/// <remarks>
/// <para>
/// <b>Por qué un doble y no el lector de verdad.</b> <c>CustomerIdentityReader</c> es
/// <c>internal</c> de <c>Sillar.Modules.Crm</c> y M07 solo puede referenciar su
/// <c>Contracts</c> (CLAUDE.md, regla 3 de módulos): usarlo aquí sería cruzar la línea
/// que el módulo tiene prohibida. Que ese lector cumpla el contrato —solo fichas
/// activas, nada de las de baja o bloqueadas, repetidas una sola vez— ya está probado
/// en <c>Sillar.Modules.Crm.Tests/CustomerIdentityReaderTests</c>, que es su sitio.
/// Lo que estas pruebas comprueban es <b>el uso que M07 hace de la respuesta</b>.
/// </para>
/// <para>
/// <b>Imita la parte del contrato que tiene filo:</b> un identificador que no esté en
/// la lista no aparece en el diccionario —no sale una entrada vacía— y
/// <see cref="GetAsync"/> devuelve <c>null</c>. Es la forma en que una ficha de baja
/// llega a la bandeja, y la que hace que el código que la rellenara se vea.
/// </para>
/// </remarks>
internal sealed class ClientesDeM04(params CustomerIdentity[] activos) : ICustomerIdentityReader
{
    /// <summary>Lo que se le preguntó, en orden, para contar consultas.</summary>
    public List<Guid[]> Consultas { get; } = [];

    public Task<CustomerIdentity?> GetAsync(Guid customerId, CancellationToken cancellationToken)
    {
        Consultas.Add([customerId]);
        return Task.FromResult(activos.FirstOrDefault(c => c.CustomerId == customerId));
    }

    public Task<IReadOnlyDictionary<Guid, CustomerIdentity>> GetManyAsync(
        IReadOnlyCollection<Guid> customerIds, CancellationToken cancellationToken)
    {
        Consultas.Add([.. customerIds]);
        return Task.FromResult<IReadOnlyDictionary<Guid, CustomerIdentity>>(
            activos.Where(c => customerIds.Contains(c.CustomerId)).ToDictionary(c => c.CustomerId));
    }
}
