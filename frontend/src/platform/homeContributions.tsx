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
 * ---
 *
 * **Por qué hay un estado «cargando» y no dos valores.** Sin él, el aviso
 * aparecería en el hueco entre montar la portada y recibir los datos: «Sitio
 * en construcción» durante 200 ms es peor que no decir nada, y encima sería
 * mentira. Mientras alguien siga esperando, el armazón no afirma que no hay
 * nada — porque todavía no lo sabe.
 *
 * Es la misma regla que la doctrina de animaciones aplica a los indicadores:
 * lo que aún no se sabe no se cuenta.
 *
 * ---
 *
 * **El armazón sigue sin conocer a ningún módulo, y esto no lo rompe.** No hay
 * códigos de módulo aquí ni forma de preguntar «¿cuánto aporta CMS?»: cada
 * bloque se registra con una clave que se da a sí mismo y declara en cuál de
 * los tres estados está. Por eso una sección puede traer **un bloque o cuatro**
 * sin que el armazón se entere ni tenga que enterarse: `cmsHome` registra
 * cuatro aportes y `catalogHome` uno, y el resumen se calcula igual.
 */
export type EstadoAporte = 'cargando' | 'vacio' | 'con-contenido';

interface Registro {
  readonly declarar: (clave: string, estado: EstadoAporte) => void;
  readonly retirar: (clave: string) => void;
}

/**
 * Fuera del proveedor, declarar no hace nada.
 *
 * **A propósito, y no es dejadez:** una sección de portada también se usa en
 * pruebas de componente y en cualquier pantalla futura que quiera reutilizarla.
 * Que reventara fuera de la portada la haría inservible en cualquier otro
 * sitio, y el único perjudicado sería quien la reutilice.
 */
const SIN_REGISTRO: Registro = { declarar: () => {}, retirar: () => {} };

const RegistroContext = createContext<Registro>(SIN_REGISTRO);
const AportesContext = createContext<readonly EstadoAporte[]>([]);

/** Envuelve la portada para que sus secciones puedan declarar qué pintaron. */
export function HomeContributions({ children }: { children: ReactNode }) {
  const [aportes, setAportes] = useState<Record<string, EstadoAporte>>({});

  const declarar = useCallback((clave: string, estado: EstadoAporte) => {
    // Se compara antes de escribir: sin esto, cada render de un bloque
    // estable dispararía un render del armazón, y el armazón vuelve a
    // renderizar el bloque. El bucle no sería infinito por suerte, sino por
    // que React corta cuando el estado es idéntico — y eso es una suerte que
    // no conviene depender de ella.
    setAportes((previos) => (previos[clave] === estado ? previos : { ...previos, [clave]: estado }));
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
 * Lo declara un bloque de portada en cada render: en cuál de los tres estados
 * está lo suyo.
 *
 * La clave la genera `useId`, así que **ningún módulo tiene que inventarse un
 * nombre único** ni coordinarse con otro. Dos instancias del mismo bloque
 * cuentan como dos aportes, que es lo correcto.
 */
export function useHomeContribution(estado: EstadoAporte): void {
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

/**
 * En qué ha quedado la portada, según lo que declararon sus bloques.
 *
 * El orden de las tres reglas es la decisión entera:
 *
 * 1. **Si alguien pintó algo, hay contenido**, aunque otros seis estén vacíos.
 * 2. **Si nadie pintó pero alguien espera, todavía no se sabe.** No se afirma
 *    que no hay nada mientras quede una petición en vuelo.
 * 3. **Y si nadie pintó y nadie espera, es que no hay nada.**
 *
 * El caso de cero aportes cuenta como «cargando» y no como «vacío», porque es
 * lo que se ve **en el primer render**: los efectos que registran corren
 * después de pintar, así que un array vacío significa «todavía no han hablado»,
 * no «han dicho que no tienen nada». Sin esta línea el aviso parpadea una vez
 * en cada carga de la portada.
 */
export function useHomeState(): EstadoAporte {
  const aportes = useContext(AportesContext);

  if (aportes.includes('con-contenido')) {
    return 'con-contenido';
  }

  return aportes.length === 0 || aportes.includes('cargando') ? 'cargando' : 'vacio';
}
