# M07 — Recomposición sobre `main 9f098a4`

Creado: 05/10/2026, America/Lima · Última verificación: 05/10/2026, America/Lima
Base autoritativa: `main = 9f098a4e5e44701be8419867bdf0028703a1753c`
Candidata focal histórica, **congelada y no movida**:
`m07-b2b-sobre-main = 343ff7cffd26a50aef4979bc7ee61c51d5b52232`

**Qué es.** La recomposición de M07 sobre el `main` que ya trae **M05a Servicios**
y **la reparación de restaurabilidad de M03**. La candidata histórica queda como
antecedente del cierre focal anterior; **ese cierre no certifica este árbol**, así
que todo se repite aquí.

---

## 1 · Estrategia y base

```
main                     9f098a4e5e44701be8419867bdf0028703a1753c
histórica (congelada)    343ff7cffd26a50aef4979bc7ee61c51d5b52232
divergencia al empezar   main ahead 22 · histórica ahead 25
merge-base de la histórica   3564718…
```

**Merge normal, no rebase.** La rama nueva nace de `9f098a4` exacto y absorbe la
histórica por merge: conserva los 25 commits de M07 con sus evidencias, y la
rama histórica **no se mueve ni se fuerza**.

---

## 2 · Los seis conflictos, y cómo se resolvieron

Ninguno se resolvió descartando un lado.

| Archivo | Conflicto | Resolución |
|---|---|---|
| `backend/Sillar.Api/Sillar.Api.csproj` | `main` añadía el `ProjectReference` de Services donde M07 añadía el de B2B | **Los dos.** El host referencia Services y B2B |
| `frontend/package.json` | `main` añadía el script `test:services`, M07 `test:b2b`, en la misma línea final | **Los dos**, con la coma que faltaba. JSON validado |
| `e2e/setup/migrate.ts` | tres hunks: la cabecera, la lista de `applyMigrations` y el bucle de seeds | **Los siete módulos**, con B2B el último. Ver §3 |
| `e2e/setup/global-setup.ts` | el texto del log enumeraba una lista por cada lado | La lista real completa: `CORE, Catalog, Cms, CRM, Services, Sales, B2B` |
| `e2e/tests/zz-instalacion.spec.ts` | dos hunks: `main` retira Sales y comprueba su equivalencia; M07 retira B2B | **Los dos dependientes**, y las comprobaciones de equivalencia de Sales **íntegras**. Ver §4 |
| `scripts/verificar.mjs` | la lista de migraciones de la etapa 4 | **Los siete**, Services y B2B detrás de Sales |

`backend/Sillar.sln` **no entró en conflicto**: los proyectos de Services y de
B2B se añadieron en sitios distintos del archivo. Verificado:
`Project: 27 · EndProject: 27`.

**Compilación y tipos sobre el árbol combinado:** `dotnet build Sillar.sln` con
**0 errores**; `tsc --build` del frontend y `tsc --noEmit` del arnés, limpios.

---

## 3 · El arnés combinado · lo de `main` se preserva

```
migrate()   Core · Catalog · Cms · Crm · Services · Sales · B2B
seed()      core · catalog · cms · crm · services · sales · b2b
```

**No se añadió Services otra vez y no se reconstruyó el arreglo de Sales.** Los
dos venían de `main` y se conservan tal cual; esta recomposición aporta
**únicamente** la mitad de B2B.

**Por qué B2B va el último.** Sus cinco claves foráneas cruzadas apuntan a
`catalog` y a `crm` (dependencias duras declaradas en `B2BModule.cs:57`), así que
esas tablas tienen que existir antes. M03 va por el mismo motivo —dos claves
cruzadas, `SalesModule.cs:74`— y M05a solo depende de `core`
(`ServicesModule.cs:24`), así que su sitio es indiferente y se conserva donde
`main` lo puso.

**Activación.** `global-setup.ts` activa `b2b` **después de `catalog` y `crm`**,
sus dependencias duras. **Sales no se activa globalmente**: se conserva la
decisión ya integrada de M03, que reparó la *restaurabilidad* del arnés y no su
cobertura funcional.

