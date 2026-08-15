# ADR-014 — La emisión de comprobantes se delega a un proveedor autorizado

- **Estado:** Aceptada
- **Fecha:** 14 de agosto de 2026
- **Decide:** JP

## Contexto

En Perú, la emisión de boletas y facturas electrónicas es obligatoria y está regulada por SUNAT. Bsale, el sistema que se va a reemplazar, no es solo un punto de venta: es también el emisor autorizado del negocio.

Reemplazarlo no es reemplazar una interfaz. Es asumir la responsabilidad de emitir documentos con validez fiscal, lo que implica certificado digital, XML en formato UBL firmado, envío a SUNAT o a un Operador de Servicios Electrónicos, series y correlativos, notas de crédito y débito, resúmenes diarios de boletas y un procedimiento de contingencia para cuando SUNAT no responde.

## Alternativas

| Opción | Por qué se descartó o se eligió |
|---|---|
| **Emisor propio** | Control total y sin costo por comprobante, pero es un proyecto en sí mismo con una superficie regulatoria que cambia cuando SUNAT decide que cambia. Gasta el presupuesto en la parte que el cliente **ya tiene resuelta** |
| **Delegar en un proveedor con API** | Semanas en vez de meses. El proveedor vive de mantener la normativa al día. Costo por comprobante y dependencia de un tercero para poder facturar |

## Decisión

**El sistema arma la venta; la emisión fiscal se delega a un proveedor autorizado a través de su API.**

El módulo **M14 Comprobantes Electrónicos** encapsula esa integración por completo. Ningún otro módulo sabe que existe un proveedor externo: piden emitir y reciben un comprobante o un error.

## Razón de fondo

Lo que el cliente está comprando no es la capacidad de emitir una boleta — eso ya lo tiene y funciona. **Lo que compra es que el flujo de venta deje de ser tedioso.** Construir el emisor consumiría el proyecto entero en la parte que no le duele.

## Diseño

- **El proveedor está detrás de una interfaz.** `IFiscalDocuments` en los contratos de M14. Cambiar de proveedor —o construir el emisor propio algún día— es escribir una implementación, sin tocar el punto de venta.
- **Las ventas se guardan siempre, emitan o no.** La venta es un hecho del negocio; el comprobante es un trámite sobre ese hecho. Si la emisión falla, la venta ya ocurrió y no puede perderse.
- **La emisión es asíncrona con reintentos.** Nadie debe esperar de pie en el mostrador a que responda un servicio externo. La venta se cierra, el comprobante se emite y se entrega en cuanto vuelve.
  **Con la ADR-015 esto deja de ser una comodidad y pasa a ser la única forma posible:** el ERP vende sin internet, y sin internet no se emite a SUNAT. La cola no es una optimización, es el mecanismo.
- **Cola visible.** Debe existir una pantalla que muestre los comprobantes pendientes, los fallidos y por qué. Una integración fiscal que falla en silencio es un problema contable que se descubre semanas después.
- **Contingencia.** Si el proveedor está caído, el sistema sigue vendiendo y acumula. Lo que no puede pasar es que el negocio no pueda cobrar porque un servicio externo no responde.

## Consecuencias

**Positivas.** Semanas en lugar de meses. Los cambios normativos los absorbe el proveedor. El punto de venta se puede vender fuera de Perú simplemente no activando M14, porque la dependencia es blanda (ADR-013).

**Negativas.** Costo recurrente por comprobante o por plan, que hay que trasladar al precio de la edición operativa. Dependencia de un tercero para una función legalmente obligatoria: si el proveedor cierra, hay que migrar. La interfaz reduce ese riesgo pero no lo elimina.

## Pendiente de averiguar con el cliente

M14 pertenece a **SILLAR ERP** y se activa cuando el negocio lo necesita. El cliente que motivó el encargo eligió su sistema actual porque necesitaba **un sistema**, no por los comprobantes: el punto de venta eficiente es el producto y la emisión fiscal es un módulo que se enciende cuando toca.

Antes de elegir proveedor concreto:

- Qué contrató con Bsale y qué parte es emisión fiscal.
- Si tiene certificado digital propio o va incluido en el servicio.
- Cuánto paga hoy, para saber contra qué se compara.
- Volumen mensual de comprobantes, que define el plan.
- Series y correlativos en uso, que hay que continuar sin saltos.

**El último punto es el que más fácilmente se pasa por alto**: al migrar de sistema, la numeración no puede reiniciarse ni saltar. Es de las cosas que SUNAT sí mira.

---

**Fuentes consultadas:** portal de Comprobantes de Pago Electrónicos de SUNAT y la documentación de integración de NubeFacT.
