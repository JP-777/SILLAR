import type { CotizacionEnBandeja, EstadoCotizacion, EvaluacionMayorista, MetodoDePago } from '../services/contratos';

/**
 * Lógica pura del ciclo de cotización en el panel (A8/A9). Sin React ni red:
 * la prueban las focales con `node --test`, y los componentes solo la consultan.
 */

/** Lo que el panel ofrece sobre una cotización, según su estado y el rol. */
export interface AccionesDeCotizacion {
  editarLineas: boolean;
  enviar: boolean;
  aprobar: boolean;
  prepararParaEnviar: boolean;
  crearNuevaDesdeSolicitud: boolean;
  registrarPago: boolean;
  darDeBaja: boolean;
}

const NINGUNA: AccionesDeCotizacion = {
  editarLineas: false,
  enviar: false,
  aprobar: false,
  prepararParaEnviar: false,
  crearNuevaDesdeSolicitud: false,
  registrarPago: false,
  darDeBaja: false,
};

/**
 * Tabla de A9 del paquete de diseño aprobado. **Las acciones de `admin` no se
 * devuelven al editor** —no se muestran deshabilitadas—. **No existe «Anular»**:
 * el contrato HTTP no la ofrece, así que ni se modela.
 */
export function accionesDeCotizacion(
  cotizacion: Pick<CotizacionEnBandeja, 'status' | 'invalidatedAt' | 'isActive'>,
  lineas: number,
  esAdmin: boolean,
): AccionesDeCotizacion {
  if (!cotizacion.isActive) return NINGUNA;
  const invalidada = cotizacion.invalidatedAt !== null;

  switch (cotizacion.status) {
    case 'borrador':
      return { ...NINGUNA, editarLineas: true, enviar: lineas >= 1, darDeBaja: esAdmin };
    case 'enviada':
      return invalidada
        ? { ...NINGUNA, crearNuevaDesdeSolicitud: true, darDeBaja: esAdmin }
        : { ...NINGUNA, aprobar: true, prepararParaEnviar: true, darDeBaja: esAdmin };
    case 'aprobada':
      return { ...NINGUNA, registrarPago: esAdmin, darDeBaja: esAdmin };
    case 'pagada':
      return { ...NINGUNA, darDeBaja: esAdmin };
    case 'anulada':
      return NINGUNA;
  }
}

const ETIQUETAS: Record<EstadoCotizacion, string> = {
  borrador: 'Borrador',
  enviada: 'Enviada',
  aprobada: 'Aprobada',
  pagada: 'Pagada',
  anulada: 'Anulada',
};

/**
 * El estado se pinta tal cual, y la invalidación **aparte**: una enviada que
 * caducó sigue diciendo «Enviada», con la condición «Ya no es válida» al lado.
 */
export function presentacionDeEstado(cotizacion: Pick<CotizacionEnBandeja, 'status' | 'invalidatedAt'>): {
  etiqueta: string;
  condicion: string | null;
} {
  return {
    etiqueta: ETIQUETAS[cotizacion.status],
    condicion: cotizacion.status === 'enviada' && cotizacion.invalidatedAt !== null ? 'Ya no es válida' : null,
  };
}

/** Los métodos que se ofrecen. Tarjeta no: llega con M11. */
export const METODOS_DE_PAGO: readonly { valor: MetodoDePago; etiqueta: string; exigeReferencia: boolean }[] = [
  { valor: 'yape', etiqueta: 'Yape', exigeReferencia: true },
  { valor: 'efectivo', etiqueta: 'Efectivo', exigeReferencia: false },
];

/** Qué impide registrar el pago, o `null` si nada. */
export function errorDePago(metodo: MetodoDePago | null, referencia: string): string | null {
  const opcion = METODOS_DE_PAGO.find((m) => m.valor === metodo);
  if (!opcion) return 'Elige cómo se pagó.';
  if (opcion.exigeReferencia && referencia.trim() === '') return 'Un pago con Yape necesita el código de operación.';
  return null;
}

/** El texto de la evaluación mayorista. Informa; **nunca** aplica descuento. */
export function textoMayorista(evaluacion: EvaluacionMayorista): { titulo: string; texto: string } {
  switch (evaluacion.estado) {
    case 'alcanza':
      return { titulo: 'Umbral mayorista', texto: 'A precio de lista, esta cotización alcanza el umbral mayorista.' };
    case 'no_alcanza':
      return { titulo: 'Umbral mayorista', texto: 'A precio de lista, esta cotización no alcanza el umbral mayorista.' };
    case 'no_evaluable':
      return {
        titulo: 'Umbral mayorista no calculable',
        texto: 'Hay líneas sin precio de catálogo, así que el umbral no se puede calcular: decide con tu criterio.',
      };
    case 'configuracion_pendiente':
      return {
        titulo: 'Umbral mayorista sin configurar',
        texto: 'Falta configurar el umbral mayorista. Mientras tanto, decide con tu criterio.',
      };
  }
}
