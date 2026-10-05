# M07 — Cierre E2E: la C9 completa y la cobertura del plan

Creado: 05/10/2026, America/Lima · Última verificación: 05/10/2026, America/Lima
Commit base comprobado: `1a7417a919beaa65e0d79ed2c56236b36ba19fcd`

**Qué cierra.** La C9 de `ESCALADAS-M07.md:202` pedía dos cosas: que
`Sillar.Api` referenciara `Sillar.Modules.B2B` y que **la etapa 4 de la puerta y
el arnés e2e** aplicaran sus migraciones. La primera mitad entró en `aecf4aa`.
Esta es la segunda, con la cobertura E2E que el `PLAN-DE-PRUEBAS-M07.md` ya
exigía.

**Lo que estaba mal, dicho sin adorno.** Hasta hoy `e2e/setup/migrate.ts` migraba
CORE, Catalog, Cms y CRM, y `global-setup.ts` activaba `catalog`, `cms` y `crm`.
**M07 no existía en el escenario e2e:** ni schema, ni módulo activo, ni una sola
de sus rutas montada. La etapa podía estar entera en verde sin decir nada de
M07 — el mismo agujero que tuvieron M02 y M04 antes de sus respectivas líneas de
migración, y que ya está documentado con esas palabras en el propio
`migrate.ts`.

---

## 1 · Lo que cambió en el arnés

| Archivo | Cambio |
|---|---|
| `e2e/setup/migrate.ts` | `applyMigrations('Sillar.Modules.B2B')`, **el último**: sus cinco claves foráneas cruzadas apuntan a `catalog` y `crm` (dependencias duras, `B2BModule.cs:57`), así que esas tablas tienen que existir antes |
| `e2e/setup/migrate.ts` | `'b2b'` en el bucle de seeds. Está intencionalmente vacío y **se aplica igual**: no hacerlo sería una asimetría que solo se nota el día que deje de estarlo |
| `e2e/setup/global-setup.ts` | `activateModule(session, 'b2b')`, **después de `catalog` y `crm`**: activarlo antes lo rechaza la propia plataforma |
| `e2e/tests/zz-instalacion.spec.ts` | Suelta `b2b` antes de `catalog`. **No es un apaño: es la guarda C6 funcionando** (ver §3) |

---

## 2 · Las cuatro specs, y por qué dos llevan `zz-`

| Spec | Criterios del plan | Reinicia el proceso | Toca schemas |
|---|---|---|---|
| `b2b-cliente.spec.ts` | 1.1–1.10, 2.6, 2.7 | No | No |
| `b2b-panel.spec.ts` | 3.1–3.6 | No | No |
| `zz-b2b-ciclo.spec.ts` | 4.1, 4.2, 4.3, 4.5, 4.6, 4.7 | **Dos veces** | No |
| `zz-b2b-instalacion.spec.ts` | 5.1–5.7 | **Dos veces** | **`b2b` y `catalog`** |

El plan nombraba genéricamente `e2e/tests/b2b-*.spec.ts`. Las dos últimas llevan
`zz-` porque **el comportamiento real del arnés manda sobre el patrón del
nombre**: Playwright ordena los archivos alfabéticamente y `zz-` es la
convención para «corre al final, cuando nadie más va a mirar»
(`zz-desmontaje.spec.ts:22`). La cabecera de cada una deja escrito que el
prefijo es deliberado, que dependen de `workers: 1` y `fullyParallel: false`, que
reordenarlas contamina a las posteriores, y que **reconstruir el estado al final
es parte del criterio y no limpieza**.

### Menú y router no son el mismo número

M07 pone **3 entradas de menú** y **4 rutas de router**: el detalle de una
cotización, `/admin/solicitudes/cotizaciones/:id`, no es entrada de menú porque
necesita un identificador. `zz-b2b-ciclo.spec.ts` lo prueba **con una cotización
real** —activo, desaparecido al desactivar y de vuelta al reactivar—. Quedarse
fuera por no tener entrada propia habría dejado sin cubrir justamente la
pantalla donde se cobra.

### Las API al apagar M07 se ejercen, no se postulan

Con M07 desactivado no se afirma «`/api/b2b/*` da 404» sobre una URL inventada,
que sería un falso verde garantizado. Se ejercen **cuatro rutas reales**: dos del
grupo de cliente (`/api/b2b/my-requests` y la cotización por su número de
verdad) y dos del panel (la bandeja y el detalle por su id de verdad). Y además
**Swagger no declara ni un path** bajo `/api/b2b` ni `/api/admin/b2b`: un módulo
inactivo no mapea sus endpoints, así que tampoco puede quedar documentado lo que
no existe.

