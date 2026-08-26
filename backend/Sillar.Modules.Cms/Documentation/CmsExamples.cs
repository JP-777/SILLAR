using Sillar.Modules.Cms.Dtos;
using Sillar.Shared.Platform;

namespace Sillar.Modules.Cms.Documentation;

/// <summary>
/// Los cuerpos de ejemplo de M02, en Swagger.
/// </summary>
/// <remarks>
/// <para>
/// Mismo criterio que <c>CatalogExamples</c>, del que esto copia la forma:
/// <b>¿podría alguien que no conoce SILLAR copiar este cuerpo y que le
/// funcione?</b> Un ejemplo con <c>"string"</c> en cada campo satisface a
/// quien lo cuenta y no informa a quien lo lee.
/// </para>
/// <para>
/// <b>Por qué faltaban hasta el 25 de agosto de 2026.</b> El arnés comprueba
/// que ningún <c>*Request</c> se quede sin ejemplo, pero la comprobación no
/// veía a M02 porque el entorno de pruebas ni aplicaba sus migraciones ni
/// activaba el módulo. Los doce cuerpos existían desde el primer día sin
/// ejemplo y sin que nada lo dijera.
/// </para>
/// <para>
/// <b>Las fechas llevan huso.</b> Son <c>timestamptz</c>, y un ejemplo sin
/// desplazamiento invita a mandar una hora local suelta — que es justo el
/// error que el tipo existe para evitar. Van con <c>-05:00</c>, que es el del
/// negocio.
/// </para>
/// <para>
/// <b>Y ninguno lleva el nombre de un negocio real</b>, ni de un colegio ni de
/// una institución: este repositorio contiene el producto, nunca a un cliente.
/// </para>
/// </remarks>
public sealed class CmsExamples : ISchemaExamples
{
    /// <inheritdoc />
    public IReadOnlyDictionary<Type, string> Examples => Cuerpos;

