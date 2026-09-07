/**
 * Si el stack e2e que vamos a levantar ya está en pie, ¿es nuestro o de otro?
 *
 * **De dónde sale.** El 5 de septiembre de 2026, dos frentes lanzaron la puerta
 * con dieciséis segundos de diferencia y el mismo `COMPOSE_PROJECT_NAME`. El
 * segundo murió en el puerto del Vite, que es la parte inofensiva. La parte
 * peligrosa no llegó a ocurrir por esos segundos: `composeDown()` lleva `-v` y es
 * incondicional, así que el que llega segundo **destruye el stack del primero a
 * mitad de suite**, contenedores y volumen, y la corrida ajena muere con un
 * fallo que no se parece en nada a su causa.
 *
 * La decisión vive aparte y es pura —sin docker, sin disco— para poder
 * provocarla en las dos direcciones sin levantar nada. Es la lección de la
 * guarda de `.media-e2e`: una barrera que solo se puede provocar levantando
 * medio sistema es una barrera que nadie va a provocar.
 */

/**
 * @param dirDelStackEnPie Worktree desde la que se levantó el stack que ya
 *   existe, según la etiqueta que pone docker compose, o `null` si no hay stack.
 * @param nuestroDir Raíz de la worktree que quiere correr ahora.
 * @returns `null` si se puede seguir; si no, qué pasa y qué hacer.
 */
export function problemaDeStackAjeno(
  dirDelStackEnPie: string | null,
  nuestroDir: string,
): string | null {
  if (dirDelStackEnPie === null) {
    // No hay nada en pie. El caso normal.
    return null;
  }

  if (dirDelStackEnPie === nuestroDir) {
    // Restos de una corrida nuestra anterior que murió sin limpiar, o un
    // E2E_KEEP_STACK=1. Es exactamente para lo que existe composeDown(): se
    // sigue y se destruye, porque destruir lo propio no le rompe el día a nadie.
    return null;
  }

  return (
    `El stack e2e ya está en pie, y lo levantó OTRA worktree:\n` +
    `    ${dirDelStackEnPie}\n` +
    `  Esta es:\n` +
    `    ${nuestroDir}\n\n` +
    '  Las dos usan el mismo COMPOSE_PROJECT_NAME, así que continuar destruiría\n' +
    '  su stack con `docker compose down -v` —contenedores y volumen— a mitad de\n' +
    '  su suite. Su corrida moriría con un fallo que no se parece a su causa.\n\n' +
    '  Qué hacer, por orden de preferencia:\n' +
    '    1. Espera a que termine. Es una corrida, no una tarde.\n' +
    '    2. Dale a ESTA worktree su propia identidad en e2e/.env.e2e:\n' +
    '       COMPOSE_PROJECT_NAME, POSTGRES_PORT, API_PORT, FRONTEND_PORT y el\n' +
    '       Port= de ConnectionStrings__Default, que no se deduce de los otros.\n' +
    '       Se modifica y NO se commitea: la identidad es de la worktree, no de\n' +
    '       la rama. Ver docs/ENTORNO.md, hallazgo 5.\n'
  );
}
