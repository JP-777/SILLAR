import { useCallback, useEffect, useState } from 'react';
import { Link, useNavigate, useParams } from 'react-router-dom';
import { PageContainer } from '../../../layout/PageContainer';
import { ForbiddenPage } from '../../../platform/ForbiddenPage';
import { fetchPublicSettings } from '../../../platform/usePublicSettings';
import { describe, type Failure } from '../../../shared/errors/messages';
import { useResource } from '../../../shared/hooks/useResource';
import { Alert, Badge, Button, Field, Input } from '../../../shared/ui';
import { ConfirmDialog, Drawer, FailureAlert, Toasts, useToasts } from '../../../shared/ui/patterns';
import { useSession } from '../../../session';
import { METODOS_DE_PAGO, accionesDeCotizacion, errorDePago, presentacionDeEstado, textoMayorista } from '../logica/cotizacion';
import { formatearFecha, formatearImporte, precioDeCatalogo } from '../logica/formato';
import { aPeticion, desdeContrato, erroresDeLineas, lineaDeCatalogo, lineaLibre, moverLinea, totalCobrado, type LineaEditable } from '../logica/lineas';
import { catalogo, cotizaciones } from '../services/b2b';
import type { CotizacionPanel, MetodoDePago, PresentacionParaElegir } from '../services/contratos';
import '../b2b.css';

/**
 * Detalle y ciclo de una cotización (A9), con el editor de borrador (A8).
 * Lo que ve cada rol sale de `accionesDeCotizacion`: lo de `admin` no se
 * enseña al editor, y no existe «Anular».
 */
