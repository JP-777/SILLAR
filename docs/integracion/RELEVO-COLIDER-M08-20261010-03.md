# SILLAR — Acta M08-03: sincronización documental y revisión diferida

**Fecha:** sábado 10 de octubre de 2026 (America/Lima; hora exacta no registrada).
**ID:** SILLAR-COLIDER-M08-20261010-03.
**Responsables:** JP (autoridad de producto/integración) y Chat 2 (documentación).
**Estado:** QA INDEPENDIENTE PENDIENTE; HOLD merge a main.

## 1. Cronología y referencias

- Base histórica main: `4a3fd1ef12fc2226bad185f003acea9168b9a4c9`.
- 09/10/2026, 23:36 Lima: contratos M05b/M06 publicados en `963e0f1d12b131c4296cd836c88c3334fc22288e` (11 archivos; rama de integración).
- 09/10/2026, 23:36 Lima: documentación M08 D1–D6 publicada en `5e3154ff23049a7d64fc798f347e47eaed77c543`.
- 10/10/2026: acta 01/02 publicada en `d61c3143d0d8166b92c9b95af1062195810fada8`.
- 10/10/2026: JP presentó prueba VERDE→ROJO→VERDE de propiedad y privacidad, cinco TRX sin omisiones, en `/var/tmp/sillar-m08-vrv-20261010-lmsjMoLj`. Las evidencias TRX siguen locales, no están incorporadas a este commit.

## 2. Alcance de esta actualización

- `docs/modules/portal/SPEC.md`: se separa el estado histórico de main del estado actual de contratos publicados y se recuerda que no hay API HTTP de M08.
- `docs/modules/portal/MATRIZ-CONTRATOS-Y-DECISIONES.md`: inventario de las interfaces materializadas, diferenciando rama frente a main.
- `docs/modules/portal/PLAN-IMPLEMENTACION-M08.md`: P1.2 y P1.3 publicados en rama, no integrados; Codex/Agente A solo deben desarrollar sobre una base que incorpore el commit contractual.

Las decisiones D1–D6 no se alteran. No se modifica código ni se aprueba integración a main.

## 3. Revisión diferida al colíder

| Frente | Estado | Revisión exigida |
|---|---|---|
| Propiedad M05b en SQL, lista/detalle/NULL | QA focal interna PASS | Reproducir y auditar contra SHA `963e0f1` |
| DTO público M06 y privacidad | QA focal interna PASS | Confirmar exclusión estructural de notas, prioridad, plazos internos y personal |
| Sabotajes | VERDE→ROJO→VERDE reportado PASS | Reproducción independiente de cinco TRX sobre SHA exacto |
| DI y módulos opcionales | Contratos registrados | Validar activación real, ausencia M03/M06 y desactivación durante lectura |
| API HTTP de M08 | No desarrollada | Política `crm:customer`, identidad por sesión M04, 404 ajeno, sin `customerId` externo |
| Puerta canónica M08 | PENDIENTE | 6/6, `[M08-CICLO]` en etapa 6 y PostgreSQL real, sin skips |
| Documentación | Esta actualización | Revisar coherencia y commits del expediente cronológico |

## 4. Continuidad autorizada sin colíder

- JP puede encargar desarrollo M08 a Codex en rama aislada, con Chat 3 en un territorio distinto.
- Chat 2 mantiene las costuras compartidas y nuevas actas cronológicas fechadas.
- Ninguna prueba propia sustituye el dictamen independiente ni habilita un merge automático.
- Primero M08; luego planificación de módulos nuevos; finalmente reparto de la jornada automática de sábado/domingo.

**HOLD:** revisión del colíder y merge a `main`. El desarrollo en ramas puede continuar.
