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
