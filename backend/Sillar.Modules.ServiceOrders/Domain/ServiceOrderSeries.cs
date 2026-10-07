namespace Sillar.Modules.ServiceOrders.Domain;

/// <summary>Contador local por nodo y año; deliberadamente no replicable.</summary>
public sealed class ServiceOrderSeries
{
    public int ServiceOrderSeriesId { get; set; }
    public required string NodeCode { get; set; }
    public int Year { get; set; }
    public int LastNumber { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
