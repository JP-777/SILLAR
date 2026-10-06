using Sillar.Modules.B2B.Bandeja;
using Sillar.Modules.B2B.Cotizaciones;
using Sillar.Modules.B2B.Solicitudes;
using Sillar.Shared.Platform;

namespace Sillar.Modules.B2B.Documentation;

/// <summary>
/// Cuerpos de ejemplo de M07 para la documentación OpenAPI.
/// </summary>
/// <remarks>
/// <para>
/// <b>Faltaba, y el verde anterior lo escondía.</b> El arnés comprueba que
/// ningún cuerpo de petición se quede sin ejemplo
/// (<c>e2e/tests/zz-instalacion.spec.ts</c>), y M07 nunca aportó su
/// <see cref="ISchemaExamples"/>. En el cierre focal del 05/10/2026 esa prueba
/// pasó <b>por accidente</b>: una spec anterior falló dejando <c>b2b</c>
/// desactivado, así que sus esquemas no llegaban al documento. Con el árbol en
/// verde, los nueve salieron a la vez.
/// </para>
/// <para>
/// El generador <b>no lee <c>&lt;example&gt;</c> de un <c>record</c>
/// posicional</b> y todos los DTO de SILLAR lo son, así que sin esto cada campo
/// se documenta con <c>"string"</c>.
/// </para>
/// <para>
/// El criterio de cada ejemplo es el del contrato: <b>¿podría alguien que no
/// conoce SILLAR copiarlo y que le funcione?</b> Por eso los identificadores son
/// <c>uuid</c> v7 con la forma real, los importes llevan dos decimales y los
/// estados son los que la tabla admite, no inventados.
/// </para>
/// </remarks>
public sealed class B2bExamples : ISchemaExamples
{
    /// <inheritdoc />
    public IReadOnlyDictionary<Type, string> Examples => Cuerpos;

    private static readonly Dictionary<Type, string> Cuerpos = new()
    {
        // --- Cliente ---------------------------------------------------------
        [typeof(CrearPersonalizacionRequest)] = """
            {
              "productId": "0192f3a0-7b1e-7c2d-8e4f-5a6b7c8d9e0f",
              "description": "El mismo cordón, pero con el escudo del colegio grabado",
              "quantity": 120,
              "neededBy": "2026-11-15"
            }
            """,

        [typeof(CrearVolumenRequest)] = """
            {
              "institutionName": "Colegio San Martín",
              "institutionDocument": "20123456789",
              "contactPerson": "Rosa Mamani, encargada de logística",
              "description": "Cordones para el desfile de aniversario",
              "quantity": 200,
              "eventDate": "2026-11-20"
            }
            """,

        // --- Panel · solicitudes --------------------------------------------
        [typeof(CambiarEstadoRequest)] = """
            {
              "status": "en_revision"
            }
            """,

        [typeof(NotasRequest)] = """
            {
              "staffNotes": "Confirmado por teléfono: entregan el escudo en vectores."
            }
            """,

        [typeof(ReenlazarRequest)] = """
            {
              "productId": "0192f3a0-7b1e-7c2d-8e4f-5a6b7c8d9e0f"
            }
            """,

        // --- Panel · cotizaciones -------------------------------------------
        //
        // `itemId` nulo es una línea libre («grabado del escudo»); con
        // presentación, la línea queda atada al catálogo de M01.
        [typeof(LineaRequest)] = """
            {
              "itemId": "0192f3a0-7b1e-7c2d-8e4f-5a6b7c8d9e10",
              "description": "Cordón para desfile, azul",
              "quantity": 200,
              "unitPrice": 3.50
            }
            """,

        [typeof(CrearCotizacionRequest)] = """
            {
              "origen": "volumen",
              "solicitudId": 1,
              "lines": [
                {
                  "itemId": "0192f3a0-7b1e-7c2d-8e4f-5a6b7c8d9e10",
                  "description": "Cordón para desfile, azul",
                  "quantity": 200,
                  "unitPrice": 3.50
                },
                {
                  "itemId": null,
                  "description": "Grabado del escudo",
                  "quantity": 1,
                  "unitPrice": 20.00
                }
              ]
            }
            """,

        [typeof(EditarLineasRequest)] = """
            {
              "lines": [
                {
                  "itemId": "0192f3a0-7b1e-7c2d-8e4f-5a6b7c8d9e10",
                  "description": "Cordón para desfile, azul",
                  "quantity": 200,
                  "unitPrice": 3.20
                }
              ]
            }
            """,

        // `paymentReference` es obligatorio con Yape y opcional en efectivo.
        [typeof(PagoRequest)] = """
            {
              "paymentMethod": "yape",
              "paymentReference": "OP-48217390"
            }
            """,
    };
}
