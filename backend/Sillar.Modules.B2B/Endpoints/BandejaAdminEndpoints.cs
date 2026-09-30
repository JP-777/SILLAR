using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Sillar.Core.Contracts;
using Sillar.Modules.B2B.Bandeja;
using Sillar.Modules.B2B.Cotizaciones;
using Sillar.Modules.Catalog.Contracts;

namespace Sillar.Modules.B2B.Endpoints;

/// <summary>
/// Las rutas de administración de M07 (SPEC §6). Mínimo <c>editor</c>; las bajas,
/// <c>admin</c>. CSRF en todas; toda escritura deja auditoría <c>b2b</c> que
/// nombra la fila; las de cotización, por su número visible <c>C-AAAA-NNNN</c>.
/// </summary>
public static class BandejaAdminEndpoints
{
    private const string Tag = "Solicitudes B2B — Panel";
    private static readonly string[] EstadosDeCotizacion = ["borrador", "enviada", "aprobada", "pagada", "anulada"];

    /// <summary>Monta las rutas del panel.</summary>
    public static IEndpointRouteBuilder MapBandejaAdminEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var admin = endpoints.MapGroup("/api/admin/b2b")
            .WithTags(Tag)
            .RequireAuthorization(AdminRole.Editor)
            .AddEndpointFilter<CsrfEndpointFilter>();

        admin.MapGet("/special-orders", ListarPersonalizaciones)
            .WithName("B2bAdminListSpecialOrders").WithSummary("Bandeja de personalizaciones, con filtro por estado.")
            .Produces<IReadOnlyList<PersonalizacionEnBandeja>>().ProducesValidationProblem();
        admin.MapGet("/special-orders/{id:int}", ObtenerPersonalizacion)
            .WithName("B2bAdminGetSpecialOrder").WithSummary("Detalle de una personalización, con notas internas.")
            .Produces<PersonalizacionDetalle>().Produces(StatusCodes.Status404NotFound);
        admin.MapPut("/special-orders/{id:int}/status", EstadoPersonalizacion)
            .WithName("B2bAdminSetSpecialOrderStatus").WithSummary("Cambia el estado de una personalización.")
            .Produces<PersonalizacionDetalle>().ProducesValidationProblem().Produces(StatusCodes.Status404NotFound).ProducesProblem(StatusCodes.Status409Conflict);
        admin.MapPut("/special-orders/{id:int}/notes", NotasPersonalizacion)
            .WithName("B2bAdminSetSpecialOrderNotes").WithSummary("Reescribe las notas internas de una personalización.")
            .Produces<PersonalizacionDetalle>().Produces(StatusCodes.Status404NotFound);
        admin.MapPut("/special-orders/{id:int}/relink", Reenlazar)
            .WithName("B2bAdminRelinkSpecialOrder").WithSummary("Ata la personalización a un producto activo y refresca su instantánea.")
            .Produces<PersonalizacionDetalle>().Produces(StatusCodes.Status404NotFound).ProducesProblem(StatusCodes.Status409Conflict);
        admin.MapDelete("/special-orders/{id:int}", BajaPersonalizacion)
            .RequireAuthorization(AdminRole.Admin)
            .WithName("B2bAdminDeleteSpecialOrder").WithSummary("Da de baja una personalización, sin borrarla.")
            .Produces<PersonalizacionDetalle>().Produces(StatusCodes.Status404NotFound);

