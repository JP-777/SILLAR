import { isApiError } from '../shared/http/errors';

/**
 * Cómo contar un fallo **sin afirmar una causa que no se conoce**.
 *
 * H03 y H22 son el mismo defecto en dos pantallas: un texto fijo que atribuye
 * a una causa concreta —el instalador, los módulos— fallos que pueden venir de
 * otro sitio. Aquí se describe cada fallo **por lo que se sabe de él**: qué
 * pregunta falló y de qué forma (sin contacto, respuesta sin explicación,
 * respuesta explicada). Nada más.
 *
 * Son funciones puras a propósito: así se prueban sin navegador
 * (`tests/describirFallos.test.mjs`).
 *
 * **Las frases son provisionales.** Las pruebas exigen propiedades —que dos
 * situaciones distintas no se cuenten igual, que no se afirme lo que no se
 * sabe—, no redacción. Cómo suenan es decisión de JP.
 */

export interface DescripcionDeFallo {
  titulo: string;
  mensaje: string | null;
}

/** La explicación del servidor, si la hay y está escrita para leerse. */
function explicacionDelServidor(error: unknown): string | null {
  if (!isApiError(error) || !error.explainedByServer) {
    return null;
  }

  // `serverMessage` ya excluye lo que no debe llegar a una pantalla (el 500
  // del framework, H-01).
  return error.serverMessage;
}

/** El `POST /api/setup` falló (H03). */
export function describirFalloDeInstalacion(error: unknown): DescripcionDeFallo {
  if (isApiError(error, 'Network')) {
    return {
      titulo: 'No se pudo contactar con el servidor',
      mensaje: 'La instalación no llegó a enviarse. Comprueba que el servicio esté en marcha y vuelve a intentarlo.',
    };
  }

  if (isApiError(error) && error.status >= 500) {
    const explicacion = explicacionDelServidor(error);

    if (explicacion === null) {
      // No explicó nada: no se sabe por qué no atendió. No es «no se pudo
      // instalar», que atribuiría al instalador algo que puede ser un proxy o
      // un servicio caído.
      return {
        titulo: 'El servidor no está disponible',
        mensaje: `Respondió con un error (${error.status}) sin explicar el motivo. Los datos del formulario no tienen por qué ser la causa.`,
      };
    }

    // Lo explicó —el 503 del instalador cuando el destino no es seguro—: su
    // título ya dice qué pasa, y es mejor que cualquiera nuestro.
    return { titulo: error.message, mensaje: error.detail };
  }

  return {
    titulo: 'No se pudo instalar',
    mensaje: isApiError(error) ? error.displayMessage : 'No se pudo completar la instalación.',
  };
}

/** Qué pregunta del arranque falló. */
export type PasoDeArranque = 'estado' | 'capacidades' | 'sesion';

export interface DescripcionDeArranque {
  /** Qué no se pudo hacer. Lo único que se sabe con certeza. */
  que: string;
  /** De qué forma falló, si se sabe. */
  como: string | null;
  /** Qué puede hacer la persona, sin inventar causa. */
  pista: string | null;
}

const QUE: Record<PasoDeArranque, string> = {
  estado: 'No se pudo comprobar en qué estado está el servidor.',
  capacidades: 'No se pudo consultar qué módulos están activos.',
  sesion: 'No se pudo comprobar la sesión.',
};

/** El arranque de la aplicación falló (H22). */
export function describirFalloDeArranque(paso: PasoDeArranque, error: unknown): DescripcionDeArranque {
  if (isApiError(error, 'Network')) {
    return {
      que: QUE[paso],
      como: 'El servidor no respondió.',
      // Sin «si acabas de activar un módulo»: tras una recarga no hay forma de
      // saber si alguien lo hizo, y afirmarlo manda a buscar donde no es.
      pista: 'Comprueba que el servicio esté en marcha. Si se estaba reiniciando, espera unos segundos y vuelve a intentarlo.',
    };
  }

  if (isApiError(error)) {
    return {
      que: QUE[paso],
      como: explicacionDelServidor(error)
        ?? (error.status >= 500 ? `El servidor respondió con un error (${error.status}).` : error.displayMessage),
      pista: null,
    };
  }

  return { que: QUE[paso], como: null, pista: null };
}
