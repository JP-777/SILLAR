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

**Sobre el árbol recompuesto (`main 9f098a4` + M07), que es el que cuenta:**

```
npx playwright test tests/b2b-cliente tests/b2b-panel tests/zz-b2b-ciclo \
                    tests/zz-b2b-instalacion tests/zz-instalacion

  15 passed (14.7m)       0 failed · 0 skipped
```

Son las **cuatro specs de M07** (12 pruebas: 5 de cliente, 5 de panel, 1 de
ciclo y 1 de instalación) más las **tres de
`zz-instalacion.spec.ts`**, la costura compartida: con M03 y M07 conviviendo,
M01 tiene dos dependientes duros instalados y la restaurabilidad tiene que
seguir en pie. Detalle en `evidencias/E2E-FOCAL-M07-9F098A4.txt`.

**Antecedente, que no certifica este árbol:** 12 passed · 0 omitidas sobre
`343ff7c`, solo con las cuatro specs de M07.

```
(histórico, sobre 343ff7c)
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

## 2c · Auditoría de paridad · medida sobre el árbol recompuesto

**Vuelta a medir, no heredada.** `main` cambió dos veces desde el cierre focal
anterior —entró M05a y entró la reparación de M03—, así que la lista se mide
contra el árbol que hay.

**Medido arrancando el host y pidiéndole la instalación**, no leyendo las listas:

```
$ POST /api/setup                                              setup=201
  schemas creados    b2b catalog cms core crm sales services
  el host dice       Módulos descubiertos: 7
                     (b2b, catalog, cms, core, crm, sales, services)
