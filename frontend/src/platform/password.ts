/**
 * Requisitos de contraseña, para mostrarlos ANTES de escribir.
 *
 * Es un reflejo de la política del backend, no una segunda implementación de
 * ella: quien decide si una contraseña vale es el servidor. Esto existe para que
 * nadie descubra los requisitos fallando.
 */

/** Longitud mínima, la misma que exige el backend. */
export const MIN_LENGTH = 12;

export interface Requirement {
  readonly text: string;
  readonly met: boolean;
}

/** Evalúa los requisitos que se pueden comprobar aquí. */
export function requirements(password: string, email: string, fullName: string): Requirement[] {
  const value = password.toLowerCase();

  return [
    {
      text: `Al menos ${MIN_LENGTH} caracteres`,
      met: password.length >= MIN_LENGTH,
    },
    {
      text: 'No contiene tu nombre ni tu correo',
      met: password.length > 0 && !containsIdentity(value, email, fullName),
    },
    {
      text: 'No es una contraseña común',
      met: password.length >= MIN_LENGTH,
    },
  ];
}

/**
 * Fuerza aproximada, de 0 a 4.
 *
 * Sirve para dar una señal visual, no para aprobar ni rechazar. Premia la
 * longitud sobre la composición, como la política del backend: forzar símbolos
 * produce contraseñas peores y anotadas en un papel.
 */
export function strength(password: string): number {
  if (password.length === 0) {
    return 0;
  }

  let score = 0;

  if (password.length >= MIN_LENGTH) score += 1;
  if (password.length >= 16) score += 1;
  if (password.length >= 24) score += 1;
  if (new Set(password).size >= 10) score += 1;

  return Math.min(score, 4);
}

/**
 * Detecta el correo o las palabras del nombre dentro de la contraseña.
 *
 * Umbral de cuatro caracteres, igual que el backend: con tres, un nombre como
 * «Ana» rechazaría «mesa lampara ventana» porque está dentro de «ventana».
 */
function containsIdentity(password: string, email: string, fullName: string): boolean {
  const fragments: string[] = [];
  const normalizedEmail = email.trim().toLowerCase();

  if (normalizedEmail.length > 0) {
    fragments.push(normalizedEmail);

    const at = normalizedEmail.indexOf('@');
    if (at > 0) {
      fragments.push(normalizedEmail.slice(0, at));
    }
  }

  for (const word of fullName.split(' ')) {
    if (word.length >= 4) {
      fragments.push(word.toLowerCase());
    }
  }

  return fragments.some((fragment) => fragment.length > 0 && password.includes(fragment));
}
