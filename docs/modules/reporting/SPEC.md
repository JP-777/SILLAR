# M10 — Reportes de SILLAR WEB

**Fecha de creación:** 26 de noviembre de 2026, America/Lima.
**Fecha de última verificación:** 26 de noviembre de 2026, America/Lima.
**Commit contrastado:** `5799bd7`.
**Estado:** contrato de cierre; implementación y acreditación en curso.

## 1. Autoridad y alcance

JP autorizó redactar este SPEC desde la candidata integrada. Es una excepción
expresa del encargo del 26/11, no un precedente para otros módulos. Desde esta
versión, una discrepancia se resuelve a favor del SPEC y se registra en BITACORA.
Las decisiones siguientes completan la candidata sin cambiar precios ni contratar
proveedores. M11 sigue aplazado; no se construye ni se mide un proveedor de pago.

M10 ofrece al personal autorizado indicadores agregados de pedidos y pagos
registrados, períodos explícitos, comparación descriptiva de cohortes y exportación
CSV de los indicadores no monetarios de pedidos. No es contabilidad, rentabilidad,
inventario ni una afirmación de ingreso bancario. No expone información personal.

Quedan fuera de WEB v1: ERP, exportación de expedientes personales, segmentación
por persona, gráficos de historia no registrada, predicciones, conversión de
monedas y proveedores de M11. La exportación monetaria no forma parte de la
candidata ni de este contrato; las cifras monetarias se consultan en pantalla.

## 2. Dependencias y comportamiento modular

Código `reporting`, dependencia dura CORE, dependencia blanda M03 `sales`.
Solo importa contratos públicos de M03. No lee su DbContext, tablas ni entidades
y no usa el bus de eventos como fuente de hechos obligatorios.

Licencia y activación son puertas separadas, en ese orden. Sin licencia no hay
interruptor. Licenciado y apagado: sin ruta de panel, navegación ni API M10.
Encendido sin M03: pantalla operativa con fuente no disponible, sin cifras
inventadas. Si M03 está presente pero falla, la respuesta es error de lectura,
no ausencia de módulo ni cero. Apagar M10 no afecta pedidos, clientes ni pagos.

## 3. Indicadores y denominadores

| Indicador | Ventana y significado | Reglas |
|---|---|---|
| I01 | Pedidos creados en `[inicio, fin)` | Recuento de la cohorte, incluidas bajas lógicas |
| I02 | Valor congelado de esa cohorte | Separado por moneda y actividad; no equivale a cobros |
| I03 | Estado comercial actual de esos pedidos | Los siete estados de M03; no reconstruye el estado al final del período |
| I04 | Pedidos de esa cohorte actualmente vencidos | Subconjunto coherente con I03; no cancela ni reserva mercancía |
| I05 | Pagos registrados dentro de `[inicio, fin)` | Fecha de registro, no de creación del pedido ni fecha inferida del banco |
| I06 | Importes registrados en la misma ventana | Separados por origen, medio y moneda; nunca sumas entre monedas |
| I07 | Pagos registrados con tardanza congelada verdadera | No recalcular tardanza desde la hora de consulta |

Activos e inactivos son dimensiones distintas del estado comercial. Un pedido
cancelado no es automáticamente una baja lógica. I03 debe sumar I01 e I04 debe
coincidir con el grupo `expired`. El servidor y la interfaz rechazan respuestas
incoherentes, incluso si son JSON válido.

Una moneda ausente se cuenta aparte; jamás se rellena con PEN por estar en Perú.
Un pago histórico sin origen, moneda o tardanza suficientes conserva esa
incertidumbre. La cobertura monetaria la acredita el escritor de M03 desde una
fecha durable. Una ventana anterior a esa fecha no se publica como medida.
Con M11 aplazado, la cobertura exigible es la del escritor manual de M03.

## 4. Estados observables

`source_unavailable` significa que el contrato no está disponible. `not_measurable`
significa que no existe evidencia suficiente para ese indicador. `empty` exige
una consulta ejecutada y válida con resultado vacío. `with_data` exige datos
validados. `currency_not_accredited` conserva la causa monetaria sin ocultarla.
Un fallo técnico devuelve 503 y texto de error; no sustituye métricas por ceros.

Toda respuesta medida identifica el período y el instante de observación UTC.
Los límites recibidos deben coincidir con los pedidos. El usuario ve la causa de
ausencia y puede volver a consultar. Cero medido se muestra como cero; una cifra
que no se pudo medir se muestra sin número, con explicación.

## 5. Tiempo y comparación

La API recibe ISO 8601 con desplazamiento explícito y exige `fin > inicio`.
La selección de días usa la zona IANA acreditada de la instalación, inicialmente
America/Lima, o UTC elegido expresamente. Nunca la zona del navegador. La
conversión rechaza fechas imposibles, horas inexistentes y horas ambiguas.

