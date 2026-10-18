/** D17: se muestra solo sobre fotografías D8 verificadas y compuerta D15 APTO. */
import type { StatusMixResult } from '../services/cohortStatusMix';

const pct = new Intl.NumberFormat('es-PE', { maximumFractionDigits: 1 });
const integer = new Intl.NumberFormat('es-PE', { maximumFractionDigits: 0 });
function formatShare(value: number | null): string {
  return value === null ? 'No calculable (I01 = 0)' : `${pct.format(value)} %`;
}
function formatDifference(value: number | null): string {
  return value === null ? '—' : `${value > 0 ? '+' : ''}${pct.format(value)} pp`;
}

export function CohortStatusMixPanel({ result }: { readonly result: StatusMixResult }) {
  if (result.kind !== 'available') return null;
  return (
    <section className="rp-compare__mix" aria-labelledby="rp-status-mix-heading">
      <h3 id="rp-status-mix-heading">I03 · Composición de estados actuales de ambas cohortes</h3>
      <p className="rp-compare__note">
        Porcentajes del estado actual sobre I01 de cada cohorte; B−A se expresa en puntos
        porcentuales. Son dos fotografías independientes, no cambios de estado ni evolución histórica.
      </p>
      <p className="rp-compare__note">
        Observación A: <time>{result.observedAtA}</time>. Observación B: <time>{result.observedAtB}</time>.
      </p>
      <div className="rp-compare__scroll">
        <table className="rp-compare__table">
          <caption>Composición relativa por estado; los recuentos siguen siendo los de I03.</caption>
          <thead>
            <tr>
              <th scope="col">Estado actual</th>
              <th scope="col">A · cantidad</th><th scope="col">A · participación</th>
              <th scope="col">B · cantidad</th><th scope="col">B · participación</th>
              <th scope="col">B−A · puntos porcentuales</th>
            </tr>
          </thead>
          <tbody>
            {result.rows.map(row => (
              <tr key={row.code}>
                <th scope="row">{row.label}</th>
                <td>{integer.format(row.countA)}</td><td>{formatShare(row.shareAPct)}</td>
                <td>{integer.format(row.countB)}</td><td>{formatShare(row.shareBPct)}</td>
                <td>{formatDifference(row.differencePoints)}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
      <p className="rp-compare__note">
        I01 A: {integer.format(result.i01A)}; I01 B: {integer.format(result.i01B)}.
        Una cohorte vacía carece de denominador porcentual. I04 y activo/inactivo
        no se reclasifican ni se mezclan con estados.
      </p>
    </section>
  );
}