### El criterio visual de M07

M07 **no aporta superficie pública**: ni sección de portada, ni bloque de pie, ni
acción en la ficha de producto. No se inventa una prueba de portada que no
corresponde. Lo que se comprueba al apagarlo es: ningún enlace residual del
grupo «Solicitudes», ninguna de sus cuatro rutas montada, ninguna de sus tres
pantallas ni su contenedor, el resto del panel operativo, y ningún error de
arranque.

---

## 2a · Resultado focal medido

```
npx playwright test tests/b2b-cliente.spec.ts tests/b2b-panel.spec.ts \
                    tests/zz-b2b-ciclo.spec.ts tests/zz-b2b-instalacion.spec.ts

  12 passed (5.6m)        0 omitidas
```

El ciclo completo tarda 52 s y la instalación 1,7 min: son las dos que reinician
el proceso y sueltan schemas, y por eso llevan `zz-`.

**Hubo dos rojas antes, y se guardan aparte** —una barrera que solo se ha visto
en verde no se ha visto— en `evidencias/E2E-FOCAL-M07-ROJAS.txt`:

| | Qué pasó | De quién era el fallo |
|---|---|---|
| Roja 1 · 4/11 | Registrar un cliente daba 403: M04 exige `Sec-Fetch-Site: same-origin` en las escrituras públicas previas a la sesión y `APIRequestContext` no manda Fetch Metadata. El precedente usa `page.evaluate` por esto exacto | **de la prueba** |
| Roja 1 · 2 de esos 11 | `catalog/99_drop.sql` salía con código 3 tras retirar M07 | **del escenario** — es `sales`, ver §6 |
| Roja 2 · 10/2 | Comparaba cuerpos de 404 con el `traceId` dentro, y un booleano de `psql` que imprime `t` solo y `true` concatenado | **de la prueba** |

Las dos veces el rojo fue útil: la primera destapó el hallazgo de Sales, y la
segunda dos aserciones que habrían dado rojos ciertos y mudos más adelante.

---

## 2b · Matriz criterio → prueba que lo acredita

| Criterio | Qué afirma | Spec · prueba |
|---|---|---|
| 1.1–1.4 | Sin sesión de cliente, las cuatro rutas dan 401 y **no dejan fila** | `b2b-cliente` · «Sin sesión de cliente, las cuatro rutas dan 401 y no dejan ninguna fila» |
| 1.5 · 1.6 | Las dos poblaciones no se mezclan, en los dos sentidos | `b2b-cliente` · «La cookie del panel no sirve como cliente, y la de cliente no abre el panel» |
| 1.7 | Sin CSRF, 403 y sin fila; **con** CSRF, 201 | `b2b-cliente` · «Con sesión y sin token CSRF, crear da 403 y no deja fila» |
| 1.8 · 1.9 · 1.10 | Cotización ajena ≡ inexistente; `staff_notes` nunca sale; el `customer_id` es el de la sesión | `b2b-cliente` · «La cotización de otro cliente responde igual que una inexistente, y sin notas internas» |
| 2.6 · 2.7 | Cupo agotado → 429 con `Retry-After`, frase de persona, sin fila | `b2b-cliente` · «Pasado el cupo, la cuenta recibe 429 con Retry-After…» |
| 3.1 · 3.2 | Tabla de permisos aplicada; tras cada 403, la base **sin cambiar**; y el `admin` sí pasa | `b2b-panel` · «Un editor lee y escribe lo suyo, y las cuatro rutas de admin le dan 403 sin cambiar nada» |
| 3.3 | Toda escritura del panel sin CSRF: 403 y nada cambia | `b2b-panel` · «Toda escritura del panel sin token CSRF da 403 y no cambia nada» |
| 3.4 | Auditoría `b2b` cuyo resumen **nombra la cotización** por su número visible | `b2b-panel` · «Toda escritura del panel deja auditoría b2b…» |
| 3.5 | Los 20 paths de M07 en Swagger, con resumen | `b2b-panel` · «Las rutas de M07 que esta suite ejerce y las de Swagger son el mismo conjunto» |
| 3.6 | Una `enviada` no admite edición; el total no cambia | `b2b-panel` · «Una cotización enviada no admite edición de líneas…» |
| 4.1 · 4.2 · 4.3 · 4.5 · 4.6 · 4.7 | El ciclo entero: 3 enlaces / 4 rutas activos, 409 nombrando a M07 al tocar M01 y M04, apagado sin enlace ni ruta ni pantalla, Swagger sin sus paths, datos intactos, y todo de vuelta | `zz-b2b-ciclo` · «M07 se usa, se apaga y vuelve…» |
| 5.1–5.7 | Schema con su historial dentro, idempotencia, guardas de C6, soltar sin tocar a nadie, host operativo, reinstalar, y **las 5 FK cruzadas de vuelta** | `zz-b2b-instalacion` · «M07 se instala, se suelta y se reinstala…» |
| **4.4** | Acción de M07 en la ficha de producto | **HOLD POR CAPACIDAD AÚN INEXISTENTE** — depende de C1, que no existe. No es `skipped` |
| 2.1–2.5 | Cupo por cuenta, nivel puro | `Sillar.Modules.B2B.Tests` — el plan los marca «Pura», no e2e |

