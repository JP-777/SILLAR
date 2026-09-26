# M07 — Matriz de reconciliación con el encargo del 26/09/2026

Creado: 26/09/2026, America/Lima · Última verificación: 26/09/2026 ·
Commit base comprobado: `711bfba7cf3be80baa146b44e79ddf7a633d695d`.

Qué decía la SPEC, qué cambió el 26/09, qué se modifica y qué se eleva. **El texto histórico no se
borra**: las modificaciones están en `SPEC.md` como bloques «Enmienda 26/09» junto al párrafo que
tocan. Las citas `SPEC.md` se refieren a la versión del commit base.

| # | Texto actual | Decisión / hecho del 26/09 | Modificación | Escalada |
|---|---|---|---|---|
| 1 | M01 y M04 **duras**, FK dentro de la migración de M07 (§3) | Ratificado en el encargo: no pasar a blanda | **Ninguna** | — |
| 2 | `special_order_leads` **refresca**; `quote_lines` **congela** (`DECISIONES-PREVIAS-M07.md` §4) | Conservar la distinción y sus pruebas | Criterio nuevo que afirma las dos por efecto (§9) | — |
| 3 | «No existe caducidad por tiempo» (§8, regla 7) | Se refiere a presupuestos de M07, no a pedidos de M03 | Aclaración de alcance, sin cambiar la regla | — |
| 4 | Regla 3: tres renglones (personalización · volumen · carrito) | Productos «a consultar» fuera del carrito; su acción es de M07 | Se anuncia el cuarto renglón, **sin escribirlo** | **E1** |
| 5 | — (no había frontera escrita con M03) | Texto obligatorio, idéntico al de A | Se inserta literal tras §1 y en §10 | C3 |
| 6 | «Públicos — todos con sesión de cliente, ninguno anónimo» (§6) | El enlace «a consultar» podría abrirse sin sesión | **Se mantiene la regla**; se eleva | **E2** |
| 7 | «El ritmo se limita contra la cuenta, igual que hace CORE en el acceso» (§6) | `LockoutPolicy.cs:3-17` es bloqueo por intentos fallidos, no límite de escrituras | M07 implementa su propio límite por cuenta; criterio nuevo | — |
| 8 | `quote_lines.product_id → catalog.products`, «precio de lista» (§4) | M01 cobra por presentación (`catalog/SPEC.md:400`, `ItemSnapshot.cs:22-34`) | Tramo en suspenso | **E3** |
| 9 | Regla 8: `catalog_price_at_quote` nulo = «no viene del catálogo» | Un producto «a consultar» también da nulo | En suspenso con 8 | **E3** |
| 10 | `reference_image_id → core.media_assets` (§4) | `IMediaStorage` solo da rutas públicas (`IMediaStorage.cs:5-8`) | Columna en suspenso | **E4** |
| 11 | `quote_number` «legible, con serie de nodo delante» (§4), sin formato | A tiene conflicto propio con la ADR-016 | Ninguna hasta resolver | **E5** |
| 12 | «Rutas públicas: ninguna», el formulario vive en la ficha (§7) | Volumen «puede no existir en el catálogo» (§4): no tiene ficha | Contradicción anotada | **E9** |
| 13 | «El botón de solicitud que M01 aloja en la ficha» (§7) | No existe superficie en la ficha; M07 no importa de M01 ni M03 | Petición de costura | C1 |
| 14 | §7 sin lista de pantallas con estados | Diseño necesita la lista íntegra con cuatro estados, claro/oscuro, móvil/escritorio | **§7.1 añadido**; §7 y §9 **no se renumeran** | — |
| 15 | Criterios de aceptación de §9 | Conservarlos salvo cambio aprobado | **Todos intactos**; siete añadidos debajo | — |
| 16 | `ARQUITECTURA_MODULAR.md:198`: `quotes` en «fase 2»; `:219-220` solo dos FK | La SPEC trae `quotes` en 1.0.0 y seis FK cruzadas | Gana la SPEC; se avisa, no se edita (es de Chat 2) | C4 |
| 17 | Paso 2 del ciclo | Requiere la barrera de fronteras **en `main`** | Bloqueado: no está en `711bfba` | C5 |
