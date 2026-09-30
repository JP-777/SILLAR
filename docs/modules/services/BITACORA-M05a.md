# Bitácora M05a

- **Creación:** 28/09/2026 · **Última modificación / verificación:** 30/09/2026 · America/Lima
- **Base integrada:** `e839989432283c755edf7d4ae47b2c37697215ec`
- **Commit de implementación verificado:** `c30c9666534edd4ddaa570870f3e992fe2552c30`

## Evidencia de etapa

- Pruebas focales: 7/7, sin omitidas.
- Falsificación B2: guarda de precio debilitada deliberadamente; 1 fallo esperado. Restauración: 7/7.
- Generación SQL mediante EF: no ejecutada; el proyecto no incorpora `Microsoft.EntityFrameworkCore.Design` y la costura de host aún no está autorizada.
- Montaje/desmontaje real: pendiente de costura con host/solución y prueba con infraestructura; no se declara superado.

## Estado de etapas

1. SPEC: ratificada y consolidada.
2. DATOS: diseñado e implementado en migración inicial y scripts.
3. API: implementada para vitrina pública, administración editorial y snapshots.
3.5. DISEÑO: publicado en `JP-777/SILLAR-DESIGN`, rama `diseno/m05a-paso-3-5`, SHA `40f6b2c48a3ba2398f3a927826a1f48a51917f99`.
4. UI: implementación estructural de S1, S2, S3, S4, S5-admin y S5-public publicada sobre `68a09aa744ce3bea27fff005e4f49378fb24ace8`; microcorrección de validación contextual incorporada posteriormente en esta rama. La validación visual final de S1/S2/S5-public sigue pendiente del tema real de una instalación cliente.
5. CIERRE: no iniciado.

No se declara ejecutada la QA canónica ni cerrado el módulo.

## Decisiones aplicadas

Persistencia propia; CORE como única dependencia dura; sin M01; sin replicación; snapshot sin FK hacia M05b; sin opciones hasta decisión; sin garantía inventada para el binario histórico.

### Rectificación documental de conflictos — 29/09/2026

El colíder adoptó la opción B: el diseño solo representa conflictos respaldados por el
contrato existente. Se retiró la promesa de detectar una edición simultánea porque
`UpdateAsync` no verifica versiones. Quedan representados slug duplicado, transición no
permitida, orden inválido, validación contextual, ausencia pública al consultar o revalidar,
fallback de imagen y fallo de red recuperable. La concurrencia editorial queda pendiente
hasta que más de una persona use simultáneamente el editor.

## Prueba sintética de independencia

La evidencia prevista hoy es: grafo del módulo con solo `core`; proyecto M05a sin referencias a M01/M05b/M06; migración que solo crea `services`; `99_drop.sql` que solo elimina `services`; e instalación sobre una base con CORE. No se atribuye ninguna prueba a M05b inexistente.
