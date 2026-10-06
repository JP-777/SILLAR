using Microsoft.EntityFrameworkCore;
using Sillar.Modules.B2B.Bandeja;
using Sillar.Modules.B2B.Data;
using Sillar.Modules.B2B.Domain;
using Sillar.Modules.Catalog.Contracts;

namespace Sillar.Modules.B2B.Cotizaciones;

/// <summary>El ciclo de una cotización (SPEC regla 6): borrador → enviada → aprobada → pagada.</summary>
public sealed class CotizacionesService(
    B2bDbContext db, BandejaService bandeja, ICatalogService catalogo, UmbralMayorista umbral, TimeProvider reloj)
{
    public async Task<CotizacionPanel?> ObtenerAsync(int id, CancellationToken ct)
    {
        var detalle = await bandeja.ObtenerCotizacionAsync(id, ct);
        if (detalle is null) return null;
        var mayorista = umbral.Evaluar([.. detalle.Lines.Select(l => (l.CatalogPriceAtQuote, l.Quantity, l.ItemId is not null))]);
        return new(detalle, mayorista);
    }

    /// <summary>Crea una cotización en borrador desde una solicitud, con su número visible.</summary>
    public async Task<OperacionDeBandeja<CotizacionPanel>> CrearAsync(CrearCotizacionRequest pedido, CancellationToken ct)
    {
        Guid cliente;
        string nombreOrigen;
        int? lead = null, institucion = null;

        switch (pedido.Origen)
        {
            case "personalizacion":
                var p = await db.SpecialOrderLeads.FirstOrDefaultAsync(x => x.Id == pedido.SolicitudId && x.IsActive, ct);
                if (p is null) return new(ResultadoBandeja.NoEncontrada);
                if (Cerrada(p.Status) is { } rp) return new(ResultadoBandeja.Conflicto, Motivo: rp);
                (cliente, nombreOrigen, lead) = (p.CustomerId, BandejaService.NombrePersonalizacion(p), p.Id);
                if (p.Status != RequestStatus.Cotizada) p.Status = RequestStatus.Cotizada;
                break;
            case "volumen":
                var v = await db.InstitutionRequests.FirstOrDefaultAsync(x => x.Id == pedido.SolicitudId && x.IsActive, ct);
                if (v is null) return new(ResultadoBandeja.NoEncontrada);
                if (Cerrada(v.Status) is { } rv) return new(ResultadoBandeja.Conflicto, Motivo: rv);
                (cliente, nombreOrigen, institucion) = (v.CustomerId, BandejaService.NombreVolumen(v), v.Id);
                if (v.Status != RequestStatus.Cotizada) v.Status = RequestStatus.Cotizada;
                break;
            default:
                return new(ResultadoBandeja.Invalida, Motivo: "Di de qué solicitud nace: «personalizacion» o «volumen».");
        }

        var lineas = await ResolverLineasAsync(pedido.Lines ?? [], ct);
        if (lineas.Error is { } error) return new(ResultadoBandeja.Invalida, Motivo: error);

        await using var transaccion = await db.Database.BeginTransactionAsync(ct);
        var cotizacion = new Quote
        {
            CustomerId = cliente,
            SpecialOrderLeadId = lead,
            InstitutionRequestId = institucion,
            TotalAmount = Total(lineas.Lineas),
        };
        cotizacion.Lines.AddRange(lineas.Lineas);
        db.Quotes.Add(cotizacion);

        // El número, AL FINAL y dentro de la transacción (ADR-016).
        cotizacion.QuoteNumber = await NumeradorDeCotizaciones.SiguienteAsync(db, reloj.GetUtcNow(), ct);
        await db.SaveChangesAsync(ct);
        await transaccion.CommitAsync(ct);

        return new(ResultadoBandeja.Hecho, await ObtenerAsync(cotizacion.Id, ct),
            Nombre: $"{cotizacion.QuoteNumber}, desde la {nombreOrigen}");
    }

    /// <summary>Sustituye las líneas. <b>Solo en borrador</b> (regla 6).</summary>
    public async Task<OperacionDeBandeja<CotizacionPanel>> EditarLineasAsync(int id, IReadOnlyList<LineaRequest>? lineasPedidas, CancellationToken ct)
    {
        var cotizacion = await db.Quotes.Include(q => q.Lines).FirstOrDefaultAsync(q => q.Id == id && q.IsActive, ct);
        if (cotizacion is null) return new(ResultadoBandeja.NoEncontrada);
        if (cotizacion.Status != QuoteStatus.Borrador)
            return new(ResultadoBandeja.Conflicto, Motivo:
                $"La cotización {cotizacion.QuoteNumber} ya está {cotizacion.Status}: sus líneas solo se editan en borrador. Si hay que cambiarla, haz una nueva desde la misma solicitud.");

        var lineas = await ResolverLineasAsync(lineasPedidas ?? [], ct);
        if (lineas.Error is { } error) return new(ResultadoBandeja.Invalida, Motivo: error);

        db.QuoteLines.RemoveRange(cotizacion.Lines);
        cotizacion.Lines.Clear();
        cotizacion.Lines.AddRange(lineas.Lineas);
        cotizacion.TotalAmount = Total(lineas.Lineas);
        await db.SaveChangesAsync(ct);
        return new(ResultadoBandeja.Hecho, await ObtenerAsync(id, ct), Nombre: cotizacion.QuoteNumber);
    }

    public Task<OperacionDeBandeja<CotizacionPanel>> EnviarAsync(int id, CancellationToken ct)
        => TransicionAsync(id, QuoteStatus.Borrador, ct, q =>
        {
            if (q.Lines.Count == 0) return $"La cotización {q.QuoteNumber} no tiene líneas. Añade al menos una antes de enviarla.";
            q.Status = QuoteStatus.Enviada;
            return null;
        });

    public Task<OperacionDeBandeja<CotizacionPanel>> AprobarAsync(int id, CancellationToken ct)
        => TransicionAsync(id, QuoteStatus.Enviada, ct, q =>
        {
            if (q.InvalidatedAt is not null)
                return $"La cotización {q.QuoteNumber} caducó y no se puede aprobar: {q.InvalidatedReason} Haz una nueva desde la misma solicitud.";
            q.Status = QuoteStatus.Aprobada;
            q.ApprovedAt = reloj.GetUtcNow();
            return null;
        });

    /// <summary>
    /// Registra el pago de una aprobada. Se registra, no se cobra (regla 9). Solo
    /// <c>admin</c>, por la ruta.
    /// </summary>
    /// <remarks>
    /// Queda <b>la atribución de los tres datos</b> de R-14 —nombre congelado,
    /// identificador local y nodo de la cuenta—, nunca una FK a
    /// <c>core.admin_users</c>. Antes guardaba un solo texto, y era el correo.
    /// </remarks>
    public Task<OperacionDeBandeja<CotizacionPanel>> RegistrarPagoAsync(int id, PagoRequest pago, AtribucionDelPersonal quienRegistra, CancellationToken ct)
        => TransicionAsync(id, QuoteStatus.Aprobada, ct, q =>
        {
            var referencia = pago.PaymentReference?.Trim();
            switch (pago.PaymentMethod)
            {
                case "yape" when string.IsNullOrEmpty(referencia):
                    return "Un pago con Yape necesita el código de operación.";
                case "yape" or "efectivo":
                    break;
                case "tarjeta":
                    return "El pago con tarjeta llega con el módulo de pagos. Por ahora registra Yape o efectivo.";
                default:
                    return "Elige cómo se pagó: «yape» o «efectivo».";
            }
            q.Status = QuoteStatus.Pagada;
            q.PaidAt = reloj.GetUtcNow();
            q.PaymentMethod = pago.PaymentMethod;
            q.PaymentReference = string.IsNullOrEmpty(referencia) ? null : referencia;
            q.PaidRegisteredBy = quienRegistra.Name;
            q.PaidRegisteredByAdminUserLocalId = quienRegistra.LocalId;
            q.PaidRegisteredByAdminUserHomeNode = quienRegistra.HomeNode;
            return null;
        });

    /// <summary>Baja lógica: la cotización deja de estar activa, en cualquier estado. Solo <c>admin</c>.</summary>
    public async Task<OperacionDeBandeja<CotizacionPanel>> BajaAsync(int id, CancellationToken ct)
    {
        var q = await db.Quotes.FirstOrDefaultAsync(x => x.Id == id && x.IsActive, ct);
        if (q is null) return new(ResultadoBandeja.NoEncontrada);
        q.IsActive = false;
        await db.SaveChangesAsync(ct);
        return new(ResultadoBandeja.Hecho, await ObtenerAsync(id, ct), Nombre: q.QuoteNumber);
    }

    private async Task<OperacionDeBandeja<CotizacionPanel>> TransicionAsync(int id, string desde, CancellationToken ct, Func<Quote, string?> aplicar)
    {
        var q = await db.Quotes.Include(x => x.Lines).FirstOrDefaultAsync(x => x.Id == id && x.IsActive, ct);
        if (q is null) return new(ResultadoBandeja.NoEncontrada);
        if (q.Status != desde)
            return new(ResultadoBandeja.Conflicto, Motivo: $"La cotización {q.QuoteNumber} está {q.Status}; esta acción es para una cotización {desde}.");
        if (aplicar(q) is { } motivo) return new(ResultadoBandeja.Conflicto, Motivo: motivo);
        await db.SaveChangesAsync(ct);
        return new(ResultadoBandeja.Hecho, await ObtenerAsync(id, ct), Nombre: q.QuoteNumber);
    }

    private async Task<(List<QuoteLine> Lineas, string? Error)> ResolverLineasAsync(IReadOnlyList<LineaRequest> pedidas, CancellationToken ct)
    {
        var lineas = new List<QuoteLine>();
        for (var i = 0; i < pedidas.Count; i++)
        {
            var l = pedidas[i];
            var n = i + 1;
            if (l.Quantity <= 0) return ([], $"La línea {n} necesita una cantidad mayor que cero.");
            if (l.UnitPrice < 0) return ([], $"El precio de la línea {n} no puede ser negativo.");
            var descripcion = l.Description?.Trim();

            if (l.ItemId is { } item)
            {
                // Congela lo que se cotizó (DECISIONES-PREVIAS-M07.md §4). El
                // precio de catálogo es ItemSnapshot.Price: nulo = «a consultar».
                var s = await catalogo.ObtenerItemAsync(item, ct);
                if (s is null || !await catalogo.ItemExisteYEstaActivoAsync(item, ct))
                    return ([], $"La presentación de la línea {n} no está activa en el catálogo. Elige otra o usa una línea libre.");
                lineas.Add(new QuoteLine
                {
                    ItemId = s.ItemId, ProductName = s.ProductName, VariantValue = s.VariantValue, SaleUnit = s.SaleUnit,
                    Description = string.IsNullOrEmpty(descripcion) ? (s.VariantValue is { Length: > 0 } va ? $"{s.ProductName} — {va}" : s.ProductName) : descripcion,
                    Quantity = l.Quantity, UnitPrice = l.UnitPrice, CatalogPriceAtQuote = s.Price, SortOrder = i,
                });
            }
            else
            {
                if (string.IsNullOrEmpty(descripcion)) return ([], $"La línea {n} es libre: escribe qué es.");
                lineas.Add(new QuoteLine { Description = descripcion, Quantity = l.Quantity, UnitPrice = l.UnitPrice, SortOrder = i });
            }
        }
        return (lineas, null);
    }

    private static decimal Total(IEnumerable<QuoteLine> lineas) => lineas.Sum(l => l.UnitPrice * l.Quantity);

    private static string? Cerrada(string estado) => estado is RequestStatus.Cerrada or RequestStatus.Rechazada
        ? $"La solicitud está {estado} y ya no se cotiza."
        : null;
}
