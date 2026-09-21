# Verificación visual del panel — CORE

**Para:** JP.
**Estado:** ✅ **COMPLETADA el 20 de septiembre de 2026 — 5/5 PASS** sobre
`6efc00de3a6a1a0587d0df7f3e949c9f0c985635`.
**Duración:** unos diez minutos, y una parte sin abrir la aplicación.
**Por qué existe:** hay cosas que un modelo no puede afirmar. Quedaron reducidas a cinco
juicios humanos y JP comprobó los cinco antes del cierre formal de Fase 1.

> **Este documento se redactó el 16 de agosto, cuando el proyecto no tenía Playwright.** El
> arnés `e2e/` entró el 18 y absorbió la mayor parte. Lo que sigue abajo es el residuo: lo que
> queda después de quitar todo lo que hoy se afirma en código. **Nada se borró por descuido**
> — la sección «Lo que ya no te toca» dice qué prueba cubre cada cosa que antes estaba aquí.

**Cómo anotar cada fallo**, una línea:

```
QUÉ PASÓ      El diálogo de borrado no se cierra con Escape
QUÉ ESPERABA  Que se cierre y no borre nada
DÓNDE         /admin/medios, al borrar una imagen
```

No hace falta que diagnostiques nada. Con esas tres líneas se corrige.

---

## A. Verificación completada — 5/5 PASS

### A.1 · Desde la galería de capturas, sin levantar nada

```
e2e/screenshots/index.html
```

Se genera sola al terminar cada corrida de `pnpm test` en `e2e/`, con cada paso en claro y
oscuro uno al lado del otro. Abre el archivo en el navegador.

**1 · ✅ PASS — Que los estados de tarjeta sigan distinguiéndose entre sí, en oscuro.** `axe-core` mide
el contraste de cada texto contra su fondo y por eso cazó los cuatro fallos del 18 — pero **no
mide si «Activo» y «Bloqueado» se parecen demasiado el uno al otro**. Son dos preguntas
distintas y solo la primera está automatizada. Mira la captura de las cuatro variantes en oscuro
y responde: ¿se distinguen de un vistazo, sin leer la insignia?

**2 · ✅ PASS — Que las frases suenen a persona.** Se afirma en código que ningún conflicto dice «Ha
ocurrido un error»; no se puede afirmar que lo que dice en su lugar esté bien escrito. Lee los
textos de las capturas —el diálogo de confirmación, el aviso del 409— como los leería quien
administra su negocio.

**3 · ✅ PASS — Que el anillo de foco se pinte al abrir con el ratón.** Mira la captura
`teclado/…foco-tras-abrir-el-dialogo-con-raton` y di si se ve. Es comprobación única, no
regresión: lo que una prueba puede afirmar es que el foco **está** en el diálogo, no que el
anillo se **vea**.

### A.2 · Con la aplicación delante

**4 · ✅ PASS — El repaso visual del panel completo y de Swagger, en una sola pasada.** Es el punto que
`PENDIENTES.md` §13 llama «verificación humana de CORE», y son dos miradas distintas: que el
panel entero se vea bien pantalla por pantalla, y que Swagger se lea —los cuerpos de ejemplo ya
están automatizados; aquí queda el juicio—. En el mismo recorrido, **comprueba
`:focus-visible` interactuando con el ratón**: la captura del punto 3 enseña un momento; usar el
panel con el ratón enseña el resto.

**5 · ✅ PASS — Que la espera del reinicio resulte razonable.** El arnés afirma que la superposición
aparece y desaparece sola, y que la sesión sobrevive (`e2e/tests/modulos.spec.ts:219`). Lo que
no puede afirmar es si esos segundos se hacen largos **en una demostración de venta**, que es
donde importa.

Para los puntos 4 y 5 hace falta levantar el entorno:

```powershell
docker compose stop api          # libera el 5080; NO mates el proceso por puerto
$env:Modules__IncludeDemoModules = "true"
dotnet run --project backend\Sillar.Api
```

```powershell
cd frontend
pnpm dev
```