export function CotizacionPage() {
  const id = Number(useParams().id);
  const cargar = useCallback(() => cotizaciones.obtener(id), [id]);
  const { state, reload } = useResource(cargar, 'cargar la cotización');
  const ajustes = useResource(fetchPublicSettings, 'cargar la moneda');
  const moneda = ajustes.state.status === 'ready' ? ajustes.state.data.currency_code ?? null : null;
  const { hasRole } = useSession();
  const { toasts, show } = useToasts();
  const navegar = useNavigate();
  const [fallo, setFallo] = useState<Failure | null>(null);
  const [ocupado, setOcupado] = useState(false);
  const [pagando, setPagando] = useState(false);
  const [dandoDeBaja, setDandoDeBaja] = useState(false);
  const [preparando, setPreparando] = useState(false);

  if (state.status === 'forbidden') return <ForbiddenPage minimum="editor" />;
  if (state.status === 'loading') return <PageContainer title="Cotización"><p className="b2b-dato">Cargando la cotización…</p></PageContainer>;
  if (state.status === 'error') return <PageContainer title="Cotización"><FailureAlert failure={state.failure} /></PageContainer>;

  const panel = state.data;
  const c = panel.detalle.cotizacion;
  const acciones = accionesDeCotizacion(c, panel.detalle.lines.length, hasRole('admin'));
  const estado = presentacionDeEstado(c);

  async function ejecutar(accion: () => Promise<CotizacionPanel>, contexto: string, mensaje: string) {
    setOcupado(true);
    setFallo(null);
    try {
      await accion();
      show(mensaje);
      await reload();
      return true;
    } catch (error) {
      setFallo(describe(error, contexto));
      return false;
    } finally {
      setOcupado(false);
    }
  }

  async function crearNueva() {
    const origen = c.specialOrderLeadId !== null ? 'personalizacion' : 'volumen';
    const solicitud = c.specialOrderLeadId ?? c.institutionRequestId;
    if (solicitud === null) return;
    setOcupado(true);
    setFallo(null);
    try {
      const nueva = await cotizaciones.crear(origen, solicitud);
      navegar(`/admin/solicitudes/cotizaciones/${nueva.detalle.cotizacion.id}`);
    } catch (error) {
      setFallo(describe(error, 'crear la cotización nueva'));
    } finally {
      setOcupado(false);
    }
  }

  return (
    <PageContainer
      title={`Cotización ${c.quoteNumber}`}
      actions={<Link to="/admin/solicitudes/cotizaciones">Volver a cotizaciones</Link>}
    >
      <div className="b2b-pila">
        <div className="b2b-fila">
          <Badge>{estado.etiqueta}</Badge>
          {estado.condicion && <Badge tone="warning">{estado.condicion}</Badge>}
          {!c.isActive && <Badge tone="danger">Dada de baja</Badge>}
          <span className="b2b-dato">Creada {formatearFecha(c.createdAt)}</span>
        </div>
        {estado.condicion && c.invalidatedReason && (
          <Alert tone="warning" title="Ya no es válida">{c.invalidatedReason}</Alert>
        )}
        <FailureAlert failure={fallo} />

        {acciones.editarLineas ? (
          <EditorDeLineas
            panel={panel}
            moneda={moneda}
            ocupado={ocupado}
            onGuardar={(lineas) => ejecutar(() => cotizaciones.editarLineas(id, lineas), 'guardar las líneas', 'Líneas guardadas.')}
          />
        ) : (
          <LineasEnLectura panel={panel} moneda={moneda} />
        )}

        <Mayorista panel={panel} moneda={moneda} />

        {(c.status === 'pagada' || panel.detalle.paidAt) && (
          <section className="b2b-seccion" aria-labelledby="b2b-pago">
            <h2 id="b2b-pago">Pago</h2>
            <p>
              {panel.detalle.paymentMethod === 'yape' ? 'Yape' : 'Efectivo'}
              {panel.detalle.paymentReference && ` · código de operación ${panel.detalle.paymentReference}`}
              {` · ${formatearFecha(panel.detalle.paidAt)}`}
            </p>
            {/* C13: hoy es el correo de quien registró; no se presenta como nombre. */}
            {panel.detalle.paidRegisteredBy && <p className="b2b-dato">Registrado por: {panel.detalle.paidRegisteredBy}</p>}
          </section>
        )}

        <section className="b2b-seccion b2b-fila" aria-label="Acciones">
          {acciones.enviar && (
            <Button disabled={ocupado} onClick={() => void ejecutar(() => cotizaciones.enviar(id), 'enviar la cotización', `Cotización ${c.quoteNumber} marcada como enviada.`)}>
              Marcar como enviada
            </Button>
          )}
          {acciones.prepararParaEnviar && (
            <Button variant="secondary" onClick={() => setPreparando(true)}>Preparar para enviar</Button>
          )}
          {acciones.aprobar && (
            <Button disabled={ocupado} onClick={() => void ejecutar(() => cotizaciones.aprobar(id), 'registrar la aprobación', `Aprobación de ${c.quoteNumber} registrada.`)}>
              Registrar aprobación
            </Button>
          )}
          {acciones.crearNuevaDesdeSolicitud && (
            <Button disabled={ocupado} onClick={() => void crearNueva()}>Preparar una nueva desde la solicitud</Button>
          )}
          {acciones.registrarPago && <Button onClick={() => setPagando(true)}>Registrar pago</Button>}
          {acciones.darDeBaja && <Button variant="danger" onClick={() => setDandoDeBaja(true)}>Dar de baja</Button>}
        </section>
      </div>

      {pagando && (
        <RegistrarPago
          numero={c.quoteNumber}
          ocupado={ocupado}
          onCancelar={() => setPagando(false)}
          onRegistrar={async (metodo, referencia) => {
            if (await ejecutar(() => cotizaciones.registrarPago(id, metodo, referencia), 'registrar el pago', `Pago de ${c.quoteNumber} registrado.`)) setPagando(false);
          }}
        />
      )}

      <ConfirmDialog
        open={dandoDeBaja}
        title={`¿Dar de baja la cotización ${c.quoteNumber}?`}
        confirmLabel="Dar de baja"
        danger
        busy={ocupado}
        onCancel={() => setDandoDeBaja(false)}
        onConfirm={async () => {
          await ejecutar(() => cotizaciones.darDeBaja(id), 'dar de baja la cotización', `Cotización ${c.quoteNumber} dada de baja.`);
          setDandoDeBaja(false);
        }}
      >
        Deja de estar activa y sale de la bandeja. No se borra: queda en el registro.
      </ConfirmDialog>

      {preparando && <PrepararParaEnviar panel={panel} moneda={moneda} onClose={() => setPreparando(false)} />}
      <Toasts toasts={toasts} />
    </PageContainer>
  );
}

function LineasEnLectura({ panel, moneda }: { panel: CotizacionPanel; moneda: string | null }) {
  const lineas = [...panel.detalle.lines].sort((a, b) => a.sortOrder - b.sortOrder);
  return (
    <section className="b2b-seccion" aria-labelledby="b2b-lineas">
      <h2 id="b2b-lineas">Líneas</h2>
      {lineas.length === 0 ? <p className="b2b-dato">Sin líneas.</p> : (
        <ul className="b2b-lista">
          {lineas.map((l, i) => (
            <li key={i} className="b2b-linea">
              <strong>{l.description}</strong>
              <span>{l.quantity} × {formatearImporte(l.unitPrice, moneda)} = {formatearImporte(l.quantity * l.unitPrice, moneda)}</span>
              {l.itemId && <span className="b2b-linea__referencia">Precio de catálogo al cotizar: {precioDeCatalogo(l.catalogPriceAtQuote, moneda)}</span>}
            </li>
          ))}
        </ul>
      )}
      <p><strong>Total: {formatearImporte(panel.detalle.cotizacion.totalAmount, moneda)}</strong></p>
    </section>
  );
}

