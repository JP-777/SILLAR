import type { FullResult, Reporter, Suite } from '@playwright/test/reporter';

/** Una prueba omitida o sin resultado impide acreditar la suite completa. */
export default class CeroOmisionesReporter implements Reporter {
  private suite: Suite | undefined;

  onBegin(_config: unknown, suite: Suite): void {
    this.suite = suite;
  }

  async onEnd(result: FullResult): Promise<{ status: FullResult['status'] }> {
    const pruebas = this.suite?.allTests() ?? [];
    const omitidas = pruebas.filter(prueba => prueba.results.length === 0
      || prueba.results.some(ejecucion => ejecucion.status === 'skipped'));
    if (pruebas.length === 0 || omitidas.length > 0) {
      console.error(`La puerta E2E exige cero omisiones: ${omitidas.length} omitidas de ${pruebas.length} pruebas.`);
      for (const prueba of omitidas) console.error(`  Sin acreditar: ${prueba.titlePath().join(' › ')}`);
      return { status: 'failed' };
    }
    return { status: result.status };
  }
}
