/**
 * Qué había en pantalla cuando `page.goto` no encontró el ancla del armazón.
 *
 * El envoltorio de `fixtures/base.ts` espera `a.pf-skip`, que solo existe
 * cuando el arranque termina en `ready` (`frontend/src/app/App.tsx`). Antes, si
 * no aparecía, el mensaje era siempre «no llegó a pintar», y eso confundía
 * cuatro situaciones distintas: la aplicación sí pintó, pero una página de
 * error, la de migraciones o el asistente; o no terminó de arrancar; o el
 * renderizador dejó de responder.
 *
 * **Esto no cambia qué se aprueba.** El envoltorio sigue fallando igual y con
 * el mismo plazo; solo dice por qué.
 *
 * **Sin datos sensibles:** de la URL solo el `pathname` —la consulta puede
 * llevar tokens de un solo uso—, y de la pantalla solo títulos fijos del
 * producto y el primer aviso, recortado. Nunca el DOM ni los campos.
 */

/** Lo que se observa en el documento. Todo opcional: puede no haberse podido leer. */
export interface ObservacionDeArranque {
  /** `location.pathname`, sin consulta ni fragmento. */
  ruta: string;
  /** El indicador de arranque (`.pf-boot`) sigue en pantalla. */
  indicadorDeCarga: boolean;
  /** Título de la tarjeta central (`.pf-centered .ui-card__title`), si la hay. */
  tituloDeTarjeta: string | null;
  /** Texto del primer aviso (`[role="alert"]`) dentro de esa tarjeta, si lo hay. */
  aviso: string | null;
  /** El formulario del asistente de instalación está montado. */
  formularioDeInstalacion: boolean;
  /** El contenedor de React está vacío. */
  raizVacia: boolean;
}

export type EstadoDeArranque =
  | 'arranque-sin-terminar'
  | 'pagina-de-error'
  | 'migraciones-pendientes'
  | 'asistente-de-instalacion'
  | 'renderizador-sin-respuesta'
  | 'nada-montado'
  | 'otro';

export interface DiagnosticoDeArranque {
  estado: EstadoDeArranque;
  ruta: string | null;
  explicacion: string;
  titulo?: string;
  aviso?: string;
  errorAlLeer?: string;
}

/** Títulos fijos del producto (`PlatformErrorPage.tsx`, `SetupPage.tsx`). */
export const TITULO_ERROR_DE_PLATAFORMA = 'No se pudo cargar el sistema';
export const TITULO_MIGRACIONES = 'Base de datos no preparada';

const LIMITE_AVISO = 200;

function recortar(texto: string | null): string | undefined {
  if (!texto) return undefined;
  const limpio = texto.replace(/\s+/g, ' ').trim();
  return limpio.length > LIMITE_AVISO ? `${limpio.slice(0, LIMITE_AVISO)}…` : limpio;
}

/** Clasifica una observación. Función pura: se prueba sin navegador. */
export function clasificarArranque(
  observacion: ObservacionDeArranque | null,
  errorAlLeer?: string,
): DiagnosticoDeArranque {
  if (!observacion) {
    return {
      estado: 'renderizador-sin-respuesta',
      ruta: null,
      explicacion:
        'No se pudo leer el documento: el renderizador no respondió o se cayó. '
        + 'Revisar memoria del equipo y eventos de caída de página.',
      errorAlLeer,
    };
  }

  const { ruta } = observacion;

  if (observacion.tituloDeTarjeta === TITULO_ERROR_DE_PLATAFORMA) {
    return {
      estado: 'pagina-de-error',
      ruta,
      explicacion:
        'La aplicación sí pintó: el arranque terminó en la página de error de plataforma. '
        + 'Una de sus preguntas iniciales (estado, capacidades o sesión) falló.',
      titulo: observacion.tituloDeTarjeta,
      aviso: recortar(observacion.aviso),
    };
  }

  if (observacion.tituloDeTarjeta === TITULO_MIGRACIONES) {
    return {
      estado: 'migraciones-pendientes',
      ruta,
      explicacion: 'La aplicación sí pintó: el servidor informa de migraciones pendientes.',
      titulo: observacion.tituloDeTarjeta,
    };
  }

  if (observacion.formularioDeInstalacion) {
    return {
      estado: 'asistente-de-instalacion',
      ruta,
      explicacion:
        'La aplicación sí pintó: el servidor pide instalar. El envoltorio acepta '
        + '`[data-pantalla="instalacion"]` para este caso, pero ese atributo no existe en el frontend.',
    };
  }

  if (observacion.indicadorDeCarga) {
    return {
      estado: 'arranque-sin-terminar',
      ruta,
      explicacion:
        'El arranque no terminó: sigue el indicador «Cargando». '
        + 'Alguna pregunta inicial al servidor no ha respondido.',
    };
  }

  if (observacion.raizVacia) {
    return {
      estado: 'nada-montado',
      ruta,
      explicacion: 'React no montó nada: el contenedor de la aplicación está vacío.',
    };
  }

  return {
    estado: 'otro',
    ruta,
    explicacion: 'Hay algo pintado que no es el armazón ni una pantalla de plataforma conocida.',
    titulo: observacion.tituloDeTarjeta ?? undefined,
  };
}
