import { crearRegistroDeSuperficie } from './surfaceRegistry';

/**
 * **Quién ha pintado algo en la portada, y quién todavía no lo sabe.**
 *
 * Existe por un agujero concreto: `PublicSite` decidía «aquí no hay nada»
 * contando **secciones de módulos activos**, que es contar quién *podría*
 * aportar, no quién *aportó*. Con M02 activo y sin contenido publicado la
 * cuenta daba uno, así que no salía el aviso — y los cuatro bloques de M02
 * devuelven `null` cuando su lista llega vacía. Resultado: una portada con el
 * nombre del negocio y **nada debajo**. Ni contenido, ni explicación.
 *
 * Lo que hacía falta no era otra condición, era **otro dato**: el armazón no
 * puede saber si una sección pintó algo mirándola desde fuera. Se lo tiene que
 * decir ella.
 *
 * **Por qué hay un estado «cargando» y no dos valores.** Sin él, el aviso
 * aparecería en el hueco entre montar la portada y recibir los datos: «Sitio
 * en construcción» durante 200 ms es peor que no decir nada, y encima sería
 * mentira. Mientras alguien siga esperando, el armazón no afirma que no hay
 * nada — porque todavía no lo sabe. Es la misma regla que la doctrina de
 * animaciones aplica a los indicadores: lo que aún no se sabe no se cuenta.
 *
 * **El armazón sigue sin conocer a ningún módulo, y esto no lo rompe.** No hay
 * códigos de módulo aquí ni forma de preguntar «¿cuánto aporta CMS?»: cada
 * bloque se registra con una clave que se da a sí mismo y declara en cuál de
 * los tres estados está. Por eso una sección puede traer **un bloque o cuatro**
 * sin que el armazón se entere ni tenga que enterarse: `cmsHome` registra
 * cuatro aportes y `catalogHome` uno, y el resumen se calcula igual.
 *
 * La maquinaria vive en `surfaceRegistry.tsx` porque el pie hace la misma
 * pregunta sobre otra superficie. Este registro y el suyo son independientes.
 */
const portada = crearRegistroDeSuperficie();

export const AportesDePortada = portada.Proveedor;
export const useAporteDePortada = portada.useAporte;
export const useHomeState = portada.useEstado;
