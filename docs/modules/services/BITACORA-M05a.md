# Bitácora M05a

- **Creación / última modificación / última verificación:** 28/09/2026 · America/Lima
- **Base integrada:** `e839989432283c755edf7d4ae47b2c37697215ec`

## Estado de etapas

1. SPEC: ratificada y consolidada.
2. DATOS: diseñado e implementado en migración inicial y scripts.
3. API: implementada para vitrina pública, administración editorial y snapshots.
3.5. DISEÑO: **PARADA obligatoria**; no iniciada.
4. UI: no iniciada.
5. CIERRE: no iniciado.

## Decisiones aplicadas

Persistencia propia; CORE como única dependencia dura; sin M01; sin replicación; snapshot sin FK hacia M05b; sin opciones hasta decisión; sin garantía inventada para el binario histórico.

## Prueba sintética de independencia

La evidencia prevista hoy es: grafo del módulo con solo `core`; proyecto M05a sin referencias a M01/M05b/M06; migración que solo crea `services`; `99_drop.sql` que solo elimina `services`; e instalación sobre una base con CORE. No se atribuye ninguna prueba a M05b inexistente.
