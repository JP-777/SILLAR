# C6 — Desinstalar una dependencia dura de M07: auditoría por las dos vías

Creado: 27/09/2026, America/Lima · Última modificación: 27/09/2026 · Última verificación:
27/09/2026 · Commit base verificado: `e839989432283c755edf7d4ae47b2c37697215ec` (`main`), rama
`m07-b2b-sobre-main`.

**Todo lo de este documento se ejecutó en bases efímeras propias** (`sillar_c6_*`,
`sillar_b2b_prueba_*`) creadas y destruidas dentro del PostgreSQL de la worktree `sillar-b2b`
(identidad sin choques, offset 62). Ninguna instalación real se tocó.

---

## 1 · Las tres operaciones, y quién guarda cada una

| Operación | Cómo se hace hoy | Qué la guarda | Mira… |
|---|---|---|---|
| **Instalar** | `POST /api/setup` → `InstaladorDeModulos` aplica las migraciones de todos los módulos declarados. También `dotnet ef database update` por módulo (e2e, puerta) | El grafo de módulos declarados: sin M01 o M04 el despliegue es inválido y se lanza antes de migrar (`backend/Sillar.Core/Setup/InstaladorDeModulos.cs:53-59`) | Lo **declarado** en el binario |
| **Activar / desactivar** | Interruptor del panel → `ModuleActivationService.SetActiveAsync` | `ModuleGraph.MissingHardDependencies` / `ActiveHardDependents` (`backend/Sillar.Core/Services/ModuleActivationService.cs:276`, `:286`) | Lo **activo** |
| **Desinstalar** | **Solo SQL a mano**: `database/modules/<código>/99_drop.sql` (`database/README.md`). No existe desinstalación desde el panel ni desde código | Hasta hoy, **nada**: el de M01 solo emitía un `NOTICE` y borraba | — |

**Consecuencia que no se ve a primera vista:** la guarda del panel mira lo **activo**, no lo
**instalado**. Con M07 desactivado, el panel deja desactivar M01. Eso es correcto —desactivar no
borra nada— y queda afirmado por
`Con_M07_inactivo_el_panel_deja_desactivar_M01_porque_desactivar_no_borra`. Pero significa que
**ninguna guarda del panel protege la desinstalación**: la única vía de borrado es el SQL, y ahí
tiene que estar la guarda.

## 2 · Lo que hacía el SQL antes de esta rama — REPRODUCIDO

`evidencias/C6-SQL-HOY-20260927.txt` (guion `evidencias/c6-sql-hoy.sh.txt`): `main` sin tocar, un
schema `b2b` mínimo con las FK duras que declara M07.

- `catalog/99_drop.sql` termina con **rc=0** y se lleva `fk_quote_lines_item` y
  `fk_special_order_leads_product` («drop cascades to constraint … on table b2b…»). Su aviso de
  módulos afectados **no nombra `b2b`** (su lista fija: `sales`, `inventory`, `pos`, `purchasing`).
- Después, `b2b.quote_lines` **acepta un `item_id` que no existe** en ningún catálogo.
- **Reinstalar M01 no devuelve las FK**: la migración de M07 ya consta aplicada.
- `crm/99_drop.sql` hace lo mismo con `fk_special_order_leads_customer`.
- Un `DROP SCHEMA catalog CASCADE` arbitrario, igual.

## 3 · Lo que se cambió

**Guarda de desinstalación** en `database/modules/catalog/99_drop.sql`,
`database/modules/crm/99_drop.sql` y el nuevo `database/modules/b2b/99_drop.sql`. Rechaza con
`SQLSTATE 2BP01 (dependent_objects_still_exist)` y un mensaje que dice qué módulo depende y qué hacer.

- **Dos señales, ninguna con nombres de módulo escritos**: (1) FK de otro schema hacia el que se
  va a borrar —lo que CASCADE destruiría—; (2) módulos registrados en `core.module_dependencies`
  con dependencia `hard` cuyo schema sigue existiendo, aunque ya no tengan FK. Así el script de M01
  **no conoce a M07** y protege igual a M03 o a cualquier dependiente futuro.
- **La comprobación y el `DROP` están en el mismo bloque `DO`.** psql sin `ON_ERROR_STOP` sigue
  con la sentencia siguiente tras un error; con el `DROP` fuera del bloque, la guarda solo pararía a
  quien lanzara el script bien.

**Guarda de instalación** en la migración de M07
(`backend/Sillar.Modules.B2B/Migrations/20260927180018_B2bInitial.cs`): sin `catalog.products` /
`catalog.product_items` o sin `crm.customers`, aborta con `SQLSTATE 42P01` y «M07 Solicitudes B2B
no se puede instalar porque falta: …».

## 4 · Lo que hace el SQL ahora — REPRODUCIDO

`evidencias/C6-SQL-DESPUES-20260927.txt` (guion `evidencias/c6-sql-despues.sh.txt`): migración real
de M07 sobre CORE + M01 + M04.

| Paso | Resultado |
|---|---|
| `catalog/99_drop.sql` con `ON_ERROR_STOP=1` | **Rechazado**, rc=3. Los cuatro schemas y las cinco FK intactos |
| `catalog/99_drop.sql` **sin** `ON_ERROR_STOP` | **Rechazado**, rc=0 (psql sigue), y **tampoco borra nada** |
| `crm/99_drop.sql`, con y sin `ON_ERROR_STOP` | Igual: rechazado, nada borrado |
| `b2b/99_drop.sql`, después `catalog`, después `crm` | Los tres rc=0; queda `core` |

