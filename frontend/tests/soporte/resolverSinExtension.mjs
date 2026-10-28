/**
 * Solo pruebas. El código de la aplicación importa sin extensión (lo resuelve
 * Vite); node no. Este gancho prueba `.ts` y `.tsx` para las rutas relativas.
 */
export async function resolve(specifier, context, next) {
  if (!specifier.startsWith('.') || /\.[cm]?[jt]sx?$/.test(specifier)) return next(specifier, context);
  for (const extension of ['.ts', '.tsx']) {
    try {
      return await next(specifier + extension, context);
    } catch {
      // siguiente extensión
    }
  }
  return next(specifier, context);
}
