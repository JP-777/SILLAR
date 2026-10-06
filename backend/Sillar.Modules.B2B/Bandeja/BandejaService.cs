using Microsoft.EntityFrameworkCore;
using Sillar.Modules.B2B.Data;
using Sillar.Modules.B2B.Domain;
using Sillar.Modules.Catalog.Contracts;
using Sillar.Modules.Crm.Contracts;

namespace Sillar.Modules.B2B.Bandeja;

/// <summary>Qué pasó con una operación del panel.</summary>
public enum ResultadoBandeja { Hecho, NoEncontrada, Invalida, Conflicto }

/// <summary>Resultado, con la frase para la persona y el nombre humano de la fila para la auditoría.</summary>
public sealed record OperacionDeBandeja<T>(ResultadoBandeja Resultado, T? Valor = default, string? Motivo = null, string? Nombre = null);

/// <summary>La bandeja del personal: las dos clases de solicitud y las cotizaciones, para leer.</summary>
/// <remarks>
/// <para>
/// <b>La identidad del cliente se pide a M04, no se guarda.</b> M07 conserva el
/// <c>customer_id</c> en sus tablas —es su columna y su clave foránea— y lo cambia por
/// una persona al leer, con <see cref="ICustomerIdentityReader"/>. No hay snapshot del
/// nombre: una ficha corregida se ve corregida en la bandeja, que es lo que una
/// bandeja tiene que hacer.
/// </para>
/// <para>
/// <b>Se inyecta directo porque <c>crm</c> es dependencia dura de M07</b>
/// (<c>B2BModule.cs:57</c>). Si fuera blanda habría que pedirla al contenedor y
/// degradar; aquí, si no está, el módulo no arranca, y eso ya lo decide la plataforma.
/// </para>
/// </remarks>
public sealed class BandejaService(B2bDbContext db, ICatalogService catalogo, ICustomerIdentityReader clientes)
{
    /// <summary>
    /// La identidad de un cliente, o <c>null</c> si M04 no la da.
    /// </summary>
    /// <remarks>
    /// Los que no existen o no están activos no aparecen en el diccionario, así que
    /// esto es un <c>TryGetValue</c> y no un indexador: un <c>KeyNotFoundException</c>
    /// tumbaría el listado entero por una ficha de baja.
    /// </remarks>
    private static ClienteDeLaBandeja? De(IReadOnlyDictionary<Guid, CustomerIdentity> identidades, Guid cliente)
        => identidades.TryGetValue(cliente, out var i) ? new ClienteDeLaBandeja(i.FullName, i.Email, i.Phone) : null;

    private static ClienteDeLaBandeja? De(CustomerIdentity? i)
        => i is null ? null : new ClienteDeLaBandeja(i.FullName, i.Email, i.Phone);

    /// <summary>
    /// Las identidades de las filas de un listado, en <b>una sola</b> consulta.
    /// </summary>
    /// <remarks>
    /// <c>Distinct()</c> antes de preguntar: un cliente con ocho solicitudes se lee una
    /// vez. Sin él, una bandeja de cincuenta filas haría cincuenta lecturas para
    /// enseñar los mismos cinco nombres.
    /// </remarks>
    private Task<IReadOnlyDictionary<Guid, CustomerIdentity>> IdentidadesAsync(IEnumerable<Guid> ids, CancellationToken ct)
        => clientes.GetManyAsync([.. ids.Distinct()], ct);

    // --- Personalización ----------------------------------------------------

    public async Task<IReadOnlyList<PersonalizacionEnBandeja>> ListarPersonalizacionesAsync(string? estado, CancellationToken ct)
    {
        var filas = await db.SpecialOrderLeads.AsNoTracking()
            .Where(x => estado == null || x.Status == estado)
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new { x.Id, x.CustomerId, x.ProductId, x.ProductName, x.ProductSlug, x.PendingRelink,
                x.Description, x.Quantity, x.NeededBy, x.Status, x.IsActive, x.CreatedAt })
            .ToListAsync(ct);

        var identidades = await IdentidadesAsync(filas.Select(f => f.CustomerId), ct);

