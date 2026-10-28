# Preparación de integraciones operativas — M11 Pagos

- **Fecha:** 19/10/2026 — America/Lima
- **Base revisada:** `0e9f0c889bc05876761ecec54533712e3a1132e4`
- **Estado:** propuesta técnica no ratificada
- **Alcance:** preparación documental; no implementa ni autoriza contratos, persistencia,
  migraciones, autenticación, proveedor, SDK o cobros reales

Este documento organiza las costuras que faltan para convertir el núcleo puro existente en una
integración operativa. No modifica `SPEC.md` ni `DECISIONES-PREVIAS-M11.md`, no asigna nombres a
interfaces, rutas, tablas o columnas y no resuelve preguntas de producto pendientes.

## 1. Decisiones que esta preparación preserva

1. M03 conserva pedido, importe autoritativo, ledger y pago comercial canónico.
2. M11 no escribe `sales.*` y no usa internamente el `DbContext` de M03.
3. El origen es `staff | provider`; un origen `provider` tiene cero atribución humana.
4. Una redirección del navegador nunca confirma el pago.
5. La confirmación exige señal verificable del lado servidor.
6. M11 recibe, verifica, registra y reintenta; M03 aplica la liquidación comercial de manera
   idempotente.
7. Las unicidades `(provider, provider_event_id)` y `external_settlement_id` son partes de un mismo
   mecanismo, no garantías intercambiables.
8. El cuerpo crudo del webhook no se conserva; sí su SHA-256, el resultado de verificación y los
   metadatos normalizados suficientes.
9. La operación técnica M11 es local/no replicada; el pago comercial M03 es replicado.
10. M11 depende de M03 y no depende de M09.
11. Mientras no exista autoridad sobre el alcance interproveedor de una liquidación, una colisión
    permanece en `HOLD`.

## 2. Costura futura M03 ↔ M11

### 2.1 Capacidad de consulta a M03

Antes de iniciar una interacción con un proveedor, M11 necesita consultar una representación
pagable cuya fuente sea M03. El contrato definitivo debe acordar, sin duplicar autoridad:

- la identidad canónica del pedido;
- el importe y la moneda definidos por M03;
- si el pedido es pagable en ese instante;
- la información de concurrencia necesaria para detectar que la representación dejó de ser
  vigente, si M03 exige esa protección;
- los motivos de rechazo que M11 puede exponer sin reinterpretar estados internos de M03.

M11 no debe calcular el importe desde líneas recibidas del navegador ni conservar una copia que se
convierta en una segunda autoridad comercial.

### 2.2 Capacidad de aplicación en M03

Después de recibir y verificar una confirmación externa, M11 necesita solicitar a M03 la aplicación
de una liquidación. El contrato definitivo debe transportar únicamente semántica ratificada:

- identidad canónica del pedido;
- identidad del proveedor;
- `external_settlement_id`;
- evidencia normalizada que permita trazabilidad, sin cuerpo crudo ni secretos;
- origen externo sin nombre, identificador local ni `HomeNode` humanos.

M03 debe decidir el resultado comercial y deduplicar por `external_settlement_id`. M11 necesita
distinguir, como mínimo, entre aplicación efectiva, repetición ya aplicada y rechazo comercial; los
nombres, forma y versionado de esos resultados quedan pendientes del contrato de M03.

### 2.3 Reglas de integración

- La llamada no otorga a M11 permiso para escribir el ledger.
- Repetir una solicitud después de una respuesta perdida no puede mover dos veces el pago o saldo.
- Dos eventos diferentes que describan la misma liquidación convergen en la barrera de M03.
- Una aceptación técnica del proveedor no sustituye el resultado comercial de M03.
- El bus interno puede notificar consecuencias, pero no es memoria durable del cobro.
- Si `PagoConfirmado` requiere datos nuevos, se versiona explícitamente; no se cambia su semántica
  de manera silenciosa.

## 3. Persistencia futura de M11

No se proponen tablas ni migraciones. La futura revisión de almacenamiento debe demostrar estas
capacidades antes de autorizar un diseño físico:

| Capacidad | Invariante exigido | Evidencia previa a migración |
|---|---|---|
| Recepción durable | unicidad de `(provider, provider_event_id)` | reentrega idéntica produce una sola recepción |
| Identidad de liquidación | no confundir evento con `external_settlement_id` | dos eventos de una liquidación llegan una sola vez a aplicación comercial |
| Evidencia | SHA-256, resultado de verificación y metadatos normalizados | cuerpo crudo y secretos ausentes |
| Procesamiento | distinguir recibido, pendiente, aplicado y fallo reintentable sin declarar nombres de estado | transición y recuperación demostrables |
| Reintento | conservar causalidad y último resultado sin autorizar dobles efectos | repetición segura tras respuesta perdida |
| Auditoría | conservar identidad, decisión, instante y referencia de evidencia | expediente reconstruible sin PAN/CVV |
| Replicación | datos técnicos locales/no replicados | clasificación de cada futura estructura |

### 3.1 Unidad de trabajo que deberá resolverse

La persistencia de recepción y el avance técnico dentro de M11 requieren una frontera transaccional
local. La aplicación en M03 cruza una frontera de módulo y no debe presentarse como una única
transacción de base de datos ya disponible. El protocolo deberá soportar:

1. recepción verificada y durable en M11;
2. selección segura de trabajo pendiente;
3. solicitud idempotente a M03;
4. caída después de que M03 aplicó pero antes de que M11 registró el resultado;
5. reintento y deduplicación en M03;
6. cierre técnico posterior en M11.