---

## 3 · Las guardas de C6, que nunca habían dicho no

`database/modules/catalog/99_drop.sql` y `database/modules/crm/99_drop.sql`
llevan desde `51d0275` una guarda que se niega a soltar el schema mientras otro
módulo instalado dependa de él de forma dura. **M07 es ese módulo** —cinco claves
foráneas: tres a `crm.customers`, dos a `catalog`— y hasta hoy esa guarda no
había dicho no a nadie en ninguna corrida. Es el §2 de
`ANTES-DE-EMPEZAR-UN-MODULO.md`: una barrera que nunca se ha visto disparar.

`zz-b2b-instalacion.spec.ts` la provoca en las dos direcciones:

| Dirección | Qué se hace | Qué tiene que pasar |
|---|---|---|
| **Niega** | Con M07 instalado, soltar `catalog` y soltar `crm` | **Ambas rechazadas**, y los dos schemas **intactos fila por fila** — el código de salida solo dice que algo falló; la promesa de la guarda es «no se ha borrado nada», y eso se comprueba por el estado |
| **Deja pasar** | Con el schema de M07 ya soltado, soltar `catalog` | Prospera: el no era **por M07** |

La segunda se ejecuta sobre `catalog` y no sobre los dos. Soltar `crm` de verdad
se llevaría los clientes que las specs posteriores necesitan, y la mitad que
podía fallar en silencio es la primera. Queda dicho aquí para que nadie lo lea
como un olvido.

### El ciclo físico va unido al ciclo de producto

La spec no se queda en «el schema desapareció y volvió». El orden es:

1. desactivar M07 **por el mecanismo soportado** (el interruptor del panel);
2. comprobar que no quedan sus rutas ni sus enlaces;
3. `b2b/99_drop.sql`;
4. comprobar que **el host y los módulos ajenos siguen operativos** —M01, M04 y
   CORE responden 200 y el panel carga—;
5. `migrate()` + `seed()`;
6. reactivar M07;
7. comprobar que sus superficies vuelven;
8. contar **las cinco** claves foráneas hacia `crm` y `catalog` en
   `pg_constraint`.

Así los dos niveles quedan atados. Una prueba capaz de acreditar el esquema
mientras el producto queda en un estado imposible no acreditaría nada.

---

## 4 · Persistencia de datos: lo que ya decía el plan, nada nuevo

No se inventa ninguna política. Se comprueban las dos que ya existen, y son
distintas:

| Operación | Qué pasa con los datos | Dónde está dicho |
|---|---|---|
| **Desactivar** el módulo | **Se conservan.** Apagar no es borrar: un cliente que deja de pagar un mes y vuelve encuentra lo suyo donde lo dejó | 4.6 del plan; precedente `zz-desmontaje.spec.ts` |
| **Desinstalar** el schema | **Se van con el schema**, y no se llevan nada de nadie más | 5.4 del plan |

`zz-b2b-ciclo.spec.ts` compara recuentos **y los números visibles de las
cotizaciones**, uno a uno: un recuento igual con contenido distinto sería igual
de malo y no se vería.

---

## 5 · Lo que queda fuera, y por qué

| | Estado |
|---|---|
| **4.4** · acción de M07 en la ficha de producto | **HOLD POR CAPACIDAD AÚN INEXISTENTE.** Depende de C1, y hoy no hay superficie en la ficha (`ESCALADAS-M07.md:123-127`). **No es `skipped`**: la prueba no existe porque el mecanismo no existe. No se inventa la costura |
| **2.1–2.5** · cupo por cuenta, nivel puro | Ya cubiertos en `Sillar.Modules.B2B.Tests`; el plan los marca «Pura», no e2e |
| **Numeración multinodo** | Fuera de este trabajo por decisión del colíder. El hallazgo sigue en `RECOMPOSICION-M07-04-10-2026.md` §6 |