        return [.. filas.Select(f => new PersonalizacionEnBandeja(f.Id, De(identidades, f.CustomerId), f.ProductId,
            f.ProductName, f.ProductSlug, f.PendingRelink, f.Description, f.Quantity, f.NeededBy, f.Status,
            f.IsActive, f.CreatedAt))];
    }

    public async Task<PersonalizacionDetalle?> ObtenerPersonalizacionAsync(int id, CancellationToken ct)
    {
        var x = await db.SpecialOrderLeads.AsNoTracking().FirstOrDefaultAsync(f => f.Id == id, ct);
        if (x is null) return null;

        return new PersonalizacionDetalle(
            new PersonalizacionEnBandeja(x.Id, De(await clientes.GetAsync(x.CustomerId, ct)), x.ProductId,
                x.ProductName, x.ProductSlug, x.PendingRelink, x.Description, x.Quantity, x.NeededBy, x.Status,
                x.IsActive, x.CreatedAt),
            x.StaffNotes);
    }

    public async Task<OperacionDeBandeja<PersonalizacionDetalle>> CambiarEstadoPersonalizacionAsync(int id, string? estado, CancellationToken ct)
    {
        var fila = await db.SpecialOrderLeads.FirstOrDefaultAsync(x => x.Id == id && x.IsActive, ct);
        if (fila is null) return new(ResultadoBandeja.NoEncontrada);
        if (Estado(fila.Status, estado) is { } rechazo) return new(rechazo.Resultado, Motivo: rechazo.Motivo);
        fila.Status = estado!;
        await db.SaveChangesAsync(ct);
        return new(ResultadoBandeja.Hecho, await ObtenerPersonalizacionAsync(id, ct), Nombre: NombrePersonalizacion(fila));
    }

    public async Task<OperacionDeBandeja<PersonalizacionDetalle>> NotasPersonalizacionAsync(int id, string? notas, CancellationToken ct)
    {
        var fila = await db.SpecialOrderLeads.FirstOrDefaultAsync(x => x.Id == id && x.IsActive, ct);
        if (fila is null) return new(ResultadoBandeja.NoEncontrada);
        fila.StaffNotes = string.IsNullOrWhiteSpace(notas) ? null : notas.Trim();
        await db.SaveChangesAsync(ct);
        return new(ResultadoBandeja.Hecho, await ObtenerPersonalizacionAsync(id, ct), Nombre: NombrePersonalizacion(fila));
    }

    /// <summary>
    /// Ata la solicitud a otro producto <b>activo</b> y refresca su instantánea.
    /// Es lo que resuelve <c>pending_relink</c>.
    /// </summary>
    public async Task<OperacionDeBandeja<PersonalizacionDetalle>> ReenlazarAsync(int id, Guid productoId, CancellationToken ct)
    {
        var fila = await db.SpecialOrderLeads.FirstOrDefaultAsync(x => x.Id == id && x.IsActive, ct);
        if (fila is null) return new(ResultadoBandeja.NoEncontrada);
        var producto = await catalogo.ObtenerParaSeleccionAsync(productoId, ct);
        if (producto is null || !producto.IsActive)
            return new(ResultadoBandeja.Conflicto, Motivo: "Ese producto no está activo en el catálogo. Elige uno que lo esté.");
        fila.ProductId = producto.ProductId;
        fila.ProductName = producto.Name;
        fila.ProductSlug = producto.Slug;
        fila.PendingRelink = false;
        await db.SaveChangesAsync(ct);
        return new(ResultadoBandeja.Hecho, await ObtenerPersonalizacionAsync(id, ct), Nombre: NombrePersonalizacion(fila));
    }

    public async Task<OperacionDeBandeja<PersonalizacionDetalle>> BajaPersonalizacionAsync(int id, CancellationToken ct)
    {
        var fila = await db.SpecialOrderLeads.FirstOrDefaultAsync(x => x.Id == id && x.IsActive, ct);
        if (fila is null) return new(ResultadoBandeja.NoEncontrada);
        fila.IsActive = false;
        await db.SaveChangesAsync(ct);
        return new(ResultadoBandeja.Hecho, await ObtenerPersonalizacionAsync(id, ct), Nombre: NombrePersonalizacion(fila));
    }

    // --- Volumen ------------------------------------------------------------

    public async Task<IReadOnlyList<VolumenEnBandeja>> ListarVolumenAsync(string? estado, CancellationToken ct)
    {
        var filas = await db.InstitutionRequests.AsNoTracking()
            .Where(x => estado == null || x.Status == estado)
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new { x.Id, x.CustomerId, x.InstitutionName, x.InstitutionDocument, x.ContactPerson,
                x.Description, x.Quantity, x.EventDate, x.Status, x.IsActive, x.CreatedAt })
            .ToListAsync(ct);

        var identidades = await IdentidadesAsync(filas.Select(f => f.CustomerId), ct);

        return [.. filas.Select(f => new VolumenEnBandeja(f.Id, De(identidades, f.CustomerId), f.InstitutionName,
            f.InstitutionDocument, f.ContactPerson, f.Description, f.Quantity, f.EventDate, f.Status, f.IsActive,
            f.CreatedAt))];
    }

    public async Task<VolumenDetalle?> ObtenerVolumenAsync(int id, CancellationToken ct)
    {
        var x = await db.InstitutionRequests.AsNoTracking().FirstOrDefaultAsync(f => f.Id == id, ct);
        if (x is null) return null;

        return new VolumenDetalle(
            new VolumenEnBandeja(x.Id, De(await clientes.GetAsync(x.CustomerId, ct)), x.InstitutionName,
                x.InstitutionDocument, x.ContactPerson, x.Description, x.Quantity, x.EventDate, x.Status,
                x.IsActive, x.CreatedAt),
            x.StaffNotes);
    }

    public async Task<OperacionDeBandeja<VolumenDetalle>> CambiarEstadoVolumenAsync(int id, string? estado, CancellationToken ct)
    {
        var fila = await db.InstitutionRequests.FirstOrDefaultAsync(x => x.Id == id && x.IsActive, ct);
        if (fila is null) return new(ResultadoBandeja.NoEncontrada);
        if (Estado(fila.Status, estado) is { } rechazo) return new(rechazo.Resultado, Motivo: rechazo.Motivo);
        fila.Status = estado!;
        await db.SaveChangesAsync(ct);
        return new(ResultadoBandeja.Hecho, await ObtenerVolumenAsync(id, ct), Nombre: NombreVolumen(fila));
    }

    public async Task<OperacionDeBandeja<VolumenDetalle>> NotasVolumenAsync(int id, string? notas, CancellationToken ct)
    {
        var fila = await db.InstitutionRequests.FirstOrDefaultAsync(x => x.Id == id && x.IsActive, ct);
        if (fila is null) return new(ResultadoBandeja.NoEncontrada);
        fila.StaffNotes = string.IsNullOrWhiteSpace(notas) ? null : notas.Trim();
        await db.SaveChangesAsync(ct);
        return new(ResultadoBandeja.Hecho, await ObtenerVolumenAsync(id, ct), Nombre: NombreVolumen(fila));
    }

    public async Task<OperacionDeBandeja<VolumenDetalle>> BajaVolumenAsync(int id, CancellationToken ct)
    {
        var fila = await db.InstitutionRequests.FirstOrDefaultAsync(x => x.Id == id && x.IsActive, ct);
        if (fila is null) return new(ResultadoBandeja.NoEncontrada);
        fila.IsActive = false;
        await db.SaveChangesAsync(ct);
        return new(ResultadoBandeja.Hecho, await ObtenerVolumenAsync(id, ct), Nombre: NombreVolumen(fila));
    }

    // --- Cotizaciones, solo lectura en este tramo ---------------------------

    public async Task<IReadOnlyList<CotizacionEnBandeja>> ListarCotizacionesAsync(string? estado, CancellationToken ct)
    {
        var filas = await db.Quotes.AsNoTracking()
            .Where(x => estado == null || x.Status == estado)
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new { x.Id, x.QuoteNumber, x.CustomerId, x.SpecialOrderLeadId, x.InstitutionRequestId,
                x.TotalAmount, x.Status, x.InvalidatedAt, x.InvalidatedReason, x.IsActive, x.CreatedAt })
            .ToListAsync(ct);

        var identidades = await IdentidadesAsync(filas.Select(f => f.CustomerId), ct);

        return [.. filas.Select(f => new CotizacionEnBandeja(f.Id, f.QuoteNumber, De(identidades, f.CustomerId),
            f.SpecialOrderLeadId, f.InstitutionRequestId, f.TotalAmount, f.Status, f.InvalidatedAt,
            f.InvalidatedReason, f.IsActive, f.CreatedAt))];
    }

    public async Task<CotizacionDetalle?> ObtenerCotizacionAsync(int id, CancellationToken ct)
    {
        var x = await db.Quotes.AsNoTracking().Include(q => q.Lines).FirstOrDefaultAsync(q => q.Id == id, ct);
        return x is null ? null : new CotizacionDetalle(
            new CotizacionEnBandeja(x.Id, x.QuoteNumber, De(await clientes.GetAsync(x.CustomerId, ct)),
                x.SpecialOrderLeadId, x.InstitutionRequestId,
                x.TotalAmount, x.Status, x.InvalidatedAt, x.InvalidatedReason, x.IsActive, x.CreatedAt),
            [.. x.Lines.OrderBy(l => l.SortOrder).Select(l => new LineaDeCotizacionAdmin(l.ItemId, l.ProductName,
                l.VariantValue, l.SaleUnit, l.Description, l.Quantity, l.UnitPrice, l.CatalogPriceAtQuote, l.SortOrder))],
            x.ApprovedAt, x.PaidAt, x.PaymentMethod, x.PaymentReference, x.PaidRegisteredBy);
    }

    // --- Comunes ------------------------------------------------------------

    private static (ResultadoBandeja Resultado, string Motivo)? Estado(string actual, string? nuevo)
    {
        if (!TransicionesDeSolicitud.EsEstado(nuevo))
            return (ResultadoBandeja.Invalida, "Ese estado no existe. Usa: recibida, en_revision, cotizada, cerrada o rechazada.");
        if (!TransicionesDeSolicitud.Permitida(actual, nuevo!))
            return (ResultadoBandeja.Conflicto, TransicionesDeSolicitud.PorQueNo(actual, nuevo!));
        return null;
    }

    /// <summary>Nombra la fila, no la clase (<c>ANTES-DE-EMPEZAR-UN-MODULO.md</c> §5).</summary>
    public static string NombrePersonalizacion(SpecialOrderLead x) => $"solicitud de personalización sobre «{x.ProductName}»";

    public static string NombreVolumen(InstitutionRequest x) => $"solicitud de volumen de «{x.InstitutionName}»";
}
