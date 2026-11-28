namespace Sillar.Modules.Sales.Dtos;

/// <summary>El intento se conserva al reintentar; nunca es una liquidación externa.</summary>
public sealed record RegistrarPagoManual(Guid RequestId, string Method, decimal Amount,
    string? Reference, bool CanFulfill);
public sealed record CambiarPedido(string Action, long ExpectedVersion, string? Reason);
public sealed record PedidoEnPanel(string OrderCode, string Status, string CustomerName,
    decimal TotalAmount, string? CurrencyCode, DateTimeOffset PlacedAt, DateTimeOffset PaymentDueAt,
    long Version, bool NeedsAttention);
public sealed record PagoEnPanel(string Method, decimal Amount, string? CurrencyCode,
    string? Reference, DateTimeOffset RegisteredAt, string RegisteredBy, bool WasLate, bool? CanFulfill);
public sealed record CambioEnPanel(string? FromStatus, string ToStatus, DateTimeOffset ChangedAt,
    string? ChangedBy, string? Reason);
public sealed record DetalleEnPanel(PedidoEnPanel Order, string CustomerEmail, string? CustomerPhone,
    IReadOnlyList<LineaDePedidoPropio> Lines, IReadOnlyList<PagoEnPanel> Payments,
    IReadOnlyList<CambioEnPanel> History);
public sealed record ResultadoDeGestion(bool Success, string? Error = null, bool Replayed = false);
