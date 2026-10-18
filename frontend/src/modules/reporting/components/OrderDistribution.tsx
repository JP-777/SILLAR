import { orderStates } from '../services/orderIndicators';
import './order-distribution.css';

/**
 * M10 D9: solo representación visual de una cohorte ya validada por D8.
 *
 * La pantalla lo monta únicamente con estado `with_data`: ni fuente ausente,
 * ni cohorte vacía, ni error dibujan distribuciones. Los estados salen del
 * catálogo central de D8; aquí no se mantiene otra lista.
 */
interface StatusCount {
  readonly status: string;
  readonly count: number;
}

export interface OrderDistributionProps {
  readonly createdOrders: number;
  readonly currentStatusCounts: ReadonlyArray<StatusCount>;
  readonly activeOrders: number;
  readonly inactiveOrders: number;
}

function DistributionRow({ label, count, total }: {
  readonly label: string;
  readonly count: number;
  readonly total: number;
}) {
  const percent = total > 0 ? (count / total) * 100 : 0;
  return (
    <li className="rp-dist__item">
      <div className="rp-dist__line">
        <span>{label}</span>
        <strong>{count.toLocaleString('es-PE')} · {percent.toLocaleString('es-PE', { maximumFractionDigits: 1 })} %</strong>
      </div>
      {/* La línea de texto ya dice cuánto y qué parte: la barra es solo apoyo visual. */}
      <div className="rp-dist__track" aria-hidden="true">
        <div className="rp-dist__fill" style={{ width: `${percent}%` }} />
      </div>
    </li>
  );
}

/** Estado y vigencia se presentan por separado: no son la misma dimensión. */
export function OrderDistribution({
  createdOrders,
  currentStatusCounts,
  activeOrders,
  inactiveOrders,
}: OrderDistributionProps) {
  // Sin cohorte no hay porcentajes: el estado vacío de la pantalla manda.
  if (createdOrders === 0) return null;

  const counts = new Map(currentStatusCounts.map((entry) => [entry.status, entry.count]));
  return (
    <div className="rp-dist">
      <section aria-labelledby="rp-dist-status-heading">
        <h2 id="rp-dist-status-heading">Distribución por estado actual</h2>
        <ul className="rp-dist__list">
          {orderStates.map(({ code, label }) => (
            <DistributionRow key={code} label={label} count={counts.get(code) ?? 0} total={createdOrders} />
          ))}
        </ul>
      </section>
      <section aria-labelledby="rp-dist-activity-heading">
        <h2 id="rp-dist-activity-heading">Vigencia del registro</h2>
        <p className="rp-dist__hint">Que un pedido esté dado de baja no depende de su estado actual.</p>
        <ul className="rp-dist__list">
          <DistributionRow label="Activos" count={activeOrders} total={createdOrders} />
          <DistributionRow label="Dados de baja" count={inactiveOrders} total={createdOrders} />
        </ul>
      </section>
    </div>
  );
}
