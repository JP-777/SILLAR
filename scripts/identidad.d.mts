/**
 * Tipos de `identidad.mjs`, escritos a mano.
 *
 * **Por qué a mano y por qué el módulo es `.mjs` y no `.ts`.** Lo consumen dos
 * mundos que no comparten compilador: el arnés e2e, que es TypeScript, y
 * `scripts/estrenar.mjs`, que es un guion de Node que se ejecuta sin paso de
 * compilación. Escribirlo dos veces sería reintroducir el defecto que este
 * módulo existe para quitar —un valor en dos sitios—, así que la lógica vive
 * una sola vez, en JavaScript, y esto declara su forma.
 *
 * Lo que se duplica aquí es la **forma**, no la decisión: si divergen, la
 * etapa 2 de la puerta lo dice al comprobar los tipos del arnés.
 */

export const ARBOLES_POSIBLES: number;

export function sufijoDeWorktree(dir: string): string;

export function offsetDeSufijo(sufijo: string): number;

export interface IdentidadDeDesarrollo {
  proyecto: string;
  base: string;
  nodo: string;
  puertoDb: number;
  puertoApi: number;
  puertoPgadmin: number;
}

export interface IdentidadE2e {
  proyecto: string;
  base: string;
  puertoDb: number;
  puertoApi: number;
  puertoFrontend: number;
}

export interface Identidad {
  dir: string;
  sufijo: string;
  offset: number;
  dev: IdentidadDeDesarrollo;
  e2e: IdentidadE2e;
}

export function identidadDeLaWorktree(dir: string): Identidad;

export function cadenaDeConexion(datos: {
  puerto: number;
  base: string;
  usuario: string;
  contrasena: string;
}): string;
