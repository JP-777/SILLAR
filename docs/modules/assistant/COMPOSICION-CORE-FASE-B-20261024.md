# M12/CORE — composición aislada candidata

Base: `58bb93a6f3021490a14c9144afd31fc7c02458a0`.
Parche CORE recibido: SHA-256 `ccf892990ed29e8e371b1862ff71a918f326c8e3388eefa4917b8adbb6ef0d8d`.
Aplicación sin conflictos mediante `git am`:

| Commit de origen | Commit recreado en esta composición |
|---|---|
| `a47cead47860e9c44ab325cbfbb06b4ec848d9b2` | `b62f36b05851fc2033c81de67d5ca226318129ee` |
| `f7ae2ade35e4a9f7c39f14de6b3a388c1ae813fc` | `3da450c39904cf14d4f53f17c4abe8fd02763b8c` |

Después se trasladaron los 30 commits M12 desde la misma base hasta
`5bdb1b9feeb117641e95003daaf740333a4d7844`, sin merge. El resultado compuesto anterior
a estas pruebas es `b1f4c5f818393e9507b2d5bbf29bd294dd0d74cb`.
`git diff --exit-code` confirmó que módulo, pruebas M12 previas y matriz coinciden
con el candidato original. La rama M12 original no se modificó.

**Cambios nuevos: solo pruebas de composición; ninguna integración productiva.**
El proyecto `backend/Sillar.Modules.Assistant.CoreComposition.Tests` referencia CORE
y M12 exclusivamente para pruebas; no cambia las dependencias de sus módulos
(`Sillar.Modules.Assistant.CoreComposition.Tests.csproj:10`).

Compilación conjunta y prueba focal: **4 PASS / 0 FAIL / 0 SKIP**.
Los registros candidatos reales se ejercitan sin conectar una base: el interceptor
aborta antes de abrirla (`ComposicionCoreProcedenciaTests.cs:117`). Se demuestran:

- Fallo de lectura de procedencia devuelve `AuthorityUnavailable` (:42).
- Fallo de lectura de autoridad devuelve `Denied`, sin identidad (:57).
- Un doble con procedencia observada no satisface las autoridades M12 ausentes (:73).
- Cancelación previa se propaga sin intento de conexión (:91).

No se reclasifica ningún criterio de aceptación ni se interpreta observación como
atestación: `backend/Sillar.Core.Contracts/IInstallationProvenance.cs:15` lo declara
observado, no firmado; `backend/Sillar.Core.Contracts/IAssistantAccessAuthority.cs:15`
mantiene `PendingDecision` ante hechos favorables.

**Bloqueos para una integración efectiva:** vinculación autorizada de
`InstallationKey` con identidad M12, prueba de atestación requerida por Host,
entitlement efectivo, permisos administrativos y revocación de CAS. Los contratos
aportados no sustituyen esas garantías. No se creó un adaptador que siempre deniegue
para aparentar una integración ni uno que conceda acceso por inferencia.

Sin migraciones físicas CORE/M12, SQL ejecutado, IA productiva, endpoints reales,
registro en Host, push, merge ni cambios de main. Al no existir todavía una
integración efectiva segura con estos contratos, se detiene aquí.