---

## 4 · La costura más delicada · `zz-instalacion.spec.ts`

En este árbol **M01 tiene dos dependientes duros instalados**: `sales` (M03) y
`b2b` (M07). Los dos están en el binario, así que la instalación los crea, y los
dos declaran claves foráneas hacia `catalog`. La guarda C6 de
`catalog/99_drop.sql` se niega mientras cualquiera de los dos siga presente — y
negarse es el comportamiento correcto, no un fallo.

La cirugía queda en este orden:

```
1 · se mide el estado de Sales y se comprueba que se midió
2 · se suelta sales   (M03)   y se confirma que desapareció
3 · se suelta b2b     (M07)   y se confirma que desapareció
4 · se suelta catalog (M01)
5 · migrate() + seed() devuelve los tres
6 · Sales vuelve EQUIVALENTE, no «vuelve»
```

**El orden entre `sales` y `b2b` es libre y se dice por qué:** ninguno declara
claves foráneas hacia el otro. M03 declara sobre M07 una dependencia **blanda**,
y una blanda no lleva FK (`CLAUDE.md`, claves foráneas entre schemas). Lo que no
es libre es que los dos vayan antes de `catalog`.

**Las comprobaciones de equivalencia de Sales se conservan íntegras**, no
sustituidas por comprobaciones de existencia: schema, conjunto exacto de tablas,
historial de migraciones, claves cruzadas **por identidad y destino** y
dependencias duras registradas. Son de `main` y siguen siendo suyas.

Y la aserción final pasa a contar **los tres** schemas que esta prueba suelta
—`catalog`, `sales`, `b2b`—, no dos.

M07 mantiene además sus pruebas propias de instalación y desinstalación en
`zz-b2b-instalacion.spec.ts`, que son otra cosa: miden el ciclo físico de M07
contra sus cinco claves cruzadas.

---

## 4b · Hallazgo propio de M07 encontrado al recomponer · CORREGIDO

**M07 nunca aportó su proveedor de ejemplos de Swagger**, y el verde anterior lo
escondía.

`e2e/tests/zz-instalacion.spec.ts` comprueba que **ningún cuerpo de petición se
quede sin ejemplo** en el documento OpenAPI. El mecanismo es antiguo —cada módulo
implementa `ISchemaExamples` y `Sillar.Api` los descubre por reflexión
(`ModuleSchemaExamples.cs:65`)— y lo tienen CORE, M01, M02 y M04. **M07 no.**

Al correr el cierre focal sobre el árbol recompuesto salieron los nueve a la vez:

```
CambiarEstadoRequest · CrearCotizacionRequest · CrearPersonalizacionRequest
CrearVolumenRequest  · EditarLineasRequest    · LineaRequest
NotasRequest         · PagoRequest            · ReenlazarRequest
```

**Por qué no había salido antes, y conviene no pasarlo por alto.** En el cierre
focal del 05/10 sobre `343ff7c` esa misma prueba pasó — pero pasó **por
accidente**: una spec anterior de aquella corrida falló dejando `b2b`
desactivado, y un módulo inactivo no mapea sus endpoints, así que sus esquemas
no llegaban al documento. **El verde era el de un módulo apagado.** Con el árbol
entero en verde, el hueco aparece.

Corregido con `backend/Sillar.Modules.B2B/Documentation/B2bExamples.cs`: los
nueve cuerpos, con el criterio del contrato —«¿podría alguien que no conoce
SILLAR copiarlo y que le funcione?»—. No hace falta registrarlo en ningún sitio:
el filtro los descubre por reflexión.

Es trabajo **propio de M07** —sus DTO, su documentación, la regla 6 de
`CLAUDE.md`— y por eso se corrige aquí y no se escala.

---

## 4c · Resultados sobre el árbol recompuesto

**Cierre focal M07 + costura compartida**

```
15 passed (14.7m)       0 failed · 0 skipped
```

