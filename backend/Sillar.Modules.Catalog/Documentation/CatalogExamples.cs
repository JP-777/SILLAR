using Sillar.Modules.Catalog.Dtos;
using Sillar.Shared.Platform;

namespace Sillar.Modules.Catalog.Documentation;

/// <summary>
/// Los cuerpos de ejemplo de M01, en Swagger.
/// </summary>
/// <remarks>
/// <para>
/// El criterio es uno solo: <b>¿podría alguien que no conoce SILLAR copiar
/// este ejemplo y que le funcione?</b> Un cuerpo con <c>"string"</c> en cada
/// campo no sirve para eso — y es lo que Swashbuckle genera solo.
/// </para>
/// <para>
/// Por eso los nombres son los del diccionario del proyecto —producto,
/// característica principal, marca o modelo, presentación— y no
/// «Producto 1». Un ejemplo enseña dos cosas a la vez: qué forma tiene el
/// cuerpo y <b>cómo se nombran las cosas aquí</b>.
/// </para>
/// <para>
/// Los identificadores de ejemplo son <c>uuid</c> v7 con la pinta que tienen
/// los de verdad, y los precios llevan decimales, porque un <c>0</c> de
/// relleno hace dudar de si el campo admite céntimos.
/// </para>
/// </remarks>
public sealed class CatalogExamples : ISchemaExamples
{
    /// <inheritdoc />
    public IReadOnlyDictionary<Type, string> Examples => Cuerpos;

    private static readonly Dictionary<Type, string> Cuerpos = new()
    {
        [typeof(CreateBrandRequest)] = """
            {
              "name": "Faber-Castell",
              "slug": "faber-castell",
              "logoId": null
            }
            """,

        [typeof(UpdateBrandRequest)] = """
            {
              "name": "Faber-Castell",
              "slug": "faber-castell",
              "logoId": "019a01d8-ce33-78ab-86ba-b108be476c55",
              "isActive": true
            }
            """,

        [typeof(CreateCategoryRequest)] = """
            {
              "name": "Cuadernos",
              "slug": "cuadernos",
              "parentId": "019a01d8-ef58-7c8a-bd2e-598df09930fb",
              "description": "Universitarios, cuadriculados y rayados.",
              "imageId": null,
              "sortOrder": 10
            }
            """,

        [typeof(UpdateCategoryRequest)] = """
            {
              "name": "Cuadernos",
              "slug": "cuadernos",
              "parentId": "019a01d8-ef58-7c8a-bd2e-598df09930fb",
              "description": "Universitarios, cuadriculados y rayados.",
              "imageId": null,
              "sortOrder": 10,
              "isActive": true
            }
            """,

        // El caso mayoritario: un producto de una sola presentación, cuyos
        // código y precio se mandan como si fueran del producto porque lo son
        // de su variante única. La palabra «variante» no aparece.
        [typeof(CreateProductRequest)] = """
            {
              "name": "Cuaderno universitario cuadriculado Stanford A4 100 hojas",
              "slug": "cuaderno-universitario-cuadriculado-stanford-a4-100-hojas",
              "shortDescription": "Tapa dura, 100 hojas cuadriculadas.",
              "description": "Espiral doble, hoja de 80 gramos y cuadrícula de 5 milímetros.",
              "primaryCategoryId": "019a01d8-ef58-7c8a-bd2e-598df09930fb",
              "categoryIds": ["019a01d8-ef58-7c8a-bd2e-598df09930fb"],
              "brandId": "019a01d8-ce33-78ab-86ba-b108be476c55",
              "listPrice": 12.50,
              "saleUnit": "Por unidad",
              "variantLabel": null,
              "code": "STF-CU-A4-100",
              "barcode": "7750182000015"
            }
            """,

        [typeof(UpdateProductRequest)] = """
            {
              "name": "Cuaderno universitario cuadriculado Stanford A4 100 hojas",
              "slug": "cuaderno-universitario-cuadriculado-stanford-a4-100-hojas",
              "shortDescription": "Tapa dura, 100 hojas cuadriculadas.",
              "description": "Espiral doble, hoja de 80 gramos y cuadrícula de 5 milímetros.",
              "brandId": "019a01d8-ce33-78ab-86ba-b108be476c55",
              "listPrice": 12.50,
              "saleUnit": "Por unidad",
              "variantLabel": null,
              "isPublic": true,
              "isActive": true,
              "code": "STF-CU-A4-100",
              "barcode": "7750182000015",
              "singleVariantFieldsPresent": true
            }
            """,

        [typeof(SetProductCategoriesRequest)] = """
            {
              "categoryIds": [
                "019a01d8-ef58-7c8a-bd2e-598df09930fb",
                "019a01d8-f0a2-7d41-9c17-2b8f4e6a1d33"
              ],
              "primaryCategoryId": "019a01d8-ef58-7c8a-bd2e-598df09930fb"
            }
            """,

        // Una presentación con precio propio: el caso que obliga a la tarjeta
        // pública a decir «Desde».
        [typeof(CreateProductItemRequest)] = """
            {
              "variantValue": "Azul metálico",
              "code": "ART-PZ-AZU",
              "barcode": "7751271000032",
              "priceOverride": 5.90,
              "imageId": null
            }
            """,

        [typeof(UpdateProductItemRequest)] = """
            {
              "variantValue": "Azul metálico",
              "code": "ART-PZ-AZU",
              "barcode": "7751271000032",
              "priceOverride": 5.90,
              "imageId": null,
              "sortOrder": 2,
              "isActive": true
            }
            """,

        [typeof(AssociateProductImageRequest)] = """
            {
              "mediaAssetId": "019a01d8-ec33-78ab-86ba-b108be476c55",
              "altText": "Cuaderno cuadriculado visto de frente"
            }
            """,

        [typeof(ReorderProductImagesRequest)] = """
            {
              "orderedImageIds": [
                "019a01d8-1111-7c8a-bd2e-598df09930fb",
                "019a01d8-2222-7c8a-bd2e-598df09930fb"
              ],
              "primaryImageId": "019a01d8-1111-7c8a-bd2e-598df09930fb"
            }
            """,
    };

}
