import {
  createContext,
  useCallback,
  useContext,
  useEffect,
  useId,
  useMemo,
  useState,
  type ReactNode,
} from 'react';
import { reducirAportes, type EstadoAporte } from './surfaceState';

/**
 * **La maquinaria de un registro de aportes, una sola vez.**
 *
 * La portada y el pie hacen la misma pregunta sobre superficies distintas:
 * quién ha pintado algo y quién todavía no lo sabe. La regla que resume esos
 * aportes ya vivía en un solo sitio (`surfaceState.ts`); lo que faltaba era
 * que la maquinaria tampoco se copiara, porque copiada son dos sitios donde
 * arreglar el mismo error y dos sitios que pueden separarse sin que nadie lo
 * note.
 *
 * Cada llamada crea **sus propios contextos**, así que los dos registros son
 * independientes: un enlace social publicado no le dice a la portada que hay
 * contenido, y una portada llena no hace aparecer un pie vacío. Son dos
 * preguntas distintas con una sola regla.
 */
interface Registro {
  readonly declarar: (clave: string, estado: EstadoAporte) => void;
  readonly retirar: (clave: string) => void;
}

/**
 * Fuera del proveedor, declarar no hace nada.
 *
 * **A propósito, y no es dejadez:** una contribución también se usa en pruebas
 * de componente y en cualquier pantalla futura que quiera reutilizarla. Que
 * reventara fuera de su superficie la haría inservible en cualquier otro
 * sitio, y el único perjudicado sería quien la reutilice.
 */
const SIN_REGISTRO: Registro = { declarar: () => {}, retirar: () => {} };

export interface RegistroDeSuperficie {
  /** Envuelve la superficie para que sus contribuciones puedan declarar. */
  readonly Proveedor: (props: { children: ReactNode }) => ReactNode;
  /** Lo declara una contribución en cada render: en cuál de los tres estados está. */
  readonly useAporte: (estado: EstadoAporte) => void;
  /** En qué ha quedado la superficie, según lo que declararon sus contribuciones. */
  readonly useEstado: () => EstadoAporte;
}

export function crearRegistroDeSuperficie(): RegistroDeSuperficie {
  const RegistroContext = createContext<Registro>(SIN_REGISTRO);
  const AportesContext = createContext<readonly EstadoAporte[]>([]);

  function Proveedor({ children }: { children: ReactNode }) {
    const [aportes, setAportes] = useState<Record<string, EstadoAporte>>({});

    const declarar = useCallback((clave: string, estado: EstadoAporte) => {
      // Se compara antes de escribir: sin esto, cada render de un bloque
      // estable dispararía un render del armazón, y el armazón vuelve a
      // renderizar el bloque. El bucle no sería infinito por suerte, sino
      // porque React corta cuando el estado es idéntico — y eso es una suerte
      // de la que no conviene depender.
      setAportes((previos) =>
        previos[clave] === estado ? previos : { ...previos, [clave]: estado },
      );
    }, []);

    const retirar = useCallback((clave: string) => {
      setAportes((previos) => {
        if (!(clave in previos)) {
          return previos;
        }

        const { [clave]: _retirado, ...resto } = previos;
        return resto;
      });
    }, []);

    const registro = useMemo(() => ({ declarar, retirar }), [declarar, retirar]);
    const valores = useMemo(() => Object.values(aportes), [aportes]);

    return (
      <RegistroContext.Provider value={registro}>
        <AportesContext.Provider value={valores}>{children}</AportesContext.Provider>
      </RegistroContext.Provider>
    );
  }

  /**
   * La clave la genera `useId`, así que **ninguna contribución tiene que
   * inventarse un nombre único** ni coordinarse con otra. Dos instancias del
   * mismo bloque cuentan como dos aportes, que es lo correcto.
   */
  function useAporte(estado: EstadoAporte): void {
    const clave = useId();
    const { declarar, retirar } = useContext(RegistroContext);

    useEffect(() => {
      declarar(clave, estado);
    }, [clave, declarar, estado]);

    // Separado del anterior a propósito: si fuera el `return` de aquel efecto,
    // **cada cambio de estado retiraría el aporte y lo volvería a poner**, y en
    // el hueco entre las dos cosas el resumen vería un aporte de menos. Retirar
    // es de desmontar, no de cambiar.
    useEffect(() => () => retirar(clave), [clave, retirar]);
  }

  function useEstado(): EstadoAporte {
    return reducirAportes(useContext(AportesContext));
  }

  return { Proveedor, useAporte, useEstado };
}
