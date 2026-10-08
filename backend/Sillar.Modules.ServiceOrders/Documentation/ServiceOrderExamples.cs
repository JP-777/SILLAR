using Sillar.Modules.ServiceOrders.Application;
using Sillar.Shared.Platform;

namespace Sillar.Modules.ServiceOrders.Documentation;

/// <summary>Cuerpos copiables de M05b para Swagger/OpenAPI.</summary>
public sealed class ServiceOrderExamples : ISchemaExamples
{
    public IReadOnlyDictionary<Type, string> Examples => Bodies;

    private static readonly Dictionary<Type, string> Bodies = new()
    {
        [typeof(CreateServiceOrderRequest)] = """
            {
              "idempotencyKey": "019a04fb-26e1-76b7-99db-7498413dc540",
              "customerId": null,
              "customerName": "Rosa Mamani",
              "customerPhone": "+51 999 888 777",
              "customerEmail": null,
              "receivedNotes": "Coordinar la entrega por teléfono.",
              "receivedAt": "2026-10-07T14:15:00-05:00",
              "promisedAt": "2026-10-10T18:00:00-05:00",
              "assignToMe": true,
              "items": [
                {
                  "serviceId": 4,
                  "requestedDetails": "Impresión a color en tamaño A3.",
                  "quantity": 2.000,
                  "agreedUnitPrice": 18.50
                }
              ]
            }
            """,

        [typeof(UpdateServiceOrderRequest)] = """
            {
              "expectedUpdatedAt": "2026-10-07T19:15:00Z",
              "customerId": null,
              "customerName": "Rosa Mamani",
              "customerPhone": "+51 999 888 777",
              "customerEmail": "rosa@ejemplo.test",
              "receivedNotes": "Coordinar la entrega por teléfono.",
              "promisedAt": "2026-10-10T18:00:00-05:00",
              "items": [
                {
                  "serviceOrderItemId": "019a01d8-ce33-78ab-86ba-b108be476c55",
                  "serviceId": null,
                  "requestedDetails": "Impresión a color en tamaño A3, papel de 200 g.",
                  "quantity": 2.000,
                  "agreedUnitPrice": 19.00
                }
              ]
            }
            """,

        [typeof(ServiceOrderConcurrencyRequest)] = """
            {
              "expectedUpdatedAt": "2026-10-07T19:15:00Z"
            }
            """,

        [typeof(ServiceOrderTransitionRequest)] = """
            {
              "expectedStatus": "received",
              "targetStatus": "in_progress"
            }
            """
    };
}
