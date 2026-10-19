/**
 * D16 — solo los pedidos creados se normalizan por duración; jamás estados ni vencidos.
 *
 * D21: este panel ya no monta los paneles de composición de D17/D18. Repetían los
 * recuentos, porcentajes y puntos porcentuales por estado y vigencia que la tabla
 * principal de OrderPeriodComparison ya muestra, con la misma compuerta.
 */
import { orderStates, type OrderIndicatorsView } from '../services/reporting';
import { assessReportComparison, type ReportComparisonSnapshot } from '../services/reportComparisonGate';
import { compareCreationWindowRates } from '../services/creationWindowRate';

interface Props {
  readonly baseline: OrderIndicatorsView;
  readonly compared: OrderIndicatorsView;
}
const FORMAT = new Intl.NumberFormat('es-PE', { maximumFractionDigits: 2 });
function snapshot(data: OrderIndicatorsView): ReportComparisonSnapshot {
  const statuses = Object.fromEntries(orderStates.map(({ code }) => [
    `status:${code}`,
    data.i03CurrentStatusCounts?.find((item) => item.status === code)?.count ?? null,
  ])) as Record<string, number | null>;
  return {
    validated: true, // solo después de verifyOrderIndicators en el llamador
    state: data.status,
    window: {
      startInclusive: data.startInclusive,
      endExclusive: data.endExclusive,
      observedAtUtc: data.observedAtUtc,
    },
    metrics: {
      i01: data.i01CreatedOrders,
      i04: data.i04CurrentlyExpiredOrders,
      active: data.activeOrders,
      inactive: data.inactiveOrders,
      ...statuses,
    },
  };
}

export function OrderCreationWindowPanel({ baseline, compared }: Props) {
  // A y B ya pasaron D8 en loadOrderIndicators; no duplicar validaciones.
  let result;
  try {
    const a = snapshot(baseline);
    const b = snapshot(compared);
    result = compareCreationWindowRates(a, b, assessReportComparison(a, b));
  } catch {
    return null;
  }
  if (result.kind !== 'available') return null;
  return (
    <section className="rp-compare__rates" aria-labelledby="rp-creation-rate-heading">
      <h3 id="rp-creation-rate-heading">Pedidos creados por duración de ventana</h3>
      <p className="rp-compare__note">
        Promedio de pedidos creados por cada 24 horas de cada período. Sirve para comparar períodos
        de distinta duración; no es una tendencia, una velocidad instantánea ni una observación sincronizada.
      </p>
      <div className="rp-compare__scroll">
        <table className="rp-compare__table">
          <caption>Normalización aritmética de pedidos creados; no compara transiciones históricas.</caption>
          <thead><tr><th scope="col">Medida</th><th scope="col">A</th><th scope="col">B</th><th scope="col">B − A</th></tr></thead>
          <tbody>
            <tr><th scope="row">Duración del período (días de 24 h)</th>
              <td>{FORMAT.format(result.windowADays)}</td>
              <td>{FORMAT.format(result.windowBDays)}</td><td>—</td></tr>
            <tr><th scope="row">Pedidos creados</th>
              <td>{FORMAT.format(result.ordersA)}</td>
              <td>{FORMAT.format(result.ordersB)}</td><td>—</td></tr>
            <tr><th scope="row">Promedio de pedidos por cada 24 h</th>
              <td>{FORMAT.format(result.averagePer24hA)}</td>
              <td>{FORMAT.format(result.averagePer24hB)}</td>
              <td>{result.averagePer24hDifference > 0 ? '+' : ''}{FORMAT.format(result.averagePer24hDifference)}</td>
            </tr>
          </tbody>
        </table>
      </div>
      <p className="rp-compare__note">
        No se aplica a los estados actuales ni a los vencidos: describen la situación observada de cada
        cohorte, no un ritmo. Cada informe conserva su propio instante de observación.
      </p>
    </section>
  );
}
