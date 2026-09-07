/**
 * Cuándo sirve la carpeta de medios de la suite, y cuándo no.
 *
 * Vive aparte de `global-setup` porque es una decisión pura —sin disco, sin
 * docker, sin arnés— y porque la primera versión se equivocaba. Aislada se puede
 * provocar entera; dentro del arranque solo se podía provocar levantando el
 * stack.
 */

/**
 * UID del proceso de la API dentro del contenedor. La imagen base define `app`
 * con este número (`backend/Dockerfile:50-51`).
 */
const UID_DE_LA_API = 1654;

/**
 * Decide si una carpeta de medios que ya existe sirve, y si no, por qué.
 *
 * **Está aparte y es pura porque la primera versión se equivocaba**, y de la
 * peor manera: bloqueaba una carpeta que funcionaba. Daba por hecho que un
 * `chmod` denegado significaba «la creó docker como root». No: significa «no soy
 * el dueño». Y si el dueño es justamente el UID de la API, la carpeta está
 * **mejor** que si fuera mía — el proceso que escribe dentro es su propietario.
 *
 * Cazado por la puerta, en la primera corrida que llevaba la guarda dentro de
 * `sillar-fx`, cuya carpeta era `1654:1654` desde antes del arreglo.
 *
 * @param uidPropietario Dueño de la carpeta en el disco.
 * @param sePudoAbrir Si el `chmod` a 0777 funcionó.
 * @returns `null` si sirve; si no, qué pasa y qué hacer.
 */
export function problemaDeLaCarpetaDeMedios(uidPropietario: number, sePudoAbrir: boolean): string | null {
  if (sePudoAbrir) {
    return null;
  }

  if (uidPropietario === UID_DE_LA_API) {
    // No la puedo abrir yo, pero no hace falta: el que escribe dentro es el
    // dueño. Es el caso normal de una worktree con corridas anteriores.
    return null;
  }

  return (
    `El propietario es el UID ${uidPropietario}, que no es el de la API (${UID_DE_LA_API}),\n` +
    'y tampoco se puede abrir en escritura desde aquí. La API no podría escribir\n' +
    'dentro, así que toda subida respondería 500 sin decir por qué.\n' +
    (uidPropietario === 0
      ? 'La creó docker como root, que es lo que hace cuando la carpeta no existe.\n'
      : '')
  );
}
