# ADR-019 — Un módulo activo que no está en el binario aborta el arranque

- **Estado:** Aceptada
- **Fecha:** 16 de agosto de 2026
- **Decide:** JP
- **Matiza:** el comportamiento de `is_orphan` de CORE

## Cómo apareció

Al reconstruir el contenedor se descubrió que el `Dockerfile` nunca había incluido a M01. El resultado no fue un error: fue un arranque correcto que anunciaba

```
Módulos descubiertos: 1 (core)
```

Un módulo entero desaparecido, y el sistema funcionando. Aquí no hizo daño porque M01 todavía no está activo en ninguna base. **Con una instalación real, esa misma línea significa una clienta cuya web se quedó sin catálogo, sin que nadie se entere.**

## Por qué el aviso del arranque no basta

Se revisó si la línea servía de salvaguarda. No sirve, por tres razones que conviene tener escritas:

1. **Es un número entre otros.** Sale junto a `Now listening on…`. Nada distingue un 1 correcto de un 1 equivocado.
2. **Exige que alguien lea y cuente.** Una salvaguarda que depende de la atención humana en cada despliegue no es una salvaguarda.
3. **Desaparece sola.** El día que alguien suba el nivel de registro para quitar ruido —una maniobra normal en operación— la línea se va con él, en silencio.

## Decisión

**Si un módulo está marcado activo en `core.modules` y el descubrimiento no lo encuentra, el host aborta el arranque.**

Reutiliza la maquinaria que ya existe: el host ya aborta cuando un módulo activo tiene una dependencia dura inactiva. Un módulo activo que directamente **no está** es un caso estrictamente peor.

### Por qué no rompe la desinstalación legítima

`is_orphan` se conserva para lo que fue pensado, y la distinción sale sola del estado que ya guardamos:

| En `core.modules` | En el binario | Qué es | Qué hace el host |
|---|---|---|---|
| Activo | Presente | Normal | Arranca |
| **Activo** | **Ausente** | **Despliegue defectuoso** | **Aborta, nombrando el módulo** |
| Inactivo | Ausente | Desinstalado a propósito | `is_orphan`, arranca |

Nadie retira el binario de un módulo **activo** a propósito: la desinstalación empieza por desactivarlo, que es una acción explícita del panel y ya provoca su reinicio. Lo que queda del otro lado es siempre un error de despliegue.

## Alternativas

| Opción | Por qué se descarta |
|---|---|
| Subir el nivel del aviso a advertencia o error | Sigue siendo texto que alguien tiene que leer, y sigue desapareciendo si se filtra el registro |
| Comprobación de salud posterior al arranque | Llega tarde: el sistema ya está sirviendo peticiones sin el módulo |
| Lista de módulos esperados en configuración | Otra lista escrita a mano que se queda corta sola. Es justo el error que causó esto |

La base de datos **ya sabe** qué debería estar. No hace falta inventar una segunda fuente de verdad.

## Consecuencias

**Positivas.** Un despliegue incompleto se detiene en vez de servir un producto mutilado, y el mensaje nombra qué falta. Se apoya en la política `restart: unless-stopped` del contenedor, que hoy relanza el host tras cada activación: el fallo será visible y repetido, no un silencio.

**Negativas.** Un error de empaquetado deja el sistema **caído** en vez de degradado. Es deliberado y es la línea de siempre —a medias es peor que parado, porque nadie lo mira—, pero conviene decirlo antes de que ocurra a las once de la noche.

**Riesgo a vigilar.** Que alguien resuelva un arranque abortado desactivando el módulo en la base para «que levante». Eso convierte un despliegue roto en una web sin catálogo, que es exactamente lo que esta decisión evita. El mensaje de aborto tiene que decir qué hacer: **reconstruir la imagen**, no tocar la base.