## 5 · Límites — dichos sin adorno

1. **Un `DROP SCHEMA catalog CASCADE` escrito a mano elude cualquier guarda.** Reproducido en el
   mismo registro: se lleva `fk_quote_lines_item` y `fk_special_order_leads_product`, y reinstalar
   M01 no las devuelve. **Ninguna guarda del panel ni de los scripts lo protege, y no se afirma lo
   contrario.** Protegerlo exigiría un disparador de eventos DDL en la base (`CREATE EVENT
   TRIGGER … ON sql_drop`), que es de CORE, necesita superusuario y cambia la política de
   desmontaje de toda la plataforma: queda como pregunta (`ESCALADAS-M07.md`, E10).
2. **Tras un daño así, la vía de reparación es desinstalar y reinstalar M07**, que devuelve las FK
   (`Desinstalar_y_reinstalar_M07_…_y_devuelve_sus_FK`) pero **pierde los datos de M07**. No hay
   reparación que conserve datos, y no se inventa aquí.
3. **Una instalación de M07 rechazada deja un schema `b2b` vacío** con `__migrations` sin filas:
   EF crea el schema y el historial antes de abrir la transacción de la migración, y la guarda vive
   dentro. Sin tablas de M07 ni migración registrada, y reanudable. Quitarlo exige tocar
   `Sillar.Shared.Data` (costura). Efecto conservador: la señal 2 puede pedir ejecutar
   `b2b/99_drop.sql` antes de desinstalar M01.
4. **La señal 2 supone schema = código de módulo.** Se cumple en todos los módulos de hoy
   (`IModule.Schema => Code`, `backend/Sillar.Shared/Modularity/IModule.cs:52`); un módulo que lo
   cambiara quedaría cubierto solo por la señal 1.

## 6 · Pruebas y falsificación

`backend/Sillar.Modules.B2B.Tests/` — **19 pruebas, 19 verdes** (`dotnet test
Sillar.Modules.B2B.Tests`), sin tocar la base de la puerta: cada una crea y destruye su base.

| Dirección | Pruebas |
|---|---|
| Panel e instalador (puro) | 8 — `DependenciasDurasTests` |
| Instalar M07 sin M01 / sin M04 | Falla con 42P01 y el mensaje exacto; no deja tablas; reanudable |
| Desinstalar M01 / M04 con M07 | Rechazado por FK, por registro, y **lanzado como psql sin detenerse en errores** |
| Retorno | M07 primero y después M01 y M04; desinstalar y reinstalar M07 conserva CORE/M01/M04 y devuelve las FK; scripts idempotentes |
| Datos | `ck_quotes_origen` con `INSERT` real |

**Sabotaje** — `evidencias/C6-SABOTAJE-20260927.txt`. Cada guarda rota a propósito, en la copia de
trabajo, y restaurada:

| | Rotura | Resultado |
|---|---|---|
| S1 | Guarda de `catalog/99_drop.sql` apagada | Rojo |
| S2 | Guarda de `crm/99_drop.sql` sin la señal de FK | Rojo |
| S3 | `DROP` sacado del bloque de la guarda | **Verde en la primera tanda** → se añadió `ComoPsql` y la prueba de psql sin `ON_ERROR_STOP` → rojo |
| S4 | Guarda de instalación apagada | **Verde en la primera tanda** → destapó un defecto real (abajo) → corregido → rojo |
| S5 | M07 sin la FK hacia `product_items` | Rojo |
| S6 | M07 declara M04 como blanda | Rojo |

Restaurado todo: **19/19**.

**El defecto que destapó S4.** La guarda de instalación hacía `faltan := faltan || 'M01 …'`, que
PostgreSQL lee como un literal de array y falla con `22P02 malformed array literal: "M01 Catálogo
(schema catalog)"`. **La guarda nunca llegaba a lanzar su mensaje**, y las pruebas pasaban porque
ese error contenía la subcadena que buscaban. Corregido con `array_append`, y las pruebas exigen
ahora el SQLSTATE y el mensaje completo. Es exactamente el caso de
`ANTES-DE-EMPEZAR-UN-MODULO.md` §2: una barrera sin provocar protegía solo su versión sana.

## 7 · Hashes de los originales (en `/var/tmp/sillar-m07-c6/`)

| Original | Publicado | SHA-256 |
|---|---|---|
| `C6-SQL-HOY.log` | `evidencias/C6-SQL-HOY-20260927.txt` | `cc1b660c309ecc2d9ce29bffce4560f51741fbda4924b5cbc547c0a63ce399c1` |
| `C6-SQL-DESPUES.log` | `evidencias/C6-SQL-DESPUES-20260927.txt` | `9cbf0f20a5abfe8917667a4726953189841b11df221d12ca05c29c3d11679e77` |
| `SABOTAJE.log` | `evidencias/C6-SABOTAJE-20260927.txt` | `aa8ddab3e609deceb5c8f1234afa362b041f9ee3cc83c984910008e4ad110633` |
| `c6-sql-hoy.sh` | `evidencias/c6-sql-hoy.sh.txt` | `5d6d4a7c281958654b1995d215a2f49881ad26390a95d9dcb64a2e31355d4329` |
| `c6-sql-despues.sh` | `evidencias/c6-sql-despues.sh.txt` | `84545ceee323d769628ee61096ea51141a85d4a4a25955baf95b2072c07b4fda` |

Copias byte a byte. Sin secretos: los guiones leen la cadena del `.env` en ejecución y no la
imprimen.