Las cuatro specs de M07 —**12 pruebas**: 5 de `b2b-cliente`, 5 de `b2b-panel`,
1 de `zz-b2b-ciclo` y 1 de `zz-b2b-instalacion`— más las tres de
`zz-instalacion.spec.ts`. **12 + 3 = 15.**
El antecedente sobre `343ff7c` fue 12 passed · 0 omitidas, y **no certifica este
árbol**. Evidencia: `evidencias/E2E-FOCAL-M07-9F098A4.txt`.

**Barrera `ModulosEnElDespliegueTests`, tres direcciones nuevas**

| | Resultado |
|---|---|
| **1 · legal** | **VERDE** |
| **2 · ilegal** · se retira **solo B2B** del host, Services y Sales dentro | **ROJO**: «Estos módulos no llegan al despliegue del host: Sillar.Modules.B2B.» Y el build daba **0 errores**, que es el problema |
| **3 · sabotaje del detector** | **ROJO** por la guarda `proyectos.Length >= 4`: «Solo 0 proyectos de módulo […]: el barrido está mirando donde no debe» |

Ningún sabotaje queda en HEAD. Evidencia:
`evidencias/BARRERA-DESPLIEGUE-9F098A4.txt`.

**Compilación y tipos**

```
dotnet build Sillar.sln     0 errores
tsc --build (frontend)      limpio
tsc --noEmit (arnés e2e)    limpio
```

---

## 4d · Puerta canónica ROJA sobre `60da5dc`, y qué se hizo con ella

**No certifica nada. Se registra porque el rojo tenía razón.**

```
candidata      60da5dc39d83ea7653ff93789aa86e9b2e1275c0
etapas 1–5     PASS
backend        704/704
etapa 6        172 passed · 7 failed · 0 skipped
rc             1
OMITIDAS_ESPERADAS = []
evidencia      rama qa/m07-60da5dc-rojo @ 9d01def038f27b36f2c74d2b7dbdeff9393f7a06
```

### La causa, y por qué no es ambiental

M07 declara `HardDependencies => ["core", "catalog", "crm"]`
(`B2BModule.cs:57`) y el arnés lo deja **activo** en el escenario normal. Desde
ese momento la plataforma **impide correctamente** desactivar M01 o M04: su
interruptor llega `disabled`.

Siete pruebas anteriores a M07 apagaban `catalog` o `crm` sin contemplar al
nuevo dependiente, y esperaban un interruptor que ahora está bloqueado.

| Spec | Qué apagaba |
|---|---|
| `aa-vacios.spec.ts` | `crm` |
| `catalogo.spec.ts` | `catalog` |
| `contenido.spec.ts` | `catalog` **y** `crm` |
| `m02-cierre-evidencia.spec.ts` · `[M02-C24]` | `catalog` |
| `tienda.spec.ts` | `catalog` |
| `zz-desmontaje.spec.ts` | `catalog` |
| `zz-z-m04-ciclo.spec.ts` · `[M04-CICLO]` | `crm`, y además lo **desinstala** |

**El rojo demuestra que el producto aplica bien el grafo.** Lo que hay que
adaptar son las pruebas, no el grafo.

### Lo que NO se hizo, y es la mitad de la decisión

No se quitó B2B de `global-setup.ts`; no se activó solo dentro de sus propias
specs; no se reordenó la suite para esconder el choque; no se aflojó ninguna
dependencia dura; no se forzó ningún interruptor que el producto deba bloquear.
**La C9 se preserva: B2B sigue activo en el escenario e2e normal.**

### Lo que sí se hizo

Un ayudante único, `e2e/fixtures/grafoDeModulos.ts`, con tres cosas y nada más:
consultar capacidades, mover el interruptor de un módulo por el panel, y
**suspender y restaurar M07** alrededor de un cuerpo.

```ts
await sinB2B(page, async () => {
  … el escenario original de M01/M04, que restaura lo suyo en su finally …
});                       // ← y aquí se reactiva M07
```

