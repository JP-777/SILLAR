# M04 — Propuesta de cierre

**Estado:** cierre propuesto, pendiente de aprobación.

**Candidato certificado:** `9f9015b4186a55f9f305222b320477e79aea7028`

**Árbol Git certificado:** `c099ca88abcf36c561e8f4cc69db18ac35988b8b`

**Fecha de certificación:** 26 de septiembre de 2026,
America/Lima.

**Resultado:** 19/19 criterios con evidencia.
Dos puertas canónicas consecutivas: 6/6 PASS cada una.

## Tabla de los 19 criterios

Se conservan los enunciados de
`docs/modules/crm/SPEC.md`, sección 9.

| N.º | Criterio | Estado | Evidencia |
|---|---|---|---|
| 1 | Un visitante se registra, recibe el correo de verificación y verifica | PASS | Bitácora y puerta integrada |
| 2 | Un cliente sin verificar entra y ve su perfil; el estado dice que le falta verificar | PASS | SPEC §9 y puerta integrada |
| 3 | Recuperar la contraseña funciona, y **el enlace no sirve dos veces** | PASS | SPEC §9 y puerta integrada |
| 4 | Se entra desde dos navegadores, se restablece en uno **y el otro deja de valer** | PASS | SPEC §9 y puerta integrada |
| 5 | **La base de datos es la autoridad sobre los blancos de `customers.email`:** por SQL directo, rechaza cualquier correo que empiece o termine con un carácter para el que el runtime .NET devuelve `char.IsWhiteSpace == true`, exactamente el conjunto que recorta `String.Trim()`; un correo limpio y caracteres que .NET no recorta, como U+200B, no son rechazados por esta regla. La comparación de la restricción se hace bajo `COLLATE "C"`, nunca bajo `core.es_ci`. **Decisión del líder técnico, 22 de septiembre de 2026 — America/Lima.** | PASS | Bitácora y puerta integrada |
| 6 | La respuesta de recuperar **es idéntica** con un correo registrado y con uno que no existe | PASS | SPEC §9 y puerta integrada |
| 7 | Los intentos fallidos **retrasan sin bloquear**, y una cuenta ajena no se puede dejar fuera | PASS | SPEC §9 y puerta integrada |
| 8 | El personal crea una ficha sin cuenta e **invita**; la persona pone su contraseña con el enlace | PASS | SPEC §9 y puerta integrada |
| 9 | Alguien se registra con el correo de una ficha existente **y se enlaza a ella**, sin duplicar | PASS | SPEC §9 y puerta integrada |
| 10 | Un cliente añade dos direcciones y cambia la preferida | PASS | SPEC §9 y puerta integrada |
| 11 | Un cliente de baja **sigue en la ficha** y no puede entrar | PASS | SPEC §9 y puerta integrada |
| 12 | **Las notas internas no aparecen en ninguna respuesta pública** — afirmado por prueba | PASS | SPEC §9 y puerta integrada |
| 13 | Ninguna contraseña aparece en registros ni en respuestas — afirmado por prueba | PASS | Bitácora y puerta integrada |
| 14 | **Una petición al panel con la cookie de cliente es rechazada** — afirmado por prueba | PASS | SPEC §9 y puerta integrada |
| 15 | Con el módulo de pedidos ausente, la ficha **enseña su hueco explicado** y nada falla | PASS | SPEC §9 y puerta integrada |
| 16 | Se desinstala M04 y **CORE y el catálogo siguen enteros**; se reinstala y arranca | PASS | SPEC §9 y puerta integrada |
| 17 | Sin la capacidad de correo configurada, **registrarse sigue funcionando** y quien administra ve que el envío falló | PASS | Bitácora y puerta integrada |
| 18 | Todos los endpoints en Swagger, con ejemplos copiables | PASS | SPEC §9 y puerta integrada |
| 19 | **La puerta pasa dos veces seguidas** (`node scripts/verificar.mjs`) | PASS | Dos puertas consecutivas, 26/09 |

## Doble puerta

**Puerta 1:** 2026-09-25 23:59:23 -0500 a 2026-09-26 00:24:29 -0500, RC=0, 6/6 PASS. Registro: `docs/modules/crm/evidencias/M04-PUERTA-1-20260926.txt`.

**Puerta 2:** 2026-09-26 00:24:29 -0500 a 2026-09-26 00:51:35 -0500, RC=0, 6/6 PASS. Registro: `docs/modules/crm/evidencias/M04-PUERTA-2-20260926.txt`.

**SHA invariable:** `9f9015b4186a55f9f305222b320477e79aea7028`.

**Árbol invariable:** `c099ca88abcf36c561e8f4cc69db18ac35988b8b`.

No se modificó el candidato entre ejecuciones.

Los registros versionados contienen extractos verificables;
los originales íntegros y sus hashes SHA-256 están
identificados en la bitácora.

## Propuesta

Se solicita aprobación del cierre de M04 a partir
del candidato certificado.

El commit que añade este informe y actualiza el
ROADMAP es exclusivamente documental.

**Fusión:** no realizada ni autorizada.