Entra en `http://localhost:5173`, ve a **Módulos**, activa uno inactivo y cronométralo a ojo.
Al terminar:

```powershell
docker compose start api
```

---

## B. Lo que ya no te toca

Estaba en esta guía y hoy lo afirma el arnés. Se conserva la lista para que se vea que salieron
por estar cubiertas, no por descuido:

| Antes era | Hoy lo cubre |
|---|---|
| Las cuatro variantes de tarjeta | `e2e/tests/modulos.spec.ts:15-28` |
| CORE sin interruptor (no uno deshabilitado) | `e2e/tests/modulos.spec.ts:30-42` |
| La bloqueada nombra lo que falta y enlaza a su tarjeta | `e2e/tests/modulos.spec.ts:44-66` |
| El aviso previo al reinicio, y que diga la verdad en las dos instalaciones | `e2e/tests/modulos.spec.ts:68-124` |
| El 409 nombra a quien bloquea y no reinicia nada | `e2e/tests/modulos.spec.ts:126-183` |
| El ciclo de activación completo, con reinicio real y sesión viva | `e2e/tests/modulos.spec.ts:185-222` |
| Texto que pierda contraste en oscuro | `e2e/fixtures/themes.ts:52-53`, en los dos temas y en cada paso |
| Que el tema oscuro sea alcanzable de verdad | `e2e/tests/tema.spec.ts:19` |
| Que ninguna pantalla suelte un error en consola | `e2e/fixtures/base.ts:48` |
| Que se pinte una tarjeta por módulo (ADR-019) | `e2e/tests/modulos.spec.ts:30` |
| Dos pestañas escribiendo a la vez, sin 403 (ADR-012) | `e2e/tests/sesion-csrf.spec.ts:31` |
| Tab, foco visible, foco atrapado y Escape sin ejecutar | `e2e/tests/teclado.spec.ts:57-115` |
| Los `PENDIENTE_DEFINIR` destacados y contados | `e2e/tests/configuracion.spec.ts:22` |
| El interruptor deshabilitado **con su razón** para el rol `admin` | `e2e/tests/configuracion.spec.ts:41` |
| Los tres rechazos de subida, **distintos entre sí** | `e2e/tests/medios.spec.ts:48` |
| Subir repetido avisa en vez de fallar | `e2e/tests/medios.spec.ts:89` |
| El aviso de baja, sin recuento y sin «Aceptar» | `e2e/tests/medios.spec.ts:109` |
| Ninguna pantalla dice «Ha ocurrido un error» | `e2e/tests/transversal.spec.ts:36` |
| Ningún botón se llama «Aceptar» | `e2e/tests/transversal.spec.ts:49` |
| Ninguna pantalla enseña un `uuid` | `e2e/tests/transversal.spec.ts:81` |

---

## C. Lo que decía esta sección y ya no es cierto

**Auditoría ya no enseña identificadores, y esta guía afirmaba lo contrario.** Decía que era un
defecto abierto, que `AuditPage.tsx` pintaba `entityId` en crudo y que estaba codificado como
defecto conocido con `test.fail`. Las tres cosas dejaron de ser verdad:

- el identificador vive detrás de un `<details>` que se despliega a voluntad
  (`frontend/src/modules/core/pages/AuditPage.tsx:277-284`);
- la prueba lo afirma **normalmente**, sin `test.fail` —no queda ninguno en `e2e/`— y afirma las
  dos mitades a la vez: que no se presenta y que no se ha perdido
  (`e2e/tests/transversal.spec.ts:130`);
- la decisión de producto que faltaba está tomada: **presentar** es poner un identificador
  delante de quien no lo pidió; **responder** es dárselo a quien despliega la fila que está
  investigando. Se prohíbe lo primero, no lo segundo.

**No queda ningún defecto conocido abierto en esta guía.** La sección A también quedó cerrada:
JP realizó los cinco juicios humanos y registró **5/5 PASS**. Lo automatizable permanece en la
tabla B; lo que exigía juicio humano ya no es un pendiente de Fase 1.
