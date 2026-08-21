namespace Sillar.Modules.Catalog.Contracts;

/// <summary>
/// Un producto tal como se ve al <b>elegirlo</b> desde otro módulo: buscar,
/// reconocerlo y quedarse con sus datos.
/// </summary>
/// <remarks>
/// <para>
/// Existe aparte de <see cref="ItemSnapshot"/> a propósito, y por dos razones
/// que no son de comodidad. La primera: <c>ItemSnapshot</c> es <b>el congelado
/// de la venta</b> —lo consumen M03 y M13—, y meterle campos de presentación
/// web contamina un registro transaccional. La segunda: aquel devuelve
/// <b>presentaciones</b>, y aquí se elige un <b>producto</b>.
/// </para>
/// <para>
/// Quien lo recibe <b>copia estos valores a su propia tabla y no vuelve a
/// preguntar</b>. Es lo que hace que su pantalla sobreviva a la desinstalación
/// del catálogo. La contrapartida: un snapshot envejece, y por eso M01 emite
/// <c>ProductoActualizado</c> siempre que cambie algo de lo que hay aquí.
/// </para>
/// </remarks>
/// <param name="ProductId">El producto. Es lo que se guarda para poder volver a enlazarlo.</param>
/// <param name="Name">Producto + característica + marca/modelo + presentación, según el diccionario.</param>
/// <param name="Slug">
/// Su dirección pública. <b>Es lo único con lo que se enlaza</b>, y no se
/// recalcula al renombrar el producto (regla 3), así que puede editarse a mano
/// y dejar un snapshot apuntando a una URL que ya no existe.
/// </param>
/// <param name="PrimaryImageId">
/// La imagen marcada como principal, o la de menor orden si ninguna lo está
/// (regla 11). <b>Nulo es un caso normal, no un borde</b>: un producto sin
/// foto se presenta con su nombre en grande, que es una decisión de diseño.
/// </param>
/// <param name="PrimaryCategoryName">
/// La categoría <b>efectiva</b>, la misma que alimenta la miga de pan: la
/// principal si está activa, y si no la primera activa de las demás
/// (<c>Breadcrumb.ChooseTarget</c>). <b>No la cruda</b> — copiar aquélla
/// congelaría un nombre que la tienda ya no muestra en ninguna parte, que es
/// peor que no tener ninguno.
/// <para>
/// Nula cuando el producto no tiene ninguna categoría activa. No es raro:
/// nueve de los veinte productos de demostración están así.
/// </para>
/// </param>
/// <param name="Price">
/// Lo que cuesta, ya resuelto por <c>ItemPricing.ForCard</c>. <b>Tres estados,
/// y los tres significan cosas distintas</b> (SPEC regla 5):
/// <list type="table">
///   <item><term><c>null</c></term><description>A consultar. <b>No es gratis.</b></description></item>
///   <item><term><c>0</c></term><description><b>Gratis.</b> Es un precio, y se muestra como tal.</description></item>
///   <item><term>mayor que 0</term><description>El precio.</description></item>
/// </list>
/// <para>
/// Está documentado aquí porque <b>ya mordió una vez</b>: en la tarjeta
/// pública, «Gratis» cortocircuitaba antes de llegar al número y un producto
/// con una presentación gratis y otra de 8 llegó a decir que no había que
/// pagar nada por él. Quien renderice estos tres estados los va a
/// redescubrir si el contrato solo advierte del nulo.
/// </para>
/// </param>
/// <param name="PriceVaries">
/// Si las presentaciones no cuestan lo mismo, y por tanto <see cref="Price"/>
/// es una cota: se dice «Desde». <b>El contrato da el número y el hecho; la
/// frase la pone quien pinta</b> — la moneda y el idioma son del frontend, y
/// congelarlos en un snapshot los deja fijos para siempre.
/// </param>
/// <param name="IsPublic">
/// Si el producto sale hoy en la tienda. <b>Se puede elegir uno que no lo
/// esté</b>: preparar en enero la portada de la campaña escolar para que se
/// publique sola en febrero es un caso real. Quien lo muestre debería avisar
/// de que todavía no se ve.
/// </param>
/// <param name="IsActive">
/// Si el producto sigue de alta en el catálogo.
/// <para>
/// <b>Falso significa «existe, pero alguien lo dio de baja», y se puede
/// volver.</b> Es distinto de que el producto no aparezca: para quien guarda
/// un snapshot, una baja se retira de la portada y se deja a la vista para
/// reactivarla, mientras que una desaparición obliga a elegir el producto otra
/// vez. Dos estados, dos respuestas.
/// </para>
/// <para>
/// Que se pueda distinguir depende de una regla nuestra: <b>en SILLAR no hay
/// borrado físico</b>. Una fila solo desaparece si se desinstaló el módulo y
/// se volvió a instalar — así que ausencia significa exactamente una cosa.
/// </para>
/// </param>
public sealed record ProductPickerItem(
    Guid ProductId,
    string Name,
    string Slug,
    Guid? PrimaryImageId,
    string? PrimaryCategoryName,
    decimal? Price,
    bool PriceVaries,
    bool IsPublic,
    bool IsActive);