/** A8. Cero líneas es válido en borrador; el orden de la lista es el del documento. */
function EditorDeLineas({ panel, moneda, ocupado, onGuardar }: {
  panel: CotizacionPanel;
  moneda: string | null;
  ocupado: boolean;
  onGuardar: (lineas: ReturnType<typeof aPeticion>) => Promise<boolean>;
}) {
  const [lineas, setLineas] = useState<LineaEditable[]>(() => desdeContrato(panel.detalle.lines));
  const [busqueda, setBusqueda] = useState('');
  const [encontradas, setEncontradas] = useState<PresentacionParaElegir[] | null>(null);
  const [falloBusqueda, setFalloBusqueda] = useState<Failure | null>(null);
  const [intentado, setIntentado] = useState(false);
  useEffect(() => { setLineas(desdeContrato(panel.detalle.lines)); }, [panel]);

  const errores = erroresDeLineas(lineas);
  const cambiar = (clave: string, cambio: Partial<LineaEditable>) => setLineas((ls) => ls.map((l) => (l.clave === clave ? { ...l, ...cambio } : l)));

  async function buscar() {
    setFalloBusqueda(null);
    try {
      setEncontradas(await catalogo.presentaciones(busqueda));
    } catch (error) {
      setFalloBusqueda(describe(error, 'buscar presentaciones'));
    }
  }

  return (
    <section className="b2b-seccion" aria-labelledby="b2b-editor">
      <h2 id="b2b-editor">Líneas del borrador</h2>
      {lineas.length === 0 && <p className="b2b-dato">Sin líneas todavía. Puedes guardar así y seguir después.</p>}
      <ol className="b2b-lista">
        {lineas.map((l, i) => (
          <li key={l.clave} className="b2b-linea">
            <div className="b2b-fila">
              <strong>{l.itemId ? l.presentacion : 'Línea libre'}</strong>
              {l.itemId && <span className="b2b-linea__referencia">Catálogo: {precioDeCatalogo(l.precioCatalogo, moneda)}</span>}
            </div>
            <div className="b2b-linea__campos">
              <Field label="Descripción" required={l.itemId === null}>
                {(props) => <Input {...props} value={l.descripcion} onChange={(e) => cambiar(l.clave, { descripcion: e.target.value })} />}
              </Field>
              <Field label="Cantidad" required>
                {(props) => <Input {...props} inputMode="numeric" value={l.cantidad} onChange={(e) => cambiar(l.clave, { cantidad: e.target.value })} />}
              </Field>
              <Field label="Precio unitario cobrado" required hint={l.itemId && l.precioCatalogo === null ? 'La presentación es «a consultar»: pon el precio que acordaste.' : undefined}>
                {(props) => <Input {...props} inputMode="decimal" value={l.precioUnitario} onChange={(e) => cambiar(l.clave, { precioUnitario: e.target.value })} />}
              </Field>
            </div>
            {intentado && errores.get(l.clave) && <p className="ui-field__error" role="alert">{errores.get(l.clave)}</p>}
            <div className="b2b-fila">
              <Button size="sm" variant="ghost" disabled={i === 0} onClick={() => setLineas((ls) => moverLinea(ls, i, -1))}>Subir</Button>
              <Button size="sm" variant="ghost" disabled={i === lineas.length - 1} onClick={() => setLineas((ls) => moverLinea(ls, i, 1))}>Bajar</Button>
              <Button size="sm" variant="ghost" onClick={() => setLineas((ls) => ls.filter((x) => x.clave !== l.clave))}>Quitar</Button>
            </div>
          </li>
        ))}
      </ol>

      <div className="b2b-fila">
        <Button variant="secondary" onClick={() => setLineas((ls) => [...ls, lineaLibre()])}>Añadir línea libre</Button>
      </div>
      <div className="b2b-fila">
        <Field label="Añadir presentación del catálogo">
          {(props) => <Input {...props} value={busqueda} onChange={(e) => setBusqueda(e.target.value)} />}
        </Field>
        <Button variant="secondary" disabled={busqueda.trim() === ''} onClick={() => void buscar()}>Buscar</Button>
      </div>
      <FailureAlert failure={falloBusqueda} />
      {encontradas !== null && (encontradas.length === 0 ? <p className="b2b-dato">Ninguna presentación coincide con «{busqueda}».</p> : (
        <ul className="b2b-lista">
          {encontradas.map((p) => (
            <li key={p.itemId} className="b2b-fila">
              <span>{[p.productName, p.variantValue].filter(Boolean).join(' — ')} · {precioDeCatalogo(p.price, moneda)}</span>
              <Button size="sm" onClick={() => { setLineas((ls) => [...ls, lineaDeCatalogo(p)]); setEncontradas(null); setBusqueda(''); }}>Añadir</Button>
            </li>
          ))}
        </ul>
      ))}

      <p><strong>Total a cobrar: {formatearImporte(totalCobrado(lineas), moneda)}</strong></p>
      <div>
        <Button disabled={ocupado} onClick={() => { setIntentado(true); if (erroresDeLineas(lineas).size === 0) void onGuardar(aPeticion(lineas)); }}>
          Guardar líneas
        </Button>
      </div>
    </section>
  );
}