Hasta que ese protocolo y sus pruebas existan, los tipos actuales sólo modelan identidades,
evidencia y clasificación pura.

## 4. Frontera futura con proveedores

La frontera debe ser provider-neutral hacia el dominio M11 y específica sólo dentro del adaptador
elegido. Sin fijar interfaz o SDK, el flujo mínimo esperado es:

1. recibir bytes y metadatos del transporte;
2. limitar y validar el transporte antes de procesar;
3. verificar la firma con secretos externos a PostgreSQL;
4. calcular el SHA-256 sobre los bytes exactos recibidos;
5. normalizar identidades y metadatos permitidos;
6. descartar el cuerpo crudo después de producir la evidencia autorizada;
7. entregar al núcleo una evidencia verificable y no una afirmación del navegador;
8. responder al proveedor según su protocolo sin convertir esa respuesta en pago comercial.

La fábrica `VerifiedExternalPaymentEvidence.FromVerifiedWebhook` es una precondición de esa futura
frontera: no ejecuta criptografía y no debe ser accesible desde datos autodeclarados por el
webhook. La referencia de atestación usada por el núcleo también necesita procedencia confiable y
no puede provenir del payload sin verificar.

### 4.1 Aspectos que dependen del proveedor elegido

- algoritmo y material de firma;
- tolerancia temporal y defensa frente a replay de transporte;
- formato de evento, identificadores y estados verificables;
- reglas de respuesta y reentrega;
- límites de tamaño y frecuencia;
- semántica y alcance de `external_settlement_id`;
- SDK o cliente HTTP, si alguno;
- ambiente de pruebas y herramientas de simulación.

Ninguno de estos puntos tiene un valor por defecto en esta propuesta.

## 5. Matriz de fallos obligatoria

| Escenario | Resultado requerido |
|---|---|
| mismo evento, mismo cuerpo | no crear una segunda recepción ni aplicación |
| mismo evento, cuerpo diferente | conflicto prioritario y `HOLD` |
| mismo evento, liquidación diferente | conflicto prioritario y `HOLD` |
| dos eventos, misma liquidación | una sola aplicación comercial en M03 |
| misma liquidación, proveedores distintos | `HOLD` mientras el alcance sea incierto |
| evidencia rechazada o contradictoria | no avanzar a barrera durable de aplicación |
| firma no verificada | no construir evidencia confiable |
| redirect exitoso sin señal servidor | no confirmar pago |
| caída antes de llamar a M03 | trabajo reintentable pendiente |
| caída después de aplicar en M03 | reintento; M03 devuelve resultado idempotente |
| respuesta de M03 perdida | reintento sin segundo movimiento comercial |
| pedido dejó de ser pagable | M03 rechaza; M11 registra el resultado técnico |
| estado o riesgo desconocido | fallo cerrado; no aplicar |

## 6. Correspondencia con el núcleo puro existente

| Necesidad futura | Cobertura actual | Falta |
|---|---|---|
| origen humano/externo | discriminante cerrado y atribución completa | integración con contrato canónico M03 |
| identidad de evento/liquidación | claves por valor separadas | barreras durables en M11 y M03 |
| detección de replay | clasificación individual y por lote | exclusión concurrente/persistente |
| evidencia externa | forma normalizada y revisión pura | verificador auténtico y adaptador |
| conflicto e identidad interproveedor | inventario, conciliación y `HOLD` | decisión de alcance de identidad |
| revisión operativa | riesgo, carriles, auditoría y expedientes | orquestador durable autorizado |
| trazabilidad | huellas y comparación reproducibles | almacenamiento y retención autorizados |
| aplicación comercial | deliberadamente ausente | contrato público M03 y pruebas de caída |

## 7. Precondiciones para solicitar implementación operativa

1. JP resuelve las preguntas de producto abiertas.
2. La autoridad correspondiente define si `external_settlement_id` es global o acotado por
   proveedor.
3. M03 publica una propuesta versionada para consulta pagable y aplicación idempotente.
4. Se revisa si `PagoConfirmado` necesita evolución explícita.
5. Se elige proveedor y se revisan su protocolo, SDK y modelo de amenazas.
6. Seguridad valida secretos, autenticación, límites y retención.
7. Infra define el almacenamiento local/no replicado de M11.
8. Se diseña el inbox con ambas unicidades y el protocolo de recuperación.
9. Se especifican pruebas de concurrencia, reentrega y caída obligatoria.
10. JP autoriza explícitamente contratos, persistencia, migraciones y QA correspondientes.

## 8. Decisiones pendientes, sin defaults técnicos

### Producto / JP

- medios online de v1 además de tarjeta;
- pago después del vencimiento;
- pagos parciales o múltiples;
- reembolsos y contracargos;
- tratamiento de la comisión;
- moneda operativa y su autoridad.

### Arquitectura y seguridad

- alcance global o por proveedor de `external_settlement_id`;
- forma y versionado del contrato M03 ↔ M11;
- eventual evolución de `PagoConfirmado`;
- protocolo transaccional y de reintentos entre ambos módulos;
- proveedor, verificador, secretos y política de metadatos;
- dependencia explícita de M04 cuando exista autenticación de cliente.

## 9. Trabajo expresamente no autorizado por este documento

- crear interfaces productivas o rutas;
- elegir proveedor o incorporar SDK;
- implementar verificación criptográfica;
- crear persistencia, migraciones, índices o tablas;
- tocar M03, CORE, M04 u otros módulos;
- aplicar pagos, cambiar estados de pedido o escribir `sales.*`;
- relajar `HOLD` interproveedor;
- ejecutar QA/E2E o declarar idempotencia acreditada.