    private static readonly Dictionary<Type, string> Cuerpos = new()
    {
        // Un banner completo: con imagen, y por tanto con texto alternativo,
        // que es obligatorio en cuanto hay imagen. La ventana de publicación
        // es lo que lo pone en «Vigente» sin que nadie lo active a mano.
        [typeof(CreateBannerRequest)] = """
            {
              "title": "Campaña escolar",
              "subtitle": "Listas completas y útiles al por mayor",
              "imageDesktopId": "019a01d8-ce33-78ab-86ba-b108be476c55",
              "imageMobileId": "019a01d8-ce33-78ab-86ba-b108be476c56",
              "altText": "Mochila y cuadernos sobre un pupitre",
              "linkUrl": "/catalogo/cuadernos",
              "linkLabel": "Ver los cuadernos",
              "startsAt": "2026-02-01T00:00:00-05:00",
              "endsAt": "2026-03-31T23:59:59-05:00"
            }
            """,

        // Editar no activa ni desactiva, y por eso no hay `isActive` aquí:
        // el ciclo de vida tiene sus propias operaciones y sus propios
        // permisos. Es la misma frase que enseña el cajón al abrirse.
        [typeof(UpdateBannerRequest)] = """
            {
              "title": "Campaña escolar",
              "subtitle": "Listas completas, útiles al por mayor y forrado gratis",
              "imageDesktopId": "019a01d8-ce33-78ab-86ba-b108be476c55",
              "imageMobileId": null,
              "altText": "Mochila y cuadernos sobre un pupitre",
              "linkUrl": "/catalogo/cuadernos",
              "linkLabel": "Ver los cuadernos",
              "startsAt": "2026-02-01T00:00:00-05:00",
              "endsAt": "2026-04-15T23:59:59-05:00"
            }
            """,

        // La insignia es corta a propósito: es lo que se pinta encima de la
        // tarjeta, no un segundo título.
        [typeof(CreatePromotionRequest)] = """
            {
              "title": "Dos plumones por el precio de uno",
              "subtitle": "Solo en colores de pizarra",
              "description": "Válido hasta agotar existencias. No acumulable con el precio al por mayor.",
              "badgeText": "2x1",
              "imageId": "019a01d8-d1f0-7a44-9c17-2b8f4e6a1d33",
              "altText": "Plumones de pizarra en un vaso",
              "linkUrl": "/catalogo/escritura",
              "linkLabel": "Ver los plumones",
              "startsAt": "2026-03-01T00:00:00-05:00",
              "endsAt": "2026-03-15T23:59:59-05:00"
            }
            """,

        [typeof(UpdatePromotionRequest)] = """
            {
              "title": "Dos plumones por el precio de uno",
              "subtitle": "Solo en colores de pizarra",
              "description": "Válido hasta agotar existencias. No acumulable con el precio al por mayor.",
              "badgeText": "2x1",
              "imageId": "019a01d8-d1f0-7a44-9c17-2b8f4e6a1d33",
              "altText": "Plumones de pizarra en un vaso",
              "linkUrl": "/catalogo/escritura",
              "linkLabel": "Ver los plumones",
              "startsAt": "2026-03-01T00:00:00-05:00",
              "endsAt": "2026-03-31T23:59:59-05:00"
            }
            """,

        // Destacar es elegir un producto de M01 y darle una ventana. El
        // snapshot lo copia CMS al guardar: aquí no se manda ni el nombre ni
        // el precio, y mandarlos sería inventarse el catálogo.
        [typeof(CreateFeaturedProductRequest)] = """
            {
              "productId": "019a01d8-f0a2-7d41-9c17-2b8f4e6a1d33",
              "startsAt": "2026-02-01T00:00:00-05:00",
              "endsAt": "2026-03-31T23:59:59-05:00"
            }
            """,

        // Editar un destacado cambia únicamente la vigencia — nunca el
        // producto enlazado. Para eso está el reenlace, que es otro endpoint.
        [typeof(UpdateFeaturedProductRequest)] = """
            {
              "startsAt": "2026-02-01T00:00:00-05:00",
              "endsAt": "2026-04-15T23:59:59-05:00"
            }
            """,

        // Reenlazar sustituye el snapshot y conserva la fila editorial: su
        // posición en la portada y su ventana siguen siendo las mismas.
        [typeof(RelinkFeaturedProductRequest)] = """
            {
              "productId": "019a01d8-f0a2-7d41-9c17-2b8f4e6a1d34"
            }
            """,

        [typeof(CreateFeaturedProjectRequest)] = """
            {
              "title": "Tarjetas de invitación troqueladas",
              "description": "Impresión a dos tintas sobre cartulina perlada, con troquel a medida.",
              "imageId": "019a01d8-d3b7-7e52-8a09-4c1d6f2b9e77",
              "altText": "Tarjetas troqueladas extendidas sobre una mesa"
            }
            """,

        [typeof(UpdateFeaturedProjectRequest)] = """
            {
              "title": "Tarjetas de invitación troqueladas",
              "description": "Impresión a dos tintas sobre cartulina perlada, con troquel a medida y sobre incluido.",
              "imageId": "019a01d8-d3b7-7e52-8a09-4c1d6f2b9e77",
              "altText": "Tarjetas troqueladas extendidas sobre una mesa"
            }
            """,

        // La plataforma es el valor del selector, en minúscula, y no la
        // etiqueta que se enseña: mandar «Instagram» es el error probable, y
        // por eso el ejemplo lo resuelve enseñando el valor bueno.
        [typeof(CreateSocialLinkRequest)] = """
            {
              "platform": "instagram",
              "url": "https://instagram.com/mi-negocio"
            }
            """,

        [typeof(UpdateSocialLinkRequest)] = """
            {
              "platform": "whatsapp",
              "url": "https://wa.me/51999888777"
            }
            """,

        // **La lista va entera y en el orden final**, no solo lo que se movió:
        // el cuerpo describe cómo queda la sección, no qué gesto se hizo. Y
        // «entera» incluye las filas inactivas — filtrarlas antes de llamar
        // rompe el contrato, como avisa `reorder.ts:9-11` en el frontend.
        //
        // Es el mismo cuerpo para banners, promociones, destacados, trabajos
        // y redes, así que los identificadores son los de su propia sección.
        [typeof(ReorderCmsRequest)] = """
            {
              "orderedIds": [4, 1, 3, 2]
            }
            """,
    };
}
