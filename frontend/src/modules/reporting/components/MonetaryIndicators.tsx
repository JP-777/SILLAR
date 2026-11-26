import { useEffect, useState, type ReactNode } from 'react';
import { type Failure } from '../../../shared/errors/messages';
import { FailureAlert } from '../../../shared/ui/patterns';
import {
  methodLabel,
  REASON_LABELS,
  showAmount,
  SOURCE_LABELS,
  type IndicatorStatus,
  type NotMeasurableReason,
  type OrderValueView,
  type PaymentIndicatorsView,
  type SourceMeasure,
} from '../services/monetaryIndicators';
import { showCount } from '../services/period';
import { describeReportFailure, loadOrderValue, loadPaymentIndicators } from '../services/reporting';

type Read<T> = { kind: 'loading' } | { kind: 'ready'; data: T } | { kind: 'error'; failure: Failure };

interface Props {
  /** Instantes de la cohorte ya consultada: la misma [inicio, fin) que los recuentos. */
  readonly start: string;
  readonly end: string;
}

/**
 * I02 e I05–I07. Cada lector se pide por separado: si uno falla, el otro se
 * sigue viendo. Se monta con `key` por cohorte, así que una respuesta que llega
 * tras cambiar la consulta se descarta (D4).
 */
export function MonetaryIndicators({ start, end }: Props) {
  const [value, setValue] = useState<Read<OrderValueView>>({ kind: 'loading' });
  const [payments, setPayments] = useState<Read<PaymentIndicatorsView>>({ kind: 'loading' });

  useEffect(() => {
    let current = true;
    loadOrderValue(start, end).then(
      (data) => { if (current) setValue({ kind: 'ready', data }); },
      (error) => { if (current) setValue({ kind: 'error', failure: describeReportFailure(error, 'consultar el valor de pedidos') }); },
    );
    loadPaymentIndicators(start, end).then(
      (data) => { if (current) setPayments({ kind: 'ready', data }); },
      (error) => { if (current) setPayments({ kind: 'error', failure: describeReportFailure(error, 'consultar los cobros registrados') }); },
    );
    return () => { current = false; };
  }, [start, end]);

  return (
    <div className="rp-kpis">
      <section className="rp-panel">
        <h2>Valor de pedidos creados</h2>
        <Shown read={value} render={(data) => <OrderValue data={data} />} />
        <p className="rp-note">Total congelado de cada pedido al crearse. No es un cobro ni un ingreso.</p>
      </section>
      <section className="rp-panel">
        <h2>Cobros registrados</h2>
        <Shown read={payments} render={(data) => <><PaymentCount data={data} /><HistoricalWithoutSource data={data} /></>} />
      </section>
      <section className="rp-panel">
        <h2>Importe de cobros por medio</h2>
        <Shown read={payments} render={(data) => <PaymentAmounts data={data} />} />
        <p className="rp-note">Suma de cobros registrados, no neta: no descuenta reembolsos ni reversos.</p>
      </section>
      <section className="rp-panel">
        <h2>Cobros registrados con retraso</h2>
        <Shown read={payments} render={(data) => <LatePayments data={data} />} />
      </section>
    </div>
  );
}

function Shown<T>({ read, render }: { read: Read<T>; render: (data: T) => ReactNode }) {
  if (read.kind === 'loading') return <p className="rp-note">Consultando…</p>;
  if (read.kind === 'error') return read.failure.kind === 'silent' ? null : <FailureAlert failure={read.failure} />;
  return <>{render(read.data)}</>;
}

/** Lo que se dice cuando no hay cifra. Nunca un cero. */
function Unmeasured({ status, reason, zero }: { status: IndicatorStatus; reason: NotMeasurableReason | null; zero: string }) {
  if (status === 'source_unavailable') {
    return <p>No disponible: el módulo de ventas no está activo en esta instalación.</p>;
  }
  return <p><strong>No medible.</strong> {reason ? REASON_LABELS[reason] : ''} No significa {zero}.</p>;
}

/** Con un origen sin medir, la cifra es de los que sí se miden, y se dice. */
function PartialNote({ coverage }: { coverage: string | null }) {
  return coverage === 'partial'
    ? <p className="rp-note">Solo de los orígenes que se pueden medir: no es el total.</p>
    : null;
}

function OrderValue({ data }: { data: OrderValueView }) {
  if (data.status === 'source_unavailable' || data.status === 'not_measurable') {
    return data.status === 'source_unavailable'
      ? <Unmeasured status={data.status} reason={null} zero="cero" />
      : <p><strong>No medible.</strong> Ventas todavía no informa el valor de los pedidos con su moneda. No significa cero.</p>;
  }
  if (data.status === 'empty') return <p>No se crearon pedidos en este período, así que no hay valor que sumar.</p>;
  const without = data.withoutCurrency ? data.withoutCurrency.active + data.withoutCurrency.inactive : 0;
  return (
    <>
      {data.byCurrency && (
        <ul>
          {data.byCurrency.map((row) => (
            <li key={row.currencyCode}>
              <strong>{showAmount(row.currencyCode, row.activeAmount)}</strong> · {showCount(row.activeCount)} pedidos activos
              {row.inactiveCount > 0 && <> · aparte, {showAmount(row.currencyCode, row.inactiveAmount)} de {showCount(row.inactiveCount)} dados de baja</>}
            </li>
          ))}
        </ul>
      )}
      {without > 0 && data.withoutCurrency && (
        <p className="rp-note">
          {showCount(without)} pedidos no tienen moneda registrada ({showCount(data.withoutCurrency.active)} activos,
          {' '}{showCount(data.withoutCurrency.inactive)} dados de baja): se cuentan, pero su valor no se suma.
        </p>
      )}
      {data.byCurrency && data.byCurrency.length > 1 && <p className="rp-note">Cada moneda se suma por separado.</p>}
    </>
  );
}

