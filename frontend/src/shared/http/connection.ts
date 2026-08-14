/**
 * Estado de la conexión con el servidor.
 *
 * Vive FUERA de React a propósito. Activar o desactivar un módulo detiene el
 * host y lo relanza el orquestador (entrega 3 §1), y el documento de F-08 §8 lo
 * dice sin rodeos: ese estado es global, no local a la pantalla de módulos.
 *
 * Que viva aquí compra tres cosas que una implementación dentro de un componente
 * no daría:
 *
 * 1. El cliente HTTP lo consulta antes de cada envío, así que durante el
 *    reinicio no sale ninguna petición. No depende de que ninguna pantalla se
 *    acuerde de comprobarlo.
 * 2. Si el usuario navega, el estado sigue ahí. Nunca estuvo montado en una
 *    pantalla que pudiera desmontarse.
 * 3. Cualquier fallo de red, venga de donde venga, entra por el mismo sitio.
 */

/** Situación de la conexión. */
export type ConnectionState =
  /** Todo normal. */
  | 'online'
  /** El servidor no responde y se está sondeando. La interfaz queda bloqueada. */
  | 'reconnecting'
  /** Se agotó el plazo de espera. Hace falta reintentar a mano. */
  | 'failed';

/** Lo que ven los suscriptores. */
export interface ConnectionStatus {
  readonly state: ConnectionState;
  /** Segundos que lleva reconectando, para poder decirlo en pantalla. */
  readonly elapsedSeconds: number;
  /** Qué provocó el reinicio, si lo provocó una operación conocida. */
  readonly reason: string | null;
}

/**
 * Espera entre sondeos, en milisegundos. El último valor se repite.
 *
 * Empieza corto porque un reinicio de este host tarda pocos segundos, y se
 * espacia para no castigar a un servidor que está arrancando.
 */
const RETRY_DELAYS_MS = [1000, 2000, 3000, 5000] as const;

/** Plazo tras el cual se deja de insistir y se pide intervención. */
const GIVE_UP_AFTER_MS = 60_000;

/** Ruta que se sondea: pública, barata y la que dice si el host ya está listo. */
const PROBE_PATH = '/api/capabilities';

type Listener = (status: ConnectionStatus) => void;

let status: ConnectionStatus = { state: 'online', elapsedSeconds: 0, reason: null };
const listeners = new Set<Listener>();

let probeTimer: ReturnType<typeof setTimeout> | null = null;
let tickTimer: ReturnType<typeof setInterval> | null = null;
let startedAt = 0;
let attempt = 0;
let onRecovered: (() => void | Promise<void>) | null = null;

function publish(next: Partial<ConnectionStatus>): void {
  status = { ...status, ...next };
  for (const listener of listeners) {
    listener(status);
  }
}

function clearTimers(): void {
  if (probeTimer !== null) {
    clearTimeout(probeTimer);
    probeTimer = null;
  }

  if (tickTimer !== null) {
    clearInterval(tickTimer);
    tickTimer = null;
  }
}

/** Pregunta al servidor si ya volvió. */
async function probe(): Promise<boolean> {
  try {
    // Sin caché: un 200 guardado de antes del reinicio diría que el servidor
    // está listo cuando todavía no lo está.
    const response = await fetch(PROBE_PATH, {
      credentials: 'same-origin',
      cache: 'no-store',
      headers: { Accept: 'application/json' },
    });

    return response.ok;
  } catch {
    return false;
  }
}

function scheduleProbe(): void {
  const delay = RETRY_DELAYS_MS[Math.min(attempt, RETRY_DELAYS_MS.length - 1)];
  attempt += 1;

  probeTimer = setTimeout(async () => {
    if (status.state !== 'reconnecting') {
      return;
    }

    if (await probe()) {
      await recover();
      return;
    }

    if (Date.now() - startedAt >= GIVE_UP_AFTER_MS) {
      clearTimers();
      publish({ state: 'failed' });
      return;
    }

    scheduleProbe();
  }, delay);
}

async function recover(): Promise<void> {
  clearTimers();

  // Se refrescan capacidades y sesión ANTES de desbloquear: si se desbloqueara
  // primero, la interfaz volvería mostrando el estado anterior al cambio, que es
  // justo lo que el usuario acaba de modificar.
  try {
    await onRecovered?.();
  } catch {
    // Que el refresco falle no puede dejar la aplicación bloqueada para
    // siempre: el servidor respondió, así que se sigue.
  }

  publish({ state: 'online', elapsedSeconds: 0, reason: null });
}

export const connection = {
  /** Estado actual. */
  get current(): ConnectionStatus {
    return status;
  },

  /** Indica si conviene abstenerse de lanzar peticiones. */
  get isDown(): boolean {
    return status.state !== 'online';
  },

  /** Se suscribe a los cambios. Devuelve la baja. */
  subscribe(listener: Listener): () => void {
    listeners.add(listener);
    listener(status);
    return () => listeners.delete(listener);
  },

  /**
   * Qué hacer cuando el servidor vuelva.
   *
   * Lo registra la aplicación al arrancar: refrescar capacidades y sesión. El
   * almacén no sabe hacer eso ni tiene por qué.
   */
  onRecovered(handler: () => void | Promise<void>): void {
    onRecovered = handler;
  },

  /**
   * Anuncia que el servidor va a reiniciarse por una operación conocida.
   *
   * La llama la pantalla que activa o desactiva un módulo, después de recibir su
   * 200. El sondeo empieza ya, sin esperar a que falle una petición.
   */
  expectRestart(reason: string): void {
    if (status.state === 'reconnecting') {
      return;
    }

    clearTimers();
    startedAt = Date.now();
    attempt = 0;
    publish({ state: 'reconnecting', elapsedSeconds: 0, reason });

    tickTimer = setInterval(() => {
      publish({ elapsedSeconds: Math.round((Date.now() - startedAt) / 1000) });
    }, 1000);

    scheduleProbe();
  },

  /**
   * Anuncia que una petición no llegó a su destino.
   *
   * La llama el cliente HTTP ante un fallo de red. Puede ser un reinicio que
   * nadie anunció —alguien relanzó el servicio a mano— o una caída de verdad; el
   * sondeo distingue una cosa de la otra sin tener que adivinar.
   */
  reportNetworkFailure(): void {
    this.expectRestart('El servidor no responde.');
  },

  /** Reintenta a mano tras agotarse el plazo. */
  retryNow(): void {
    if (status.state !== 'failed') {
      return;
    }

    this.expectRestart(status.reason ?? 'Reintentando.');
  },

  /** Solo para pruebas y para el desmontaje de la aplicación. */
  reset(): void {
    clearTimers();
    attempt = 0;
    publish({ state: 'online', elapsedSeconds: 0, reason: null });
  },
};