```

| Módulo | Binario / `/api/setup` | `migrate()` | `seed()` | Diferencia |
|---|:--:|:--:|:--:|---|
| `Sillar.Core` | sí | sí | sí (`core`) | **ninguna** |
| `Sillar.Modules.Catalog` | sí | sí | sí | **ninguna** |
| `Sillar.Modules.Cms` | sí | sí | sí | **ninguna** |
| `Sillar.Modules.Crm` | sí | sí | sí | **ninguna** |
| `Sillar.Modules.Services` | sí | sí | sí | **ninguna** — de `main` con M05a, **no reescrito** |
| `Sillar.Modules.Sales` | sí | sí | sí | **ninguna** — de `main` con la reparación de M03, **no reconstruido** |
| `Sillar.Modules.B2B` | sí | sí | sí | **ninguna** — es lo que aporta esta recomposición |
| `Sillar.Modules.Demo` | sí, **solo en Debug** | no | no | **sin migraciones ni seed aplicables**: 0 archivos de migración y no existe `database/modules/demo/` |

Migraciones reales por proyecto, contadas excluyendo los `*.Designer.cs` y el
`*DbContextModelSnapshot.cs` —que no es una migración—:

```
Core 4 · Catalog 1 · Cms 1 · Crm 2 · Sales 1 · Services 1 · B2B 1 · Demo 0
```

**No queda ningún módulo atrasado.** Y es la **regla de paridad** de
`docs/modules/sales/E2E-RESTAURABILIDAD-M03.md` §3 cumpliéndose en el caso que
ella misma nombraba: M07 entra a la candidata con su mitad del arnés puesta, no
después.

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

## 6 · El hallazgo de Sales — RESUELTO PREVIAMENTE EN MAIN POR M03

**Ya no es deuda vigente.** Se conserva el apartado porque el hallazgo nació
aquí y la trazabilidad importa, pero su estado cambió: **lo cerró `main`**, no
esta candidata.

### Qué se encontró, y cuándo

Durante el cierre focal de M07 del 04–05/10/2026, al añadir B2B al arnés, se
midió que **`e2e/setup/migrate.ts` no migraba `Sillar.Modules.Sales`** mientras
`POST /api/setup` sí lo instalaba, porque M03 está en el binario. Los dos caminos
de instalación no convergían, y la consecuencia era concreta:
`e2e/tests/zz-instalacion.spec.ts` quedaba en rojo porque la guarda C6 de M01 se
negaba con `sales` presente, y soltar `sales` no valía porque el arnés no sabía
devolverlo.

**Chat 2 lo separó de M07** de forma expresa para no mezclar un arreglo de M03
con esta candidata, y abrió el encargo aparte.

### Cómo quedó resuelto

En la rama `fix/m03-e2e-restauracion`, **certificada e integrada en `main`**:

| | |
|---|---|
| Arreglo | `Sillar.Modules.Sales` en `migrate()` y `sales` en el bucle de `seed()` |
| Equivalencia | medida en tres estados: lo que deja `/api/setup`, lo que dejaba `migrate()+seed()` sin Sales, y lo que deja con él. Los dos caminos convergen en schema, tablas, historial, claves cruzadas por identidad y destino, y dependencias duras registradas |
| Tres direcciones | legal verde · ilegal (Sales fuera del arnés) rojo · sabotaje del detector rojo en la preparación |
| Documentación | `docs/modules/sales/E2E-RESTAURABILIDAD-M03.md`, con la **regla de paridad** que esto dejó escrita |

**Y la regla que salió de ahí es la que ahora obliga a M07:**

> La auditoría de paridad entre los módulos del binario / `/api/setup` y los que
> enumeran `migrate()` + `seed()` debe repetirse cada vez que entre a `main` un
> módulo real nuevo.

M07 es el caso siguiente que esa regla nombraba por su nombre, y esta
recomposición es su cumplimiento: ver §2c, la auditoría medida sobre el árbol
recompuesto.

### Lo que esta recomposición hizo con ello

`zz-instalacion.spec.ts` ya no retira solo B2B: **retira los dos dependientes
duros de M01** —`sales` de M03 y `b2b` de M07— antes de soltar `catalog`, y
**conserva íntegras las comprobaciones de equivalencia de Sales** que `main`
trajo. No se sustituyeron por comprobaciones de existencia, y no se reconstruyó
el arreglo de M03: se preservó.

---

## 6a · Puerta canónica roja sobre `60da5dc` · el grafo funcionando

**No certifica nada**, y se registra porque el rojo tenía razón.

```
etapas 1–5   PASS          backend   704/704
etapa 6      172 passed · 7 failed · 0 skipped        rc=1
evidencia    qa/m07-60da5dc-rojo @ 9d01def038f27b36f2c74d2b7dbdeff9393f7a06
```

Con M07 activo —como lo deja la C9— la plataforma **impide correctamente**
desactivar M01 y M04, porque M07 depende duro de los dos. Siete pruebas
anteriores a M07 los apagaban sin contemplarlo.

**Se adaptaron las pruebas, no el grafo.** B2B sigue activo en el escenario e2e
normal; no se aflojó ninguna dependencia dura ni se forzó ningún interruptor que
el producto deba bloquear. El detalle de la decisión, el ayudante `sinB2B` y la
secuencia especial de `[M04-CICLO]` —que además retira físicamente `sales` y
`b2b` antes de poder soltar `crm`— están en
`RECOMPOSICION-M07-05-10-2026-9f098a4.md` §4d.

---

## 6b · Precondición de disco antes de la puerta canónica de M07

**Se registra; no se cambia el umbral ni el runner.**

La puerta canónica de M03 midió durante la etapa 6 dos muestras consecutivas de
disco libre: **2861 MiB** y **2865 MiB**. La red de seguridad del vigía actúa con
**tres** muestras consecutivas por debajo de **3072 MiB**, así que aquella
corrida no fue interrumpida y **siguió siendo válida**. Se quedó a una muestra.

> **Disparador para M07:** antes de lanzar la puerta canónica de esta candidata
> hay que **liberar espacio en disco y superar normalmente el preflight
> existente**.

El detalle y la medición del día están en
`RECOMPOSICION-M07-05-10-2026-9f098a4.md` §5.

---

## 7 · La barrera del despliegue, tres direcciones

**La acreditación vigente es la del árbol recompuesto** —`main 9f098a4` + M07—,
y vive en un solo archivo:

> `evidencias/BARRERA-DESPLIEGUE-9F098A4.txt`

| | Qué se hace | Resultado |
|---|---|---|
| **1 · legal** | El árbol tal como queda en HEAD, con Services, Sales y B2B en el host | **VERDE** |
| **2 · ilegal** | Se retira **solo B2B** del host —**Services y Sales se conservan**, comprobado antes de medir— | **ROJO**, y nombra `Sillar.Modules.B2B` y solo a él. El build seguía dando **0 errores**, que es justo el problema |
| **3 · sabotaje** | `ProyectosDeModulo` devuelve colección vacía | **ROJO** por la guarda `proyectos.Length >= 4` |

Que la dirección 2 conserve Services y Sales es lo que hace el rojo atribuible:
con los tres fuera, el mensaje no diría de quién es.

Y la tercera es la que más importa: con la lista vacía, «ninguno falta» es
trivialmente cierto, y esa guarda es lo único que impide el verde.

Ningún sabotaje queda en HEAD: `git diff --stat` de los dos archivos tocados sin
salida, `grep -rn "SABOTAJE" backend/ --include=*.cs` sin salida, y la barrera de
vuelta en verde sobre el árbol restaurado.

### Antecedente histórico · no acredita este árbol

Los tres archivos de la candidata congelada `343ff7c` se conservan como
antecedente del cierre focal anterior y **no certifican la recomposición**:

| | |
|---|---|
| `evidencias/BARRERA-DESPLIEGUE-1-LEGAL.txt` | antecedente, sobre `343ff7c` |
| `evidencias/BARRERA-DESPLIEGUE-2-ILEGAL.txt` | antecedente, sobre `343ff7c` |
| `evidencias/BARRERA-DESPLIEGUE-3-SABOTAJE.txt` | antecedente, sobre `343ff7c` |

Aquella medición se hizo sobre un árbol **sin Services y sin la reparación de
M03**, así que su dirección ilegal no podía demostrar lo que demuestra la de
ahora: que el rojo es de B2B **teniendo los otros dos dentro**.