/** El umbral informa; nunca cambia un precio. */
function Mayorista({ panel, moneda }: { panel: CotizacionPanel; moneda: string | null }) {
  const m = panel.mayorista;
  const t = textoMayorista(m);
  return (
    <Alert tone="info" title={t.titulo}>
      {t.texto}
      {m.umbral !== null && m.importeDeLista !== null && ` Importe a precio de lista: ${formatearImporte(m.importeDeLista, moneda)}; umbral: ${formatearImporte(m.umbral, moneda)}.`}
    </Alert>
  );
}

/** Solo `admin`. Yape exige código; efectivo no; tarjeta no se ofrece (llega con M11). */
function RegistrarPago({ numero, ocupado, onCancelar, onRegistrar }: {
  numero: string;
  ocupado: boolean;
  onCancelar: () => void;
  onRegistrar: (metodo: MetodoDePago, referencia: string) => Promise<void>;
}) {
  const [metodo, setMetodo] = useState<MetodoDePago | null>(null);
  const [referencia, setReferencia] = useState('');
  const [intentado, setIntentado] = useState(false);
  const error = errorDePago(metodo, referencia);
  const exige = METODOS_DE_PAGO.find((m) => m.valor === metodo)?.exigeReferencia ?? false;

  return (
    <Drawer open title={`Registrar pago de ${numero}`} onClose={onCancelar}
      footer={
        <div className="b2b-fila">
          <Button variant="ghost" onClick={onCancelar}>Volver</Button>
          <Button disabled={ocupado} onClick={() => { setIntentado(true); if (!error && metodo) void onRegistrar(metodo, referencia); }}>Registrar pago</Button>
        </div>
      }
    >
      <div className="b2b-pila">
        <fieldset className="b2b-pila">
          <legend>Cómo se pagó</legend>
          {METODOS_DE_PAGO.map((m) => (
            <label key={m.valor} className="b2b-fila">
              <input type="radio" name="metodo" value={m.valor} checked={metodo === m.valor} onChange={() => setMetodo(m.valor)} />
              {m.etiqueta}
            </label>
          ))}
        </fieldset>
        <Field label="Código de operación" required={exige} error={intentado && error && metodo ? error : null}
          hint={exige ? 'Obligatorio con Yape.' : 'Opcional con efectivo.'}>
          {(props) => <Input {...props} value={referencia} onChange={(e) => setReferencia(e.target.value)} />}
        </Field>
        {intentado && !metodo && <p className="ui-field__error" role="alert">{error}</p>}
      </div>
    </Drawer>
  );
}

/**
 * «Preparar para enviar»: un texto para copiar y mandar desde el WhatsApp propio.
 * Sin integración, sin estado «enviado por WhatsApp», sin confirmación remota.
 */
function PrepararParaEnviar({ panel, moneda, onClose }: { panel: CotizacionPanel; moneda: string | null; onClose: () => void }) {
  const c = panel.detalle.cotizacion;
  const texto = [
    `Cotización ${c.quoteNumber}`,
    ...[...panel.detalle.lines].sort((a, b) => a.sortOrder - b.sortOrder)
      .map((l) => `• ${l.description}: ${l.quantity} × ${formatearImporte(l.unitPrice, moneda)} = ${formatearImporte(l.quantity * l.unitPrice, moneda)}`),
    `Total: ${formatearImporte(c.totalAmount, moneda)}`,
  ].join('\n');
  const [copiado, setCopiado] = useState(false);

  return (
    <Drawer open title="Preparar para enviar" description="Prepara la cotización y envíala desde tu propio WhatsApp." onClose={onClose}
      footer={<Button onClick={() => void navigator.clipboard?.writeText(texto).then(() => setCopiado(true))}>{copiado ? 'Texto copiado' : 'Copiar texto'}</Button>}
    >
      <textarea className="ui-input b2b-texto-copiable" readOnly value={texto} aria-label="Texto de la cotización" />
    </Drawer>
  );
}
