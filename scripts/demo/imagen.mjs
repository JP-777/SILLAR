import { deflateSync } from 'node:zlib';

/**
 * Genera un PNG de verdad, en memoria.
 *
 * **Los archivos de apoyo se generan, no se commitean.** Es la misma regla que
 * ya sigue el arnés e2e: veinte imágenes de demostración en el historial de
 * git entran para siempre, y generarlas en disco obligaría a limpiarlas
 * después. Así no hay ni una cosa ni la otra.
 *
 * No es una foto: es un fondo con un degradado suave y una banda diagonal, en
 * el color que se le pida. Basta para que la rejilla del catálogo se vea como
 * un catálogo y no como una maqueta, y **deja ver de paso que el sistema
 * acepta imágenes de verdad** — el servidor comprueba el contenido, no la
 * extensión (ADR-011), así que un archivo inventado no pasaría.
 */

/** CRC-32, que es lo único que un PNG pide y `zlib` no da hecho. */
const TABLA_CRC = (() => {
  const tabla = new Int32Array(256);

  for (let n = 0; n < 256; n += 1) {
    let c = n;
    for (let k = 0; k < 8; k += 1) {
      c = c & 1 ? 0xedb88320 ^ (c >>> 1) : c >>> 1;
    }
    tabla[n] = c;
  }

  return tabla;
})();

function crc32(buffer) {
  let c = 0xffffffff;

  for (const byte of buffer) {
    c = TABLA_CRC[(c ^ byte) & 0xff] ^ (c >>> 8);
  }

  return (c ^ 0xffffffff) >>> 0;
}

/** Un trozo de PNG: longitud, tipo, datos y su CRC. */
function chunk(tipo, datos) {
  const longitud = Buffer.alloc(4);
  longitud.writeUInt32BE(datos.length);

  const cuerpo = Buffer.concat([Buffer.from(tipo, 'ascii'), datos]);
  const crc = Buffer.alloc(4);
  crc.writeUInt32BE(crc32(cuerpo));

  return Buffer.concat([longitud, cuerpo, crc]);
}

/** «#1E5AA8» a sus tres componentes. */
function componentes(hex) {
  const limpio = hex.replace('#', '');

  return [
    parseInt(limpio.slice(0, 2), 16),
    parseInt(limpio.slice(2, 4), 16),
    parseInt(limpio.slice(4, 6), 16),
  ];
}

/**
 * Un PNG cuadrado con el color que se le pase.
 *
 * @param {string} hex Color base, en «#rrggbb».
 * @param {number} lado Píxeles de lado. 600 es de sobra para una tarjeta.
 * @returns {Buffer} El archivo entero, listo para subir.
 */
export function png(hex, lado = 600) {
  const [r, g, b] = componentes(hex);

  // Una fila de PNG empieza por su byte de filtro; se usa 0, «ninguno»,
  // porque comprimir mejor no vale la complicación para esto.
  const filas = [];

  for (let y = 0; y < lado; y += 1) {
    const fila = Buffer.alloc(lado * 3 + 1);
    let i = 1;

    for (let x = 0; x < lado; x += 1) {
      // Degradado diagonal suave, más claro arriba a la izquierda.
      const t = (x + y) / (lado * 2);
      const claro = 1 - t * 0.55;

      // Y una banda diagonal apenas más oscura, para que no parezca un
      // rectángulo de color plano.
      const banda = Math.abs(((x - y) % 180) - 90) < 14 ? 0.9 : 1;

      fila[i] = Math.round(Math.min(255, r * claro * banda + 40 * (1 - t)));
      fila[i + 1] = Math.round(Math.min(255, g * claro * banda + 40 * (1 - t)));
      fila[i + 2] = Math.round(Math.min(255, b * claro * banda + 40 * (1 - t)));
      i += 3;
    }

    filas.push(fila);
  }

  const ihdr = Buffer.alloc(13);
  ihdr.writeUInt32BE(lado, 0);
  ihdr.writeUInt32BE(lado, 4);
  ihdr[8] = 8; // 8 bits por componente
  ihdr[9] = 2; // color verdadero, sin alfa
  ihdr[10] = 0; // deflate
  ihdr[11] = 0; // filtrado adaptativo
  ihdr[12] = 0; // sin entrelazado

  return Buffer.concat([
    Buffer.from([0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a]),
    chunk('IHDR', ihdr),
    chunk('IDAT', deflateSync(Buffer.concat(filas), { level: 9 })),
    chunk('IEND', Buffer.alloc(0)),
  ]);
}
