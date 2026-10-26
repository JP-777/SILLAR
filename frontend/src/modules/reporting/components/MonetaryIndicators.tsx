import { useEffect, useState, type ReactNode } from 'react';
import { type Failure } from '../../../shared/errors/messages';
import { FailureAlert } from '../../../shared/ui/patterns';
import {
  methodLabel,
  showAmount,
  SOURCE_LABELS,
  type CurrencyAmount,
  type IndicatorStatus,
  type OrderValueView,
  type PaymentIndicatorsView,
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
 * sigue viendo. Se monta con `key` por cohorte y zona, así que una respuesta
 * que llega tras cambiar la consulta se descarta (D4).
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
        <Shown read={payments} render={(data) => <PaymentCount data={data} />} />
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
function Unmeasured({ status, what }: { status: IndicatorStatus; what: 'value' | 'payments' }) {
  if (status === 'source_unavailable') {
    return <p>No disponible: el módulo de ventas no está activo en esta instalación.</p>;
  }
  return what === 'value'
    ? <p><strong>No medible.</strong> Ventas todavía no informa el valor de los pedidos con su moneda. No significa cero.</p>
    : <p><strong>No medible.</strong> Ventas todavía no permite registrar y consultar cobros. No significa cero cobros.</p>;
}

function Amounts({ list, unit }: { list: CurrencyAmount[]; unit: string }) {
  return (
    <ul>
      {list.map((item) => (
        <li key={item.currencyCode}><strong>{showAmount(item.currencyCode, item.amount)}</strong> · {showCount(item.count)} {unit}</li>
      ))}
    </ul>
  );
}

function OrderValue({ data }: { data: OrderValueView }) {
  if (data.status === 'source_unavailable' || data.status === 'not_measurable') return <Unmeasured status={data.status} what="value" />;
  if (data.status === 'empty') return <p>No se crearon pedidos en este período, así que no hay valor que sumar.</p>;
  return (
    <>
      {data.byCurrency && <Amounts list={data.byCurrency} unit="pedidos" />}
      {data.withoutCurrencyCount !== null && data.withoutCurrencyCount > 0 && (
        <p className="rp-note">
          {showCount(data.withoutCurrencyCount)} pedidos no tienen moneda registrada: se cuentan, pero su valor no se suma.
          {data.byCurrency && data.byCurrency.length > 1 && ' Cada moneda se suma por separado.'}
        </p>
      )}
    </>
  );
}

function PaymentCount({ data }: { data: PaymentIndicatorsView }) {
  const { i05 } = data;
  if (i05.status === 'source_unavailable' || i05.status === 'not_measurable') return <Unmeasured status={i05.status} what="payments" />;
  return (
    <>
      <strong>{showCount(i05.count)}</strong>
      {i05.status === 'empty' && <p>No se registró ningún cobro en este período.</p>}
      {i05.bySource && i05.bySource.length > 0 && (
        <ul>{i05.bySource.map((s) => <li key={s.source}>{SOURCE_LABELS[s.source]}: {showCount(s.count)}</li>)}</ul>
      )}
      <p className="rp-note">Cuenta cobros, no pedidos: un pedido puede tener varios.</p>
    </>
  );
}

function PaymentAmounts({ data }: { data: PaymentIndicatorsView }) {
  const { i06 } = data;
  if (i06.status === 'source_unavailable' || i06.status === 'not_measurable') return <Unmeasured status={i06.status} what="payments" />;
  if (i06.status === 'empty') return <p>No se registró ningún cobro en este período, así que no hay importe que sumar.</p>;
  return (
    <>
      {i06.byMethod && (
        <ul>
          {i06.byMethod.map((m) => (
            <li key={`${m.method}|${m.source}|${m.currencyCode}`}>
              {methodLabel(m.method)} · {SOURCE_LABELS[m.source]}: <strong>{showAmount(m.currencyCode, m.amount)}</strong> ({showCount(m.count)} cobros)
            </li>
          ))}
        </ul>
      )}
      {i06.withoutCurrency && i06.withoutCurrency.length > 0 && (
        <p className="rp-note">
          Sin moneda registrada, se cuentan pero no se suman: {i06.withoutCurrency
            .map((m) => `${methodLabel(m.method)} · ${SOURCE_LABELS[m.source]}: ${showCount(m.count)}`).join('; ')}.
        </p>
      )}
    </>
  );
}

function LatePayments({ data }: { data: PaymentIndicatorsView }) {
  const { i07 } = data;
  if (i07.status === 'source_unavailable' || i07.status === 'not_measurable') return <Unmeasured status={i07.status} what="payments" />;
  return (
    <>
      <strong>{showCount(i07.count)}</strong>
      {i07.status === 'empty' && <p>Ningún cobro de este período se registró con retraso.</p>}
      {i07.amountByCurrency && <Amounts list={i07.amountByCurrency} unit="cobros" />}
      {i07.withoutCurrencyCount !== null && i07.withoutCurrencyCount > 0 && (
        <p className="rp-note">{showCount(i07.withoutCurrencyCount)} de ellos no tienen moneda registrada: se cuentan, pero su importe no se suma.</p>
      )}
      <p className="rp-note">Retraso marcado al registrar el cobro; no se recalcula con el plazo actual.</p>
    </>
  );
}