---

## 6 · HALLAZGO AJENO DETECTADO DURANTE M07 — NO CORREGIDO

**Archivo afectado:** `e2e/setup/migrate.ts`.

**Hecho observado:** el arnés e2e **no migra `Sillar.Modules.Sales`**. Tras este
cambio la lista es CORE, Catalog, Cms, CRM y B2B; M03 no está. `global-setup.ts`
tampoco activa `sales`, y `frontend/src/app/routes.tsx` no monta rutas de
`sales` —M03 no tiene frontend todavía—.

**Posible efecto:** es el mismo agujero que M07 tenía. La etapa e2e puede
ejecutar el producto completo sin que M03 exista en el escenario: sin su schema,
activar `sales` fallaría, y ninguna de sus superficies ni de sus endpoints se
cargaría nunca en una corrida verde. El §j que M03 cerró el 3 de octubre cubrió
la **etapa 4 de `scripts/verificar.mjs`** (`c1188f9`), no el arnés e2e.

**Quién lo separó:** **Chat 2 lo separó de M07** de forma expresa, para no
mezclar un arreglo de M03 con la candidata de M07. No se ha añadido `sales` a
`migrate.ts` en esta rama.

### Y resultó no ser teórico: bloquea una spec de plataforma

Al medirlo el 05/10/2026 el hallazgo creció, así que queda aquí con su medida.

**Lo que no se había tenido en cuenta:** el arnés migra cinco módulos a mano,
pero **la instalación va por `POST /api/setup`, que migra todos los módulos del
binario**. Desde `aecf4aa` el binario trae M07, y desde que M03 entró en `main`
trae también M03. Así que en el escenario e2e existen **también** los schemas
`b2b` y `sales` creados por la instalación, no por `migrate.ts`.

Medido sobre una base limpia con los cinco módulos migrados a mano más
`POST /api/setup` (201):

```
schemas tras migrar a mano          b2b catalog cms core crm
dependientes duros de catalog       b2b   hard   schema existe: t
                                    sales hard   schema existe: t
                                    cms   soft   schema existe: t
FK de otros schemas hacia catalog   b2b, sales
```

**Consecuencia:** `e2e/tests/zz-instalacion.spec.ts:114` —«El schema catalog se
elimina sin llevarse nada de core»— **estaba ya en rojo en esta rama antes de
escribir una sola spec de M07**, porque la guarda C6 se niega con `b2b` y con
`sales` presentes. Soltar `b2b` antes (lo que esta entrega añadió) es necesario
pero **no suficiente**: `sales` sigue bloqueando.

**Por qué no se arregla aquí.** Soltar `sales` en la spec no vale: `migrate.ts`
no lo migra, así que quedaría sin restaurar y el estado no se podría
reconstruir — y reconstruirlo es parte del criterio, no limpieza. El arreglo es
**añadir M03 al arnés**, que es trabajo de M03 y que Chat 2 separó de esta
candidata. Queda escrito, no corregido.

**Lo que sí se acotó:** `zz-b2b-instalacion.spec.ts` ya no intenta ejecutar el
drop de `catalog`. Comprueba, en su lugar, que **las dos señales que la guarda
lee y que son de M07** han desaparecido —sus claves foráneas, y su registro de
dependencia dura con schema presente— y que quien bloquea ahora **es otro y se
puede nombrar**. Si algún día esa lista saliera vacía, la prueba lo dice: el
drop prosperaría y habría que volver a la versión ejecutada.

---

## 7 · La barrera del despliegue, tres direcciones

`ModulosEnElDespliegueTests` (`aecf4aa`) se acreditó en las tres direcciones que
la doctrina exige, con evidencia separada:

| | Evidencia |
|---|---|
| 1 · árbol legal → verde | `evidencias/BARRERA-DESPLIEGUE-1-LEGAL.txt` |
| 2 · módulo sin ensamblado → rojo **nombrándolo** | `evidencias/BARRERA-DESPLIEGUE-2-ILEGAL.txt` |
| 3 · sabotaje de la propia detección → rojo, no verde silencioso | `evidencias/BARRERA-DESPLIEGUE-3-SABOTAJE.txt` |

La tercera es la que importa más de las tres: con `ProyectosDeModulo` devolviendo
lista vacía, «ninguno falta» es trivialmente cierto. La guarda
`proyectos.Length >= 4` es lo único que impide ese verde.

Ningún sabotaje queda en HEAD: comprobado con `git diff --stat` de los dos
archivos tocados y `grep -rn "SABOTAJE" backend/ --include=*.cs` sin salida.