**El orden de restauración es el punto:** dependencias primero, M07 después, en
un `finally`, para que una prueba que falle a mitad no deje el grafo contaminado
para las siguientes. Y `sinB2B` **comprueba que M07 estaba activo antes de
suspenderlo**: si no lo estuviera, suspenderlo sería un no-op y el ayudante
pasaría a ser el sitio donde un fallo se esconde.

### El caso especial · `[M04-CICLO]`

Aquí **desactivar M07 no basta**, porque la prueba ejecuta `crm/99_drop.sql` y
la guarda C6 mira módulos **instalados**, no activos. En el árbol combinado hay
**dos** schemas dependientes duros de M04: `sales` y `b2b`.

```
estado inicial   CRM activo · B2B activo · Sales instalado y NO activado
                 (los tres se acreditan antes de tocar nada)

suspensión       desactivar B2B  →  desactivar CRM
retirada física  sales/99_drop.sql · b2b/99_drop.sql · comprobar que no están
                 →  crm/99_drop.sql  ×2 (se conserva la idempotencia)
reconstrucción   el reinstalado propio de M04, intacto
                 →  migrate() + seed()  ← infraestructura canónica, sin otra
                                           lista de migraciones escrita a mano
activación       CRM  →  B2B

estado final     capacidades iguales · CRM activo · B2B activo
                 Sales NO activado · schemas sales y b2b presentes
                 las CINCO claves foráneas de B2B restauradas
                 CORE, M01 y M02 intactos
```

**Reconstruir no es activar:** `migrate()` crea el schema de M03 y no toca su
activación, así que M03 sigue fuera de capacidades al cerrar el ciclo — y la
prueba lo afirma.

**La sonda de las cinco claves foráneas no se reinventó.** La que ya tenía
`zz-b2b-instalacion.spec.ts` se movió al ayudante —SQL idéntico— y ahora la usan
los dos sitios, con la lista esperada al lado de la consulta que la produce. Dos
mediciones parecidas que divergieran un día serían peor que ninguna.

### Lo que no se tocó

`HardDependencies` de M07 · la guarda C6 · los módulos del panel · las reglas de
activación · producto de M01, M04, Sales, Services, M08 ni M11. **Es una
adaptación del arnés al grafo combinado, no una corrección funcional.**

### Y una observación de B que no deja deuda

B contó 49 esquemas `*Request` en Swagger, **0 sin ejemplo**, y 9 de los 10 tipos
del ensamblado de M07 cuyo nombre acaba en `Request`. El décimo es
`InstitutionRequest` (`Domain/Entidades.cs`): es una **entidad persistente de
dominio**, no un cuerpo de petición HTTP. Los nueve DTO reales están en
`B2bExamples.cs`. **No hay deuda nueva y no se tocó Swagger.**

---

## 4e · Regresión focal tras adaptar las siete · 21 de 23 acreditadas

**Lo verificado, medido:**

| Paquete | Resultado |
|---|---|
| **6 de las 7 adaptadas** | **VERDES** — `aa-vacios`, `catalogo`, `contenido`, `[M02-C24]`, `tienda`, `zz-desmontaje` |
| **M07 · las cuatro specs** | **12/12 PASS** |
| **`zz-instalacion.spec.ts`** · costura compartida | **3/3 PASS**, con Sales + B2B + Catalog retirados y reconstruidos, y la equivalencia de Sales intacta |
| | **21 de las 23 esperadas** |

**Lo que no se pudo acreditar en esta máquina, y por qué:**

| | |
|---|---|
| `[M04-CICLO]` | **5 intentos, siempre rojo**, y nunca en la aserción: muere al navegar a `/admin/contenido/banners` con «React no montó nada». La consola de la traza dice `net::ERR_INSUFFICIENT_RESOURCES` y 500 del proxy de Vite. Las navegaciones **inmediatamente anteriores sí montan** —`/admin`, `/producto/…`—, así que no es la aplicación: es el fetch del *chunk* diferido de esa ruta fallando por recursos |
| `[M05A-CICLO]` | Rojo también, y **es un archivo que esta rama no toca**. En una corrida su fallo fue de arrastre —`[M04-CICLO]` corrió antes y dejó `crm` apagado—; en otra fue el mismo `nada-montado`; el intento aislado murió sin producir resultado |

