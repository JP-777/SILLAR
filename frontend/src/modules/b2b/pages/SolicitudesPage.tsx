import { useCallback, useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { PageContainer } from '../../../layout/PageContainer';
import { ForbiddenPage } from '../../../platform/ForbiddenPage';
import { describe, type Failure } from '../../../shared/errors/messages';
import { useResource } from '../../../shared/hooks/useResource';
import { Badge, Button, EmptyState, Field, Input } from '../../../shared/ui';
import { Drawer, FailureAlert, Table, Toasts, useToasts, type Column } from '../../../shared/ui/patterns';
import { formatearFecha } from '../logica/formato';
import { NOMBRE_DE_ESTADO, esFinal, estadosSiguientes } from '../logica/solicitudes';
import { catalogo, cotizaciones, personalizaciones, volumen } from '../services/b2b';
import type { EstadoSolicitud, PersonalizacionDetalle, PersonalizacionEnBandeja, ProductoParaElegir, VolumenDetalle, VolumenEnBandeja } from '../services/contratos';
import '../b2b.css';

type Tipo = 'personalizacion' | 'volumen';
type Fila = PersonalizacionEnBandeja | VolumenEnBandeja;

const esPersonalizacion = (f: Fila): f is PersonalizacionEnBandeja => 'productName' in f;

/**
 * Bandejas de solicitudes (A1/A4) con su panel lateral (A2/A5) y el bloque A3:
 * estado, notas internas y reenlace.
 *
 * **Estructura, no superficie terminada:** la identidad humana del cliente
 * espera la costura de CRM. Aquí no se pinta ningún `customerId` ni se
 * sustituye por un texto de relleno.
 */
export function SolicitudesPage({ tipo }: { tipo: Tipo }) {
  const [filtro, setFiltro] = useState<EstadoSolicitud | ''>('');
  const cargar = useCallback(
    () => (tipo === 'personalizacion' ? personalizaciones.listar(filtro || undefined) : volumen.listar(filtro || undefined)) as Promise<Fila[]>,
    [tipo, filtro],
  );
  const { state, reload } = useResource(cargar, 'cargar las solicitudes');
  const [abierta, setAbierta] = useState<number | null>(null);
  const { toasts, show } = useToasts();

  if (state.status === 'forbidden') return <ForbiddenPage minimum="editor" />;

  const columnas: Column<Fila>[] = [
    {
      key: 'origen',
      header: tipo === 'personalizacion' ? 'Producto de origen' : 'Institución',
      render: (f) => (
        <span className="b2b-fila">
          {esPersonalizacion(f) ? f.productName : f.institutionName}
          {esPersonalizacion(f) && f.pendingRelink && <Badge tone="warning">Por reenlazar</Badge>}
        </span>
      ),
    },
    { key: 'descripcion', header: 'Qué pide', render: (f) => f.description },
    { key: 'estado', header: 'Estado', render: (f) => <Badge>{NOMBRE_DE_ESTADO[f.status]}</Badge> },
    { key: 'fecha', header: 'Recibida', render: (f) => formatearFecha(f.createdAt) },
    { key: 'abrir', header: '', align: 'right', render: (f) => <Button size="sm" variant="ghost" onClick={() => setAbierta(f.id)}>Abrir</Button> },
  ];

  const filas = state.status === 'ready' ? state.data.filter((f) => f.isActive) : [];

  return (
    <PageContainer
      title={tipo === 'personalizacion' ? 'Solicitudes personalizadas' : 'Solicitudes por volumen'}
      description={tipo === 'personalizacion' ? 'Encargos que cambian un producto de la tienda.' : 'Pedidos donde la cantidad cambia el precio.'}
    >
      <div className="b2b-pila">
        <Field label="Estado">
          {(props) => (
            <select className="ui-input" {...props} value={filtro} onChange={(e) => setFiltro(e.target.value as EstadoSolicitud | '')}>
              <option value="">Todos</option>
              {(Object.keys(NOMBRE_DE_ESTADO) as EstadoSolicitud[]).map((e) => (
                <option key={e} value={e}>{NOMBRE_DE_ESTADO[e]}</option>
              ))}
            </select>
          )}
        </Field>
        {state.status === 'error' && <FailureAlert failure={state.failure} />}
        <Table
          columns={columnas}
          rows={filas}
          rowKey={(f) => f.id}
          loading={state.status === 'loading'}
          empty={<EmptyState title={filtro ? 'Ninguna solicitud en ese estado' : 'Todavía no hay solicitudes'} description={filtro ? 'Prueba con otro estado.' : 'Aparecerán aquí cuando un cliente las envíe.'} />}
        />
      </div>
      {abierta !== null && (
        <DetalleDeSolicitud
          tipo={tipo}
          id={abierta}
          onClose={() => setAbierta(null)}
          onCambio={(mensaje) => { show(mensaje); void reload(); }}
        />
      )}
      <Toasts toasts={toasts} />
    </PageContainer>
  );
}

/** A2/A5 con el bloque A3. Ante un 409 conserva lo escrito y enseña la frase del backend. */
function DetalleDeSolicitud({ tipo, id, onClose, onCambio }: { tipo: Tipo; id: number; onClose: () => void; onCambio: (mensaje: string) => void }) {
  const cargar = useCallback(
    (): Promise<PersonalizacionDetalle | VolumenDetalle> => (tipo === 'personalizacion' ? personalizaciones.obtener(id) : volumen.obtener(id)),
    [tipo, id],
  );
  const { state, reload } = useResource(cargar, 'cargar la solicitud');
  const navegar = useNavigate();
  const [estadoNuevo, setEstadoNuevo] = useState<EstadoSolicitud | ''>('');
  const [notas, setNotas] = useState('');
  const [busqueda, setBusqueda] = useState('');
  const [resultados, setResultados] = useState<ProductoParaElegir[] | null>(null);
  const [fallo, setFallo] = useState<Failure | null>(null);
  const [ocupado, setOcupado] = useState(false);

  const detalle = state.status === 'ready' ? state.data : null;
  useEffect(() => { if (detalle) setNotas(detalle.staffNotes ?? ''); }, [detalle]);

  async function ejecutar(accion: () => Promise<unknown>, contexto: string, mensaje: string) {
    setOcupado(true);
    setFallo(null);
    try {
      await accion();
      onCambio(mensaje);
      await reload();
    } catch (error) {
      // Lo escrito se queda: solo se enseña por qué no se pudo.
      setFallo(describe(error, contexto));
    } finally {
      setOcupado(false);
    }
  }

  async function buscar() {
    setFallo(null);
    try {
      setResultados(await catalogo.productos(busqueda));
    } catch (error) {
      setFallo(describe(error, 'buscar productos'));
    }
  }

  async function crearCotizacion() {
    setOcupado(true);
    setFallo(null);
    try {
      const creada = await cotizaciones.crear(tipo, id);
      navegar(`/admin/solicitudes/cotizaciones/${creada.detalle.cotizacion.id}`);
    } catch (error) {
      setFallo(describe(error, 'crear la cotización'));
      setOcupado(false);
    }
  }

  const s = detalle?.solicitud;
  const siguientes = s ? estadosSiguientes(s.status) : [];

  return (
    <Drawer open title={s ? (esPersonalizacion(s) ? s.productName : s.institutionName) : 'Solicitud'} onClose={onClose}>
      {state.status === 'loading' && <p className="b2b-dato">Cargando la solicitud…</p>}
      {state.status === 'error' && <FailureAlert failure={state.failure} />}
      {s && (
        <div className="b2b-pila">
          <FailureAlert failure={fallo} />
          <section className="b2b-seccion">
            <p>{s.description}</p>
            <p className="b2b-dato">
              {esPersonalizacion(s)
                ? `${s.quantity ? `${s.quantity} unidades · ` : ''}${s.neededBy ? `para el ${s.neededBy}` : 'sin fecha'}`
                : `${s.quantity} unidades${s.eventDate ? ` · para el ${s.eventDate}` : ''}${s.contactPerson ? ` · contacto: ${s.contactPerson}` : ''}${s.institutionDocument ? ` · RUC ${s.institutionDocument}` : ''}`}
            </p>
          </section>

          <section className="b2b-seccion" aria-labelledby="b2b-estado">
            <h3 id="b2b-estado">Estado: {NOMBRE_DE_ESTADO[s.status]}</h3>
            {esFinal(s.status) ? (
              <p className="b2b-dato">Es un estado final: no admite más cambios.</p>
            ) : (
              <div className="b2b-fila">
                <Field label="Pasar a">
                  {(props) => (
                    <select className="ui-input" {...props} value={estadoNuevo} onChange={(e) => setEstadoNuevo(e.target.value as EstadoSolicitud)}>
                      <option value="">Elige un estado</option>
                      {siguientes.map((e) => <option key={e} value={e}>{NOMBRE_DE_ESTADO[e]}</option>)}
                    </select>
                  )}
                </Field>
                <Button
                  disabled={!estadoNuevo || ocupado}
                  onClick={() => estadoNuevo && ejecutar(
                    () => (tipo === 'personalizacion' ? personalizaciones.cambiarEstado(id, estadoNuevo) : volumen.cambiarEstado(id, estadoNuevo)),
                    'cambiar el estado',
                    `La solicitud pasó a ${NOMBRE_DE_ESTADO[estadoNuevo].toLowerCase()}.`,
                  ).then(() => setEstadoNuevo(''))}
                >
                  Cambiar estado
                </Button>
              </div>
            )}
          </section>

          <section className="b2b-seccion">
            <Field label="Notas internas" hint="Solo las ve el personal en este panel. Nunca llegan al cliente.">
              {(props) => <textarea className="ui-input" rows={4} {...props} value={notas} onChange={(e) => setNotas(e.target.value)} />}
            </Field>
            <div>
              <Button variant="secondary" disabled={ocupado}
                onClick={() => ejecutar(
                  () => (tipo === 'personalizacion' ? personalizaciones.guardarNotas(id, notas) : volumen.guardarNotas(id, notas)),
                  'guardar las notas', 'Notas internas guardadas.')}
              >
                Guardar notas
              </Button>
            </div>
          </section>

          {esPersonalizacion(s) && (
            <section className="b2b-seccion" aria-labelledby="b2b-reenlace">
              <h3 id="b2b-reenlace">Producto de origen</h3>
              <p className="b2b-dato">
                {s.pendingRelink ? 'El producto de origen se dio de baja. Elige uno activo para seguir cotizando.' : `Enlazada a «${s.productName}».`}
              </p>
              <div className="b2b-fila">
                <Field label="Buscar producto activo">
                  {(props) => <Input {...props} value={busqueda} onChange={(e) => setBusqueda(e.target.value)} />}
                </Field>
                <Button variant="secondary" disabled={busqueda.trim() === ''} onClick={() => void buscar()}>Buscar</Button>
              </div>
              {resultados !== null && (resultados.length === 0 ? (
                <p className="b2b-dato">Ningún producto activo coincide con «{busqueda}».</p>
              ) : (
                <ul className="b2b-lista">
                  {resultados.map((p) => (
                    <li key={p.productId} className="b2b-fila">
                      <span>{p.name}</span>
                      {!p.isPublic && <Badge tone="warning">No publicado</Badge>}
                      <Button size="sm" disabled={ocupado}
                        onClick={() => ejecutar(() => personalizaciones.reenlazar(id, p.productId), 'reenlazar el producto', `Reenlazada a «${p.name}».`)
                          .then(() => setResultados(null))}
                      >
                        Reenlazar a este producto
                      </Button>
                    </li>
                  ))}
                </ul>
              ))}
            </section>
          )}

          {!esFinal(s.status) && (
            <section className="b2b-seccion">
              <div>
                <Button onClick={() => void crearCotizacion()} disabled={ocupado}>Crear cotización</Button>
              </div>
            </section>
          )}
        </div>
      )}
    </Drawer>
  );
}
