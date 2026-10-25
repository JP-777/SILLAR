import { test as base } from './base.js';

/**
 * Las pruebas de ciclo llegan al final de una corrida larga y reinician el
 * producto varias veces. Compartir con ellas el worker que ejecutó las specs
 * funcionales conserva también el proceso Chromium y todos los recursos que
 * este acumuló, aunque Playwright sí cierre el contexto de cada prueba.
 *
 * Esta opción worker-scoped forma parte del hash con el que Playwright decide
 * si puede reutilizar un worker. Cada spec de ciclo le asigna su propia URL,
 * por lo que el runner cierra el worker anterior y arranca un Chromium limpio
 * antes de ejecutar ese archivo. No cambia `workers: 1`, el orden serial ni el
 * stack compartido.
 */
type OpcionesDeWorkerDeCiclo = {
  aislamientoDeNavegador: string;
};

export const test = base.extend<{}, OpcionesDeWorkerDeCiclo>({
  aislamientoDeNavegador: [
    async ({}, use) => {
      await use('');
    },
    { scope: 'worker', option: true },
  ],
});

export { duringExpectedOutage, expect } from './base.js';