**Por qué esto apunta a la máquina y no al cambio.** `[M05A-CICLO]` lo certificó
el frente B **dentro de la etapa 6 de la puerta canónica** sobre `a0ef786`, y
aquí falla sin que esta rama lo haya tocado. Y `[M04-CICLO]` falla con
`ERR_INSUFFICIENT_RESOURCES`, que es Chromium sin recursos, no una aserción del
producto.

**Lo que sí cambió en el peso de `[M04-CICLO]`, y hay que decirlo:** pasó de dos
reinicios del proceso a **cuatro** —suspender y devolver M07 se suma a apagar y
encender M04— y además ejecuta `migrate()` de los siete módulos dentro del test.
Es, con diferencia, la prueba más cara de la suite. **Que sea más caro no la hace
incorrecta**, pero sí la convierte en la primera que cae cuando la máquina va
justa.

**Dos arreglos reales que salieron de estas corridas**, los dos porque el
ayudante comprobaba lo que no debía:

1. Afirmaba el estado del módulo contra la **tarjeta del panel** recién
   repintada tras el reinicio, y fallaba con «element(s) not found». Ahora
   pregunta a `/api/capabilities`, que es quien lo sabe.
2. Daba por vuelto el sistema cuando respondía **la API**, sin esperar al
   **servidor web** contra el que navegan las pruebas. El `cambiarModulo` local
   de `zz-z-m04-ciclo.spec.ts` no esperaba nada en absoluto; ahora delega en el
   ayudante.

> **Esto refuerza la precondición del §5, no la sustituye:** antes de la puerta
> canónica de M07 hay que liberar recursos. Dos pruebas de ciclo cayendo por
> `ERR_INSUFFICIENT_RESOURCES` en una máquina con 7 GiB disponibles dice que el
> margen no está en la RAM total, sino en lo que queda cuando Chromium, Vite,
> siete `dotnet ef` y PostgreSQL coinciden.

---

## 5 · Condición operativa de disco para la próxima puerta canónica

**Se registra, no se cambia nada.** Ni el umbral ni el runner se tocan en M07.

La puerta canónica de M03 tuvo durante la etapa 6 dos muestras consecutivas de
disco libre:

```
2861 MiB
2865 MiB
```

La red de seguridad del vigía actúa con **tres** muestras consecutivas por debajo
de **3072 MiB**, así que aquella corrida **no fue interrumpida y siguió siendo
válida**. Pasó por dos.

> **Disparador para M07:** antes de lanzar la puerta canónica de esta candidata
> **hay que liberar espacio en disco y superar normalmente el preflight
> existente.** Dos muestras bajo el umbral son un aviso, no una anécdota: la
> tercera habría abortado la corrida a mitad de la etapa más cara.

Medición al escribir este documento: **11 268 MiB libres (91 % de uso)**. Por
encima del umbral, pero el disparador queda escrito porque la etapa 6 consume
mientras corre y el margen de M03 se midió *durante* la etapa, no antes.

---

## 6 · Lo que esta recomposición NO tocó

Ni se aprovechó para corregir nada de esto:

| | |
|---|---|
| Numeración multinodo · deuda M16 | sigue registrada donde estaba |
| C1 / criterio 4.4 | sigue en **HOLD POR CAPACIDAD AÚN INEXISTENTE** |
| Producto ajeno a M07 | sin tocar |
| M08 · M11 | sin tocar |
| `main` · la rama histórica `343ff7c` | sin mover |
| ROADMAP de M07 | **no se cierra**: M07 no estará cerrado hasta pasar QA canónica e integración |

El hallazgo de Sales que nació en el cierre focal anterior **ya no es deuda
vigente**: lo resolvió `main` con la reparación de M03. Su estado está en
`CIERRE-E2E-M07.md` §6.
