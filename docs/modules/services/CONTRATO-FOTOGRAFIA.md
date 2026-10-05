# Contrato de fotografía M05a → futuro M05b

- **Creación / última modificación / última verificación:** 28/09/2026 · America/Lima
- **Base verificada:** `e839989432283c755edf7d4ae47b2c37697215ec`
- **Commit de implementación verificado:** `c30c9666534edd4ddaa570870f3e992fe2552c30`

`IServiceShowcaseSnapshots.GetPublishedSnapshotAsync` devuelve una fotografía editorial puntual. El consumidor copia nombre, slug, descripciones, precio, unidad, identificador de medio, URL y texto alternativo. No hay FK entre `services` y `service_orders`, ni lectura posterior obligatoria de M05a.

El contrato garantiza que eliminar M05a no destruye filas futuras de M05b. No garantiza que una URL siga sirviendo el binario si CORE da de baja el medio. Resolver eso exige elegir entre: conservar binarios lógicamente en CORE; copiar el binario a almacenamiento propio de M05b; o aceptar una fotografía histórica solo textual. M05a no decide ni implementa esa política.
