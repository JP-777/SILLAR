/** M10 D18 — Dimensión activo/inactivo, separada de los siete estados de D17. */
import type { ActivityMixResult } from '../services/cohortActivityMix';

const pct = new Intl.NumberFormat('es-PE', { maximumFractionDigits: 1 });
const integer = new Intl.NumberFormat('es-PE', { maximumFractionDigits: 0 });
const percentage = (value: number | null): string => value === null
  ? 'No calculable (I01 = 0)' : `${pct.format(value)} %`;
const delta = (value: number | null): string => value === null
  ? '—' : `${value > 0 ? '+' : ''}${pct.format(value)} pp`;

export function CohortActivityMixPanel({ result }: { readonly result: ActivityMixResult }) {
  if (result.kind !== 'available') return null;
  return (
    <section className="rp-compare__activity" aria-labelledby="rp-activity-mix-heading">
      <h3 id="rp-activity-mix-heading">Actividad actual · Activos e inactivos</h3>
      <p className="rp-compare__note">
        La actividad es una dimensión independiente de los siete estados I03.
        Cada porcentaje usa I01 de su cohorte como denominador; B−A son puntos
        porcentuales entre fotografías independientes, no cambios de actividad
        de pedidos específicos ni un seguimiento histórico.
      </p>
      <p className="rp-compare__note">
        Observación A: <time>{result.observedAtA}</time>.
        {' '}Observación B: <time>{result.observedAtB}</time>.
      </p>
      <div className="rp-compare__scroll">
        <table className="rp-compare__table">
          <caption>Distribución de actividad actual, separada de la composición I03.</caption>
          <thead><tr>
            <th scope="col">Actividad actual</th>
            <th scope="col">A · cantidad</th><th scope="col">A · participación</th>
            <th scope="col">B · cantidad</th><th scope="col">B · participación</th>
            <th scope="col">B−A · pp descriptivos</th>
          </tr></thead>
          <tbody>
            {result.rows.map((row) => (
              <tr key={row.code}>
                <th scope="row">{row.label}</th>
                <td>{integer.format(row.countA)}</td>
                <td>{percentage(row.shareAPct)}</td>
                <td>{integer.format(row.countB)}</td>
                <td>{percentage(row.shareBPct)}</td>
                <td>{delta(row.differencePoints)}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
      <p className="rp-compare__note">
        I01 A: {integer.format(result.i01A)}; I01 B: {integer.format(result.i01B)}.
        Los estados vencido/cancelado de I03 no equivalen a inactividad;
        no sumar ni cruzar ambas dimensiones. Si I01 = 0 no existe participación calculable.
      </p>
    </section>
  );
}
