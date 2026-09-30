using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Sillar.Core.Contracts;
using Sillar.Modules.B2B.Data;
using Sillar.Modules.B2B.Domain;
using Sillar.Modules.Catalog.Contracts;
using Sillar.Modules.Catalog.Contracts.Events;
using Sillar.Shared.Events;

namespace Sillar.Modules.B2B.Catalogo;

/// <summary>
/// Lo que M07 hace cuando M01 cambia un producto (SPEC §5, eventos consumidos).
/// </summary>
/// <remarks>
/// <para>
/// <b>Dos variedades de instantánea, cada una en su sitio</b>
/// (<c>DECISIONES-PREVIAS-M07.md</c> §4): la solicitud de personalización
/// <b>refresca</b> —el personal necesita el producto de hoy para cotizar— y la
/// línea de cotización <b>congela</b> —registra lo que se cotizó—. Aquí la
/// línea nunca se reescribe: solo se compara su precio para invalidar.
/// </para>
/// <para>
/// <b>Idempotente</b>: M01 puede emitir varios <c>ProductoActualizado</c> por
/// una sola acción (<c>CatalogEvents.cs:24-28</c>). Aplicar dos veces el mismo
/// estado da el mismo resultado, y una cotización ya invalidada no se toca.
/// Un semáforo por producto evita que dos eventos del mismo producto se crucen.
/// </para>
/// </remarks>
public sealed class ReaccionAlCatalogo(IServiceScopeFactory scopes, TimeProvider reloj)
{
    private readonly ConcurrentDictionary<Guid, SemaphoreSlim> turnos = new();

    /// <summary>Refresca instantáneas e invalida cotizaciones enviadas cuyo precio cambió.</summary>
    public Task ProductoActualizadoAsync(Guid productoId, CancellationToken ct)
        => EnTurnoAsync(productoId, async (db, catalogo, moneda) =>
        {
            var producto = await catalogo.ObtenerParaSeleccionAsync(productoId, ct);
            await RefrescarSolicitudesAsync(db, productoId, producto, ct);
            if (producto is not null)
            {
                await InvalidarPorPrecioAsync(db, catalogo, moneda, productoId, ct);
            }
            await db.SaveChangesAsync(ct);
        }, ct);

    /// <summary>
    /// Marca el reenlace pendiente y <b>no invalida ninguna cotización</b>: lo que
    /// cambia el trato con el cliente es el precio, no la disponibilidad (SPEC §5).
    /// </summary>
    public Task ProductoDesactivadoAsync(Guid productoId, CancellationToken ct)
        => EnTurnoAsync(productoId, async (db, _, _) =>
        {
            await db.SpecialOrderLeads.Where(x => x.ProductId == productoId && !x.PendingRelink)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.PendingRelink, true), ct);
        }, ct);

    private static async Task RefrescarSolicitudesAsync(B2bDbContext db, Guid productoId, ProductPickerItem? producto, CancellationToken ct)
    {
        var solicitudes = await db.SpecialOrderLeads.Where(x => x.ProductId == productoId).ToListAsync(ct);
        foreach (var solicitud in solicitudes)
        {
            if (producto is null || !producto.IsActive)
            {
                // Ya no se puede releer, o lo dieron de baja: la instantánea se
                // queda diciendo de qué se hablaba, y el personal lo reenlaza.
                solicitud.PendingRelink = true;
                continue;
            }

            solicitud.ProductName = producto.Name;
            solicitud.ProductSlug = producto.Slug;
            solicitud.PendingRelink = false;
        }
    }

    private async Task InvalidarPorPrecioAsync(B2bDbContext db, ICatalogService catalogo, string? moneda, Guid productoId, CancellationToken ct)
    {
        var precios = (await catalogo.VariantesDeAsync(productoId, ct)).ToDictionary(v => v.ItemId, v => v.Price);
        if (precios.Count == 0) return;
        var items = precios.Keys.ToList();

        // Solo las ENVIADAS y aún válidas (SPEC regla 7). Regla final de E3b,
        // ratificada por JP el 30/09/2026:
        //   precio conocido → otro precio      ⇒ invalida;
        //   precio conocido → «a consultar»    ⇒ invalida (desapareció la referencia);
        //   «a consultar» → aparece un precio  ⇒ NO invalida (no había contra qué comparar);
        //   línea libre                        ⇒ no participa.
        // Por eso solo se miran líneas con presentación Y precio de catálogo registrado.
        var candidatas = await db.Quotes.Include(q => q.Lines)
            .Where(q => q.Status == QuoteStatus.Enviada && q.InvalidatedAt == null && q.IsActive
                && q.Lines.Any(l => l.ItemId != null && items.Contains(l.ItemId.Value) && l.CatalogPriceAtQuote != null))
            .ToListAsync(ct);

        foreach (var cotizacion in candidatas)
        {
            var movida = cotizacion.Lines.FirstOrDefault(l =>
                l.ItemId is { } item && l.CatalogPriceAtQuote is { } cotizado
                && precios.TryGetValue(item, out var ahora) && ahora != cotizado);
            if (movida is null) continue;

            // La moneda es la de la instalación (currency_code, de CORE por su
            // contrato), no un símbolo escrito aquí.
            var ahoraTexto = precios[movida.ItemId!.Value] is { } p ? Importe(p, moneda) : "a consultar";
            cotizacion.InvalidatedAt = reloj.GetUtcNow();
            cotizacion.InvalidatedReason =
                $"Cambió el precio de catálogo de «{Presentacion(movida)}»: se cotizó con {Importe(movida.CatalogPriceAtQuote!.Value, moneda)} y ahora está {ahoraTexto}.";
        }
    }

    private static string Presentacion(QuoteLine l)
        => l.VariantValue is { Length: > 0 } variante ? $"{l.ProductName} — {variante}" : l.ProductName ?? l.Description;

    private static string Importe(decimal valor, string? moneda)
        => string.IsNullOrWhiteSpace(moneda) ? $"{valor:0.00}" : $"{valor:0.00} {moneda}";

    private async Task EnTurnoAsync(Guid productoId, Func<B2bDbContext, ICatalogService, string?, Task> trabajo, CancellationToken ct)
    {
        var turno = turnos.GetOrAdd(productoId, static _ => new SemaphoreSlim(1, 1));
        await turno.WaitAsync(ct);
        try
        {
            await using var scope = scopes.CreateAsyncScope();
            await trabajo(scope.ServiceProvider.GetRequiredService<B2bDbContext>(),
                scope.ServiceProvider.GetRequiredService<ICatalogService>(),
                scope.ServiceProvider.GetService<ISettingsReader>()?.Get("currency_code"));
        }
        finally
        {
            turno.Release();
        }
    }
}

internal sealed class ProductoActualizadoEnB2b(ReaccionAlCatalogo reaccion) : IEventHandler<ProductoActualizado>
{
    public Task HandleAsync(ProductoActualizado evento, CancellationToken ct) => reaccion.ProductoActualizadoAsync(evento.ProductId, ct);
}

internal sealed class ProductoDesactivadoEnB2b(ReaccionAlCatalogo reaccion) : IEventHandler<ProductoDesactivado>
{
    public Task HandleAsync(ProductoDesactivado evento, CancellationToken ct) => reaccion.ProductoDesactivadoAsync(evento.ProductId, ct);
}