        admin.MapGet("/institution-requests", ListarVolumen)
            .WithName("B2bAdminListInstitutionRequests").WithSummary("Bandeja de solicitudes de volumen, con filtro por estado.")
            .Produces<IReadOnlyList<VolumenEnBandeja>>().ProducesValidationProblem();
        admin.MapGet("/institution-requests/{id:int}", ObtenerVolumen)
            .WithName("B2bAdminGetInstitutionRequest").WithSummary("Detalle de una solicitud de volumen, con notas internas.")
            .Produces<VolumenDetalle>().Produces(StatusCodes.Status404NotFound);
        admin.MapPut("/institution-requests/{id:int}/status", EstadoVolumen)
            .WithName("B2bAdminSetInstitutionRequestStatus").WithSummary("Cambia el estado de una solicitud de volumen.")
            .Produces<VolumenDetalle>().ProducesValidationProblem().Produces(StatusCodes.Status404NotFound).ProducesProblem(StatusCodes.Status409Conflict);
        admin.MapPut("/institution-requests/{id:int}/notes", NotasVolumen)
            .WithName("B2bAdminSetInstitutionRequestNotes").WithSummary("Reescribe las notas internas de una solicitud de volumen.")
            .Produces<VolumenDetalle>().Produces(StatusCodes.Status404NotFound);
        admin.MapDelete("/institution-requests/{id:int}", BajaVolumen)
            .RequireAuthorization(AdminRole.Admin)
            .WithName("B2bAdminDeleteInstitutionRequest").WithSummary("Da de baja una solicitud de volumen, sin borrarla.")
            .Produces<VolumenDetalle>().Produces(StatusCodes.Status404NotFound);

        admin.MapGet("/quotes", ListarCotizaciones)
            .WithName("B2bAdminListQuotes").WithSummary("Bandeja de cotizaciones, con filtro por estado.")
            .Produces<IReadOnlyList<CotizacionEnBandeja>>().ProducesValidationProblem();
        admin.MapGet("/quotes/{id:int}", ObtenerCotizacion)
            .WithName("B2bAdminGetQuote").WithSummary("Detalle de una cotización con sus líneas, su precio de catálogo y la evaluación mayorista.")
            .Produces<CotizacionPanel>().Produces(StatusCodes.Status404NotFound);
        admin.MapPost("/quotes", CrearCotizacion)
            .WithName("B2bAdminCreateQuote").WithSummary("Crea una cotización en borrador desde una solicitud, con su número C-AAAA-NNNN.")
            .Produces<CotizacionPanel>(StatusCodes.Status201Created).ProducesValidationProblem()
            .Produces(StatusCodes.Status404NotFound).ProducesProblem(StatusCodes.Status409Conflict);
        admin.MapPut("/quotes/{id:int}", EditarCotizacion)
            .WithName("B2bAdminEditQuoteLines").WithSummary("Sustituye las líneas de una cotización. Solo en borrador.")
            .Produces<CotizacionPanel>().ProducesValidationProblem()
            .Produces(StatusCodes.Status404NotFound).ProducesProblem(StatusCodes.Status409Conflict);
        admin.MapPut("/quotes/{id:int}/send", EnviarCotizacion)
            .WithName("B2bAdminSendQuote").WithSummary("Marca enviada una cotización en borrador con al menos una línea.")
            .Produces<CotizacionPanel>().Produces(StatusCodes.Status404NotFound).ProducesProblem(StatusCodes.Status409Conflict);
        admin.MapPut("/quotes/{id:int}/approve", AprobarCotizacion)
            .WithName("B2bAdminApproveQuote").WithSummary("Registra que el cliente aprobó una cotización enviada y vigente.")
            .Produces<CotizacionPanel>().Produces(StatusCodes.Status404NotFound).ProducesProblem(StatusCodes.Status409Conflict);
        admin.MapPut("/quotes/{id:int}/payment", PagarCotizacion)
            .RequireAuthorization(AdminRole.Admin)
            .WithName("B2bAdminRegisterQuotePayment").WithSummary("Registra el pago (Yape o efectivo) de una cotización aprobada.")
            .Produces<CotizacionPanel>().Produces(StatusCodes.Status404NotFound).ProducesProblem(StatusCodes.Status409Conflict);
        admin.MapDelete("/quotes/{id:int}", BajaCotizacion)
            .RequireAuthorization(AdminRole.Admin)
            .WithName("B2bAdminDeleteQuote").WithSummary("Da de baja una cotización, sin borrarla.")
            .Produces<CotizacionPanel>().Produces(StatusCodes.Status404NotFound);