CORE conserva y verifica la zona de la instalación y su evidencia de reglas
horarias. La configuración por sí sola no acredita la zona. Una evidencia
caducada, revocada, incompatible con la configuración o con el runtime impide
publicar un período o exportar hasta volver a acreditarla. Instalar no supone
una decisión comercial nueva: aplica la zona autorizada y verifica su resolución.

Comparar A y B describe dos cohortes observadas en sus instantes respectivos.
No prueba evolución temporal ni una instantánea común. Se muestran solapamiento,
adyacencia y límites. Las diferencias solo se calculan con fuentes y métricas
compatibles y medidas; los estados no medibles se propagan. Los enlaces de
comparación solo transportan parámetros de reportes validados, nunca identidad.

## 6. Persistencia y atribución

M10 no copia pedidos, pagos ni clientes. El schema `reporting` contiene únicamente
su historial local `__migrations`, no replicado. El instalador aplica su migración;
el panel solo comprueba el esquema antes de activar. Seed sin datos de negocio.
La desinstalación transaccional elimina solo ese historial y el schema, con
RESTRICT. Un objeto desconocido impide la baja sin borrado parcial.

CORE es dueño de la evidencia local de zona y de las autorizaciones y conserva
la auditoría de exportación. Cada fila que nazca se clasifica en su migración:
local sin replicación, o replicada con UUID v7, nodo y versión. No se crea FK de
una tabla replicada hacia una local. La autoría humana congela nombre, id local
y nodo; nunca referencia por FK al administrador ni se resuelve su nombre al leer.
Una acción de sistema conserva los tres nulos. Un evento de bus no sustituye un
registro durable ni una lectura autoritativa.

## 7. Permisos y exportación

Operaciones declaradas: `read` y `export`, almacenadas y evaluadas por CORE,
localmente. Rol mínimo inicial admin; las revocaciones por operación prevalecen.
La guarda vive en la operación además de proteger HTTP. Sesión de cliente o
editor no abre Reportes. Las escrituras HTTP exigen CSRF.

El CSV de I01/I03/I04 se genera en el servidor desde una única lectura validada:
autorizar, resolver zona, leer, preparar bytes y contar sus filas, revalidar zona,
registrar en CORE, revalidar y entregar. No acepta contenido ni cantidad de
filas declaradas por el navegador como evidencia. El endpoint candidato de
auditoría separada no sustituye esta operación y no se monta como descarga.

Sin registro durable válido no sale archivo. Se audita una exportación preparada
y autorizada, no se afirma que la persona descargó o abrió el archivo. Un error
de transporte posterior puede dejar auditoría sin descarga. La auditoría no
almacena el contenido ni datos personales de clientes. Se conserva id de
exportación UUID v7, módulo, tipo, formato, filas y actor congelado.

CSV UTF-8, nombres y títulos en español, límites e instante de observación
explícitos, escaping de celdas y neutralización de fórmulas. Se rechazan períodos,
zonas y respuestas incoherentes antes de entregar bytes. No hay exportación
silenciosa en el navegador cuando el servidor rechaza la operación.

## 8. Superficies

Panel `/admin/reportes`, navegación «Reportes / Pedidos». API bajo
`/api/admin/reporting`: GET `/orders`, `/orders/value`, `/payments` e
`/installation-zone`; POST `/orders/export`. La ruta exacta de lectura de pedidos
se conserva del candidato. Errores visibles en español y sin información privada.
La interfaz ofrece consulta, períodos rápidos, comparación y descarga cuando
están acreditadas sus condiciones; estados de carga, vacío y error distinguibles.
Teclado, foco, móvil y ambos temas forman parte del cierre.

## 9. Brechas leídas en el artefacto contrastado

`ReportsModule.cs:20` todavía declara schema nulo. `ReportsModule.cs:45–50`
monta lecturas y la costura CORE, pero no la exportación de servidor.
`OrderIndicatorsExportEndpoints.cs:43–62` contiene ese montaje pendiente.
`CoreSeamEndpoints.cs:46–55` ofrece auditoría de filas declaradas por el cliente;
se retirará esa superficie al conectar la descarga del servidor.
Estos son hechos leídos, no cierres acreditados. La candidata de zona permanece
cerrada ante falta de autoridad; no se resolverá borrando esa guarda.

## 10. Acreditación obligatoria

Puerta canónica 6/6, cero omitidas, sobre un HEAD declarado. Pruebas físicas del
historial, instalación repetida y baja/reinstalación sin tocar dueños. Activación
sin esquema roja y sin migración lateral. Instalación sin licencia y módulo
apagado sin superficies. Lecturas sin M03 y con error de M03 distintas de cero.
Indicadores con datos activos/inactivos, monedas distintas e históricos incompletos.
Exportación real con auditoría persistida y actor congelado; revocación, falta de
CSRF, fallo de auditoría y cambio de zona sin archivo. Permisos comprobados
también invocando la operación sin endpoint. Controles negativos deliberados que
hagan fallar las pruebas al retirar las guardas. Evidencia del artefacto y sus
líneas, resultados observados y límites en BITACORA; no se heredan los verdes de
una rama rescatada ni se acredita por el número de pruebas escritas.
