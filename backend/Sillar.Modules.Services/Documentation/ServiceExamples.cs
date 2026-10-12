using Sillar.Modules.Services.Dtos;
using Sillar.Shared.Platform;

namespace Sillar.Modules.Services.Documentation;

public sealed class ServiceExamples : ISchemaExamples
{
    public IReadOnlyDictionary<Type, string> Examples => Bodies;

    private static readonly Dictionary<Type, string> Bodies = new()
    {
        [typeof(SaveServiceRequest)] = """
            {
              "name": "Impresión de planos",
              "slug": "impresion-de-planos",
              "shortDescription": "Impresión técnica en formatos grandes.",
              "description": "Servicio de impresión de planos y láminas.",
              "price": 25.50,
              "saleUnit": "unidad",
              "imageId": null,
              "imageAltText": null
            }
            """,
        [typeof(ReorderServicesRequest)] = """
            {
              "orderedIds": [3, 1, 2]
            }
            """
    };
}
