# M12 — frontera editorial revocable candidata

Base de composición: `83d6138f4ba9f37ec5089081ba44225e534af816`.
Alcance autorizado: exigir identidad y permisos autorizados, sin promover procedencia
observada ni habilitar entitlement, CAS o accesos positivos sin autoridad real.

## Cambios

- Contrato consumidor por artículo y operación, con principal resuelto por el
  productor y token de revocación. No es un emisor de identidad ni permisos:
  `backend/Sillar.Modules.Assistant/Authorization/IRevocableAssistantEditorialAuthority.cs:48`.
- Adaptador de las cinco operaciones del contrato editorial existente:
  `backend/Sillar.Modules.Assistant/Authorization/RevocableAssistantEditorialAdministration.cs:13`.
  Sin ambas autoridades devuelve denegación. Reevalúa CORE en cada llamada,
  rechaza `PendingDecision`, decisiones incoherentes y cambio de principal/nodo/ámbito.
  No convierte `InstallationKey` en `InstallationId` (:46).
- No conserva autorizaciones positivas entre operaciones. Exige token cancelable,
  suprime resultados revocados y cierra el callback al terminar la autorización;
  rechaza callbacks tardíos o duplicados (:67).
- Errores y cancelaciones internas deniegan, sin detalles ni reintentos (:98).

**No existe implementación productiva de la autoridad nueva ni registro automático
del adaptador en Host/DI.** Se debe componer explícitamente, sustituyendo el servicio
editorial sin dejar una ruta de escritura que eluda la frontera, cuando exista una
autoridad real aprobada. No se modificaron CORE, Host, endpoints, cuotas ni migraciones.

## Evidencia focal

Compilación conjunta CORE/M12 y `AutorizacionEditorialRevocableTests`: **28 PASS,
0 FAIL, 0 SKIP**. Autoridades sintéticas de prueba exclusivamente; los recorridos
positivos de esos dobles no acreditan identidad, entitlement ni permisos reales.

Casos: autoridades ausentes/averiadas, CORE denegado/pendiente/incoherente, ámbito
incorrecto, token no revocable, revocación antes/durante/después del callback, lectura
suspendida que ignora cancelación, revocación entre llamadas, ausencia de caché,
callbacks tardíos/duplicados, permiso específico por operación y cancelación sin
detalles. Fuente: `backend/Sillar.Modules.Assistant.CoreComposition.Tests/AutorizacionEditorialRevocableTests.cs:18`.

## Garantía y dependencias pendientes

La capa demuestra rechazo y supresión de resultados cuando observa revocación; no
certifica rollback ni impide por sí sola que un productor defectuoso confirme una
escritura. El contrato exige serialización entre autorización, ejecución y revocación
al productor (:41 del contrato); si no puede garantizarla, no debe invocar la operación.
No se promete revocación posterior al punto final de comprobación.

CORE/Host/Chat 2 deben aportar y validar el productor real: identidad M12 atestada y
vinculada a CORE, permiso contextual vigente, revocación transaccional y composición
sin bypass. Entitlement continúa reservado por `IAssistantAccessAuthority`; CAS y
sus reglas físicas permanecen bloqueados. No se reclasifica ningún AC.

Sin PostgreSQL, SQL, migraciones físicas, IA productiva, endpoints reales, push,
merge ni cambio de main. La rama M12 original se conserva intacta.
