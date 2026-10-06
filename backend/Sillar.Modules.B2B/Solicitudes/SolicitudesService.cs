using Microsoft.EntityFrameworkCore;
using Sillar.Modules.B2B.Data;
using Sillar.Modules.B2B.Domain;
using Sillar.Modules.Catalog.Contracts;

namespace Sillar.Modules.B2B.Solicitudes;

/// <summary>Qué pasó con una operación del cliente.</summary>
public enum ResultadoSolicitud { Creada, Invalida, ProductoNoDisponible, Limitada }

/// <summary>Resultado con su frase para la persona, y la espera si la limitó el ritmo.</summary>
public sealed record OperacionDeSolicitud(
    ResultadoSolicitud Resultado, string? Motivo = null, SolicitudPropiaResponse? Solicitud = null,
    TimeSpan? Esperar = null);

/// <summary>Lo que el cliente hace con M07: pedir, ver lo suyo y leer su cotización.</summary>
public sealed class SolicitudesService(
    B2bDbContext db, ICatalogService catalogo, LimitePorCuenta limite, TimeProvider reloj)
{
    public const string Personalizacion = "personalizacion";
    public const string Volumen = "volumen";

    public async Task<OperacionDeSolicitud> CrearPersonalizacionAsync(
        Guid cliente, CrearPersonalizacionRequest pedido, CancellationToken ct)
    {
        var descripcion = pedido.Description?.Trim();
        if (string.IsNullOrEmpty(descripcion))
            return new(ResultadoSolicitud.Invalida, "Cuenta qué quieres cambiar del producto.");
        if (pedido.Quantity is <= 0)
            return new(ResultadoSolicitud.Invalida, "La cantidad tiene que ser mayor que cero, o déjala en blanco.");

        var producto = await catalogo.ObtenerParaSeleccionAsync(pedido.ProductId, ct);
        if (producto is null || !producto.IsActive || !producto.IsPublic)
            return new(ResultadoSolicitud.ProductoNoDisponible,
                "Ese producto ya no está en la tienda, así que no se puede pedir un cambio sobre él.");

        // El límite se consume DESPUÉS de validar: una petición que no puede
        // crear nada no gasta cupo. El precedente de M04 lo hace al revés
        // (ContactMessageService.cs:35-51); allí no hay cuenta que proteger.
        if (Limitada(cliente) is { } limitada) return limitada;

        var fila = new SpecialOrderLead
        {
            CustomerId = cliente,
            ProductId = producto.ProductId,
            ProductName = producto.Name,
            ProductSlug = producto.Slug,
            Description = descripcion,
            Quantity = pedido.Quantity,
            NeededBy = pedido.NeededBy,
        };
        db.SpecialOrderLeads.Add(fila);
        await db.SaveChangesAsync(ct);

        return new(ResultadoSolicitud.Creada, Solicitud: new(
            Personalizacion, fila.Id, fila.Description, fila.Status, fila.CreatedAt));
    }

    public async Task<OperacionDeSolicitud> CrearVolumenAsync(
        Guid cliente, CrearVolumenRequest pedido, CancellationToken ct)
    {
        var institucion = pedido.InstitutionName?.Trim();
        var descripcion = pedido.Description?.Trim();
        if (string.IsNullOrEmpty(institucion))
            return new(ResultadoSolicitud.Invalida, "Escribe para quién es el pedido: un colegio, una empresa o un evento.");
        if (string.IsNullOrEmpty(descripcion))
            return new(ResultadoSolicitud.Invalida, "Cuenta qué necesitas.");
        if (pedido.Quantity <= 0)
            return new(ResultadoSolicitud.Invalida, "La cantidad tiene que ser mayor que cero.");

        if (Limitada(cliente) is { } limitada) return limitada;

        var fila = new InstitutionRequest
        {
            CustomerId = cliente,
            InstitutionName = institucion,
            InstitutionDocument = Opcional(pedido.InstitutionDocument),
            ContactPerson = Opcional(pedido.ContactPerson),
            Description = descripcion,
            Quantity = pedido.Quantity,
            EventDate = pedido.EventDate,
        };
        db.InstitutionRequests.Add(fila);
        await db.SaveChangesAsync(ct);

        return new(ResultadoSolicitud.Creada, Solicitud: new(
            Volumen, fila.Id, fila.Description, fila.Status, fila.CreatedAt));
    }

    /// <summary>Las propias, de los dos tipos, más recientes primero. Sin notas internas.</summary>
    public async Task<IReadOnlyList<SolicitudPropiaResponse>> ListarPropiasAsync(Guid cliente, CancellationToken ct)
    {
        var personalizaciones = await db.SpecialOrderLeads.AsNoTracking()
            .Where(x => x.CustomerId == cliente && x.IsActive)
            .Select(x => new SolicitudPropiaResponse(Personalizacion, x.Id, x.Description, x.Status, x.CreatedAt))
            .ToListAsync(ct);
        var volumen = await db.InstitutionRequests.AsNoTracking()
            .Where(x => x.CustomerId == cliente && x.IsActive)
            .Select(x => new SolicitudPropiaResponse(Volumen, x.Id, x.Description, x.Status, x.CreatedAt))
            .ToListAsync(ct);

        return [.. personalizaciones.Concat(volumen).OrderByDescending(x => x.CreatedAt)];
    }

    /// <summary>
    /// Una cotización propia por su número. <c>null</c> tanto si no existe como
    /// si es de otro cliente: distinguirlos convertiría la ruta en un detector
    /// de números válidos (SPEC §6).
    /// </summary>
    public async Task<CotizacionPropiaResponse?> ObtenerCotizacionPropiaAsync(
        Guid cliente, string numero, CancellationToken ct)
    {
        var cotizacion = await db.Quotes.AsNoTracking().Include(x => x.Lines)
            .FirstOrDefaultAsync(x => x.QuoteNumber == numero && x.CustomerId == cliente && x.IsActive
                && x.Status != QuoteStatus.Borrador, ct);
        if (cotizacion is null) return null;

        return new(
            cotizacion.QuoteNumber, cotizacion.TotalAmount, cotizacion.Status,
            SigueValida: cotizacion.InvalidatedAt is null && cotizacion.Status != QuoteStatus.Anulada,
            cotizacion.ApprovedAt, cotizacion.PaidAt,
            [.. cotizacion.Lines.OrderBy(l => l.SortOrder).Select(l => new LineaDeCotizacionResponse(l.Description, l.Quantity, l.UnitPrice))]);
    }

    private OperacionDeSolicitud? Limitada(Guid cliente)
    {
        var decision = limite.Intentar(cliente, reloj.GetUtcNow());
        if (decision.Permitida) return null;
        var minutos = Math.Max(1, (int)Math.Ceiling(decision.EsperarHasta.TotalMinutes));
        return new(ResultadoSolicitud.Limitada,
            $"Ya enviaste varias solicitudes seguidas. Podrás enviar otra dentro de {minutos} min.",
            Esperar: decision.EsperarHasta);
    }

    private static string? Opcional(string? valor) => string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();
}
