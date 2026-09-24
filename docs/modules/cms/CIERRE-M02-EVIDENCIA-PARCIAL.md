# M02 — Evidencia parcial de cierre

**Base:** `21983d1f5491c87f7ff61d8d21e748d5483f9ff1`

**Rama:** `test/m02-cierre-evidencia`

**Veredicto:** 10 PASS, 1 FAIL. No constituye cierre M02.

## Ejecución de los once criterios pendientes

| Criterio | Estado | Evidencia |
|---|---|---|
| 3 | PASS | Desactivación, arranque, menú, portada y pie |
| 5 | PASS | Fallback móvil e imágenes con proporciones distintas |
| 13 | PASS | Cambio efectivo del slug y del enlace público |
| 16 | PASS | Precios variables y sin precio |
| 17 | PASS | Gratis puro frente a gratis más presentación de pago |
| 18 | PASS | Retirada de la presentación más cara |
| 19 | PASS | Cambio de precio propagado al destacado |
| 21 | FAIL | Diferencia visual entre CMS y Catálogo sin fotografía |
| 24 | PASS | Cero solicitudes al selector con Catálogo inactivo |
| 25 | PASS | Mensaje explícito para búsquedas por prefijo |
| 32 | PASS | Móvil, escritorio, teclado y comprobación de colores |

Los resultados PASS corresponden a las afirmaciones de la
primera batería de pruebas. La validación deliberadamente
roja de las barreras sigue pendiente.

## Hallazgo C21 — No corregido

La tarjeta sin foto de Catálogo utiliza el componente `NoPhoto`,
con categoría y nombre dentro del espacio de la fotografía.

La tarjeta de CMS omite ese espacio cuando no hay imagen,
aunque sí muestra el nombre, la categoría y el enlace público.

La prueba comprobó la existencia de esos datos y falló
únicamente al exigir la presentación equivalente a Catálogo.

**Clasificación:** diferencia funcional de presentación
respecto de la equivalencia exigida por el criterio 21.

**Acción:** registrada; no se modifica producción ni se rebaja
la prueba para conseguir un resultado verde.

## Pendiente

- Ejecutar y registrar la comprobación deliberadamente roja
  de cada barrera nueva, verificando ambas direcciones.
- Mantener C21 en FAIL hasta una corrección autorizada
  y verificada fuera de este encargo de evidencia.
- No declarar los 33 criterios en PASS.
- No ejecutar el criterio innegociable como certificado
  final mientras no estén aprobados los 33.
- No marcar M02 como cerrado en el ROADMAP.

## Registro

Ejecución completa:
`docs/modules/cms/evidencias/ONCE-CRITERIOS-20260923.txt`

Pruebas:
`e2e/tests/m02-cierre-evidencia.spec.ts`

SHA256 del log original: `d12a3a42173d31047f893d52f9cea803e289412dd91da151274c0e387e3a4bda`