        // Selectores, sobre el contrato real de M01 (mismo patrón que M02 en
        // FeaturedProductEndpoints): el frontend de M07 nunca habla con M01.
        admin.MapGet("/catalog/products", BuscarProductos)
            .WithName("B2bAdminSearchCatalogProducts").WithSummary("Busca productos activos de M01 para reenlazar una personalización.")
            .Produces<IReadOnlyList<ProductoParaElegir>>();
        admin.MapGet("/catalog/items", BuscarPresentaciones)
            .WithName("B2bAdminSearchCatalogItems").WithSummary("Busca presentaciones de M01 para una línea de cotización.")
            .Produces<IReadOnlyList<PresentacionParaElegir>>();

        return endpoints;
    }

    /// <summary>Lista las personalizaciones; con <c>status</c>, solo las de ese estado.</summary>
    private static Task<IResult> ListarPersonalizaciones(string? status, BandejaService s, CancellationToken ct)
        => ListarConFiltro(status, st => s.ListarPersonalizacionesAsync(st, ct));

    /// <summary>Devuelve una personalización con sus notas internas.</summary>
    private static async Task<IResult> ObtenerPersonalizacion(int id, BandejaService s, CancellationToken ct)
        => await s.ObtenerPersonalizacionAsync(id, ct) is { } v ? Results.Ok(v) : Results.NotFound();

    /// <summary>Cambia el estado de una personalización según la regla 5, y lo audita.</summary>
    private static async Task<IResult> EstadoPersonalizacion(int id, CambiarEstadoRequest r, BandejaService s, IAuditWriter a, ICurrentAdmin u, CancellationToken ct)
        => await Escritura(await s.CambiarEstadoPersonalizacionAsync(id, r.Status, ct), a, u, AuditAction.Update, "special_order_lead", id,
            n => $"La {n} pasó a {TransicionesDeSolicitud.Nombre(r.Status!)}.", ct);

    /// <summary>Reescribe las notas internas de una personalización, y lo audita.</summary>
    private static async Task<IResult> NotasPersonalizacion(int id, NotasRequest r, BandejaService s, IAuditWriter a, ICurrentAdmin u, CancellationToken ct)
        => await Escritura(await s.NotasPersonalizacionAsync(id, r.StaffNotes, ct), a, u, AuditAction.Update, "special_order_lead", id,
            n => $"Notas internas de la {n} actualizadas.", ct);

    /// <summary>Reenlaza una personalización a un producto activo, y lo audita.</summary>
    private static async Task<IResult> Reenlazar(int id, ReenlazarRequest r, BandejaService s, IAuditWriter a, ICurrentAdmin u, CancellationToken ct)
        => await Escritura(await s.ReenlazarAsync(id, r.ProductId, ct), a, u, AuditAction.Update, "special_order_lead", id,
            n => $"La {n} quedó reenlazada a su producto.", ct);

    /// <summary>Baja lógica de una personalización (solo <c>admin</c>), y lo audita.</summary>
    private static async Task<IResult> BajaPersonalizacion(int id, BandejaService s, IAuditWriter a, ICurrentAdmin u, CancellationToken ct)
        => await Escritura(await s.BajaPersonalizacionAsync(id, ct), a, u, AuditAction.Delete, "special_order_lead", id,
            n => $"Baja lógica de la {n}.", ct);

    /// <summary>Lista las solicitudes de volumen; con <c>status</c>, solo las de ese estado.</summary>
    private static Task<IResult> ListarVolumen(string? status, BandejaService s, CancellationToken ct)
        => ListarConFiltro(status, st => s.ListarVolumenAsync(st, ct));

    /// <summary>Devuelve una solicitud de volumen con sus notas internas.</summary>
    private static async Task<IResult> ObtenerVolumen(int id, BandejaService s, CancellationToken ct)
        => await s.ObtenerVolumenAsync(id, ct) is { } v ? Results.Ok(v) : Results.NotFound();

    /// <summary>Cambia el estado de una solicitud de volumen según la regla 5, y lo audita.</summary>
    private static async Task<IResult> EstadoVolumen(int id, CambiarEstadoRequest r, BandejaService s, IAuditWriter a, ICurrentAdmin u, CancellationToken ct)
        => await Escritura(await s.CambiarEstadoVolumenAsync(id, r.Status, ct), a, u, AuditAction.Update, "institution_request", id,
            n => $"La {n} pasó a {TransicionesDeSolicitud.Nombre(r.Status!)}.", ct);

    /// <summary>Reescribe las notas internas de una solicitud de volumen, y lo audita.</summary>
    private static async Task<IResult> NotasVolumen(int id, NotasRequest r, BandejaService s, IAuditWriter a, ICurrentAdmin u, CancellationToken ct)
        => await Escritura(await s.NotasVolumenAsync(id, r.StaffNotes, ct), a, u, AuditAction.Update, "institution_request", id,
            n => $"Notas internas de la {n} actualizadas.", ct);

    /// <summary>Baja lógica de una solicitud de volumen (solo <c>admin</c>), y lo audita.</summary>
    private static async Task<IResult> BajaVolumen(int id, BandejaService s, IAuditWriter a, ICurrentAdmin u, CancellationToken ct)
        => await Escritura(await s.BajaVolumenAsync(id, ct), a, u, AuditAction.Delete, "institution_request", id,
            n => $"Baja lógica de la {n}.", ct);

    /// <summary>Lista las cotizaciones; con <c>status</c>, solo las de ese estado.</summary>
    private static async Task<IResult> ListarCotizaciones(string? status, BandejaService s, CancellationToken ct)
    {
        if (status is not null && !EstadosDeCotizacion.Contains(status))
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["status"] = ["Ese estado no existe. Usa: borrador, enviada, aprobada, pagada o anulada."] });
        return Results.Ok(await s.ListarCotizacionesAsync(status, ct));
    }

    /// <summary>Devuelve una cotización con sus líneas, su precio de catálogo y la evaluación mayorista.</summary>
    private static async Task<IResult> ObtenerCotizacion(int id, CotizacionesService s, CancellationToken ct)
        => await s.ObtenerAsync(id, ct) is { } v ? Results.Ok(v) : Results.NotFound();

    /// <summary>Crea una cotización desde una solicitud, y lo audita con su número.</summary>
    internal static async Task<IResult> CrearCotizacion(CrearCotizacionRequest r, CotizacionesService s, IAuditWriter a, ICurrentAdmin u, CancellationToken ct)
    {
        var op = await s.CrearAsync(r, ct);
        var resultado = await Escritura(op, a, u, AuditAction.Create, "quote", op.Valor?.Detalle.Cotizacion.Id ?? 0,
            n => $"Cotización {n}, creada en borrador.", ct);
        return op.Resultado == ResultadoBandeja.Hecho
            ? Results.Created($"/api/admin/b2b/quotes/{op.Valor!.Detalle.Cotizacion.Id}", op.Valor)
            : resultado;
    }

    /// <summary>Sustituye las líneas de una cotización en borrador, y lo audita.</summary>
    internal static async Task<IResult> EditarCotizacion(int id, EditarLineasRequest r, CotizacionesService s, IAuditWriter a, ICurrentAdmin u, CancellationToken ct)
        => await Escritura(await s.EditarLineasAsync(id, r.Lines, ct), a, u, AuditAction.Update, "quote", id,
            n => $"Líneas de la cotización {n} actualizadas.", ct);

    /// <summary>Marca enviada una cotización, y lo audita.</summary>
    internal static async Task<IResult> EnviarCotizacion(int id, CotizacionesService s, IAuditWriter a, ICurrentAdmin u, CancellationToken ct)
        => await Escritura(await s.EnviarAsync(id, ct), a, u, AuditAction.Update, "quote", id, n => $"Cotización {n} enviada.", ct);

    /// <summary>Registra la aprobación del cliente, y lo audita.</summary>
    internal static async Task<IResult> AprobarCotizacion(int id, CotizacionesService s, IAuditWriter a, ICurrentAdmin u, CancellationToken ct)
        => await Escritura(await s.AprobarAsync(id, ct), a, u, AuditAction.Update, "quote", id, n => $"Cotización {n} aprobada por el cliente.", ct);

    /// <summary>Registra el pago (solo <c>admin</c>), y lo audita con el método.</summary>
    internal static async Task<IResult> PagarCotizacion(int id, PagoRequest r, CotizacionesService s, IAuditWriter a, ICurrentAdmin u, CancellationToken ct)
        => await Escritura(await s.RegistrarPagoAsync(id, r, u.Email ?? "desconocido", ct), a, u, AuditAction.Update, "quote", id,
            n => $"Pago de la cotización {n} registrado ({r.PaymentMethod}).", ct);

    /// <summary>Baja lógica de una cotización (solo <c>admin</c>), y lo audita.</summary>
    internal static async Task<IResult> BajaCotizacion(int id, CotizacionesService s, IAuditWriter a, ICurrentAdmin u, CancellationToken ct)
        => await Escritura(await s.BajaAsync(id, ct), a, u, AuditAction.Delete, "quote", id, n => $"Baja lógica de la cotización {n}.", ct);

    /// <summary>Busca productos activos para reenlazar; sin texto no devuelve nada.</summary>
    private static async Task<IResult> BuscarProductos(string? q, ICatalogService catalogo, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(q)) return Results.Ok(Array.Empty<ProductoParaElegir>());
        var productos = await catalogo.BuscarParaSeleccionAsync(q, 20, ct);
        return Results.Ok(productos.Where(p => p.IsActive)
            .Select(p => new ProductoParaElegir(p.ProductId, p.Name, p.IsPublic)).ToArray());
    }

    /// <summary>Busca presentaciones para una línea de cotización; sin texto no devuelve nada.</summary>
    private static async Task<IResult> BuscarPresentaciones(string? q, ICatalogService catalogo, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(q)) return Results.Ok(Array.Empty<PresentacionParaElegir>());
        var items = await catalogo.BuscarAsync(q, 20, ct);
        return Results.Ok(items.Select(i => new PresentacionParaElegir(i.ItemId, i.ProductName, i.VariantValue, i.SaleUnit, i.Price)).ToArray());
    }

    private static async Task<IResult> ListarConFiltro<T>(string? estado, Func<string?, Task<IReadOnlyList<T>>> listar)
    {
        if (estado is not null && !TransicionesDeSolicitud.EsEstado(estado))
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["status"] = ["Ese estado no existe. Usa: recibida, en_revision, cotizada, cerrada o rechazada."] });
        return Results.Ok(await listar(estado));
    }

    private static async Task<IResult> Escritura<T>(OperacionDeBandeja<T> op, IAuditWriter audit, ICurrentAdmin usuario,
        string accion, string entidad, int id, Func<string, string> resumen, CancellationToken ct)
    {
        switch (op.Resultado)
        {
            case ResultadoBandeja.Hecho:
                await audit.WriteAsync(new AuditEntry(accion)
                {
                    AdminUserId = usuario.AdminUserId,
                    AdminUserEmail = usuario.Email,
                    ModuleCode = B2BModule.ModuleCode,
                    EntityType = entidad,
                    EntityId = id.ToString(),
                    Summary = resumen(op.Nombre!),
                }, ct);
                return Results.Ok(op.Valor);
            case ResultadoBandeja.NoEncontrada:
                return Results.NotFound();
            case ResultadoBandeja.Invalida:
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["solicitud"] = [op.Motivo!] });
            default:
                return Results.Problem(title: op.Motivo, statusCode: StatusCodes.Status409Conflict);
        }
    }
}