/** Cada origen con su cifra, o con el motivo por el que no la tiene. */
function Sources({ list, unit }: { list: SourceMeasure[] | null; unit: string }) {
  if (!list) return null;
  return (
    <ul>
      {list.map((s) => (
        <li key={s.source}>
          {SOURCE_LABELS[s.source]}: {s.status === 'measured'
            ? `${showCount(s.count)} ${unit}`
            : <>no medible. {s.reason ? REASON_LABELS[s.reason] : ''}</>}
        </li>
      ))}
    </ul>
  );
}

function Amounts({ list, unit }: { list: { currencyCode: string; count: number; amount: string }[]; unit: string }) {
  return (
    <ul>
      {list.map((row) => (
        <li key={row.currencyCode}><strong>{showAmount(row.currencyCode, row.amount)}</strong> · {showCount(row.count)} {unit}</li>
      ))}
    </ul>
  );
}

/** Los históricos sin origen: origen histórico no acreditado, nunca del personal ni del proveedor. */
function HistoricalWithoutSource({ data }: { data: PaymentIndicatorsView }) {
  if (!data.historicalWithoutSource) return null;
  return (
    <p className="rp-note">
      Origen histórico no acreditado: {showCount(data.historicalWithoutSource.count)} cobros antiguos sin origen registrado.
      No se atribuyen al personal ni al proveedor de pagos, ni se suman a ninguna cifra.
    </p>
  );
}

function PaymentCount({ data }: { data: PaymentIndicatorsView }) {
  const { i05 } = data;
  if (i05.status === 'source_unavailable' || i05.status === 'not_measurable') {
    return <Unmeasured status={i05.status} reason={data.notMeasurableReason} zero="cero cobros" />;
  }
  return (
    <>
      <strong>{showCount(i05.count)}</strong>
      {i05.status === 'empty' && <p>No se registró ningún cobro en este período.</p>}
      <PartialNote coverage={i05.coverage} />
      <Sources list={i05.bySource} unit="cobros" />
      <p className="rp-note">Cuenta cobros, no pedidos: un pedido puede tener varios.</p>
    </>
  );
}

function PaymentAmounts({ data }: { data: PaymentIndicatorsView }) {
  const { i06 } = data;
  if (i06.status === 'source_unavailable' || i06.status === 'not_measurable') {
    return <><Unmeasured status={i06.status} reason={data.notMeasurableReason} zero="cero cobros" /><Sources list={i06.bySource} unit="cobros con importe" /></>;
  }
  if (i06.status === 'empty') return <p>No se registró ningún cobro en este período, así que no hay importe que sumar.</p>;
  return (
    <>
      {i06.byCurrency && i06.byCurrency.length > 0 && <Amounts list={i06.byCurrency} unit="cobros" />}
      {i06.byMethod && i06.byMethod.length > 0 && (
        <ul>
          {i06.byMethod.map((m) => (
            <li key={`${m.method}|${m.source}|${m.currencyCode}`}>
              {methodLabel(m.method)} · {SOURCE_LABELS[m.source]}: {showAmount(m.currencyCode, m.amount)} ({showCount(m.count)} cobros)
            </li>
          ))}
        </ul>
      )}
      {i06.withoutCurrencyCount !== null && i06.withoutCurrencyCount > 0 && (
        <p className="rp-note">{showCount(i06.withoutCurrencyCount)} cobros no tienen moneda registrada: se cuentan, pero su importe no se suma.</p>
      )}
      <PartialNote coverage={i06.coverage} />
      <Sources list={i06.bySource} unit="cobros con importe" />
    </>
  );
}

function LatePayments({ data }: { data: PaymentIndicatorsView }) {
  const { i07 } = data;
  if (i07.status === 'source_unavailable' || i07.status === 'not_measurable') {
    return <Unmeasured status={i07.status} reason={data.notMeasurableReason} zero="cero cobros con retraso" />;
  }
  return (
    <>
      <strong>{showCount(i07.count)}</strong>
      {i07.status === 'empty' && <p>Ningún cobro de este período se registró con retraso.</p>}
      <PartialNote coverage={i07.coverage} />
      <Sources list={i07.bySource} unit="con retraso" />
      {i07.amountByCurrency && i07.amountByCurrency.length > 0 && <Amounts list={i07.amountByCurrency} unit="cobros con retraso" />}
      {i07.amountStatus === 'currency_not_accredited' && <p className="rp-note">Su importe no se suma: hay cobros sin moneda registrada.</p>}
      {i07.amountStatus === 'not_measurable' && <p className="rp-note">Su importe no se puede medir en este período.</p>}
      <PartialNote coverage={i07.amountCoverage} />
      <p className="rp-note">Retraso marcado al registrar el cobro; no se recalcula con el plazo actual.</p>
    </>
  );
}
