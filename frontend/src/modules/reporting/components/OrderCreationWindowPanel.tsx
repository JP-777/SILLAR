/** D16 — solo I01 se normaliza por duración; jamás I03/I04. */
import { compareCohortStatusMix, type StatusMixResult } from '../services/cohortStatusMix';
import { CohortStatusMixPanel } from './CohortStatusMixPanel';
import { compareCohortActivityMix, type ActivityMixResult } from '../services/cohortActivityMix';
import { CohortActivityMixPanel } from './CohortActivityMixPanel';
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
  let mix: StatusMixResult;
  let activity: ActivityMixResult;
  try {
    const a = snapshot(baseline);
    const b = snapshot(compared);
    const gate = assessReportComparison(a, b);
    result = compareCreationWindowRates(a, b, gate);
    mix = compareCohortStatusMix(a, b, gate, orderStates);
    activity = compareCohortActivityMix(a, b, gate);
  } catch {
    return null;
  }
  if (result.kind !== 'available') return null;
  return (
    <section className="rp-compare__rates" aria-labelledby="rp-creation-rate-heading">
      <h3 id="rp-creation-rate-heading">Pedidos creados por duración de ventana</h3>
      <p className="rp-compare__note">{result.coverageNotice}</p>
      <div className="rp-compare__scroll">
        <table className="rp-compare__table">
          <caption>Normalización aritmética de pedidos creados; no compara transiciones históricas.</caption>
          <thead><tr><th scope="col">Medida</th><th scope="col">A</th><th scope="col">B</th><th scope="col">B−A</th></tr></thead>
          <tbody>
            <tr><th scope="row">Ventana (días de 24 h)</th>
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
        Esta normalización no se aplica a los indicadores de estado actual ni vencimiento: describen la situación observada de cada cohorte.
        Ambos informes conservan instantes de observación independientes.
      </p>
      <CohortStatusMixPanel result={mix} />
      <CohortActivityMixPanel result={activity} />
    </section>
  );
}
