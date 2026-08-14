# SILLAR — Identidad del producto

**Estado:** Base para el trabajo de diseño · **Fecha:** 14 de agosto de 2026

---

## 1. El nombre

**SILLAR** — *Sistema Integrado y Licenciable de Logística, Administración y Retail*

El sillar es la piedra volcánica blanca con la que está construida Arequipa: catedral, claustros, casonas, la ciudad entera. Cada sillar es un **bloque tallado que se apila con otros** para levantar un edificio. Ningún bloque es el edificio; el edificio es lo que se arma con ellos.

Esa es exactamente la propuesta del producto: un negocio compra los bloques que necesita y levanta su sistema. Otro negocio compra otros bloques y levanta otro sistema distinto con las mismas piezas.

La metáfora no es decorativa: **es el argumento de venta**.

---

## 2. Dos marcas que no se mezclan

Este es el punto que más fácilmente se rompe y el que más cuesta arreglar después.

| | **SILLAR** | **La marca del cliente** |
|---|---|---|
| Qué es | El producto | El negocio que lo instala |
| A quién le habla | Dueños de negocios, desarrolladores, compradores del sistema | Los clientes finales de ese negocio |
| Dónde vive | Este repositorio, documentación técnica, panel de administración, material de venta | La web pública de la instalación |
| Tono | Sobrio, técnico, confiable, sólido | El que defina cada negocio |
| Quién lo ve | El equipo y los negocios que compren el sistema | El público del negocio |

**Regla:** un visitante de la web de un cliente **nunca** debería ver la palabra SILLAR. Y un negocio evaluando comprar SILLAR nunca debería ver la identidad de otro cliente, salvo como caso de estudio con su permiso.

Consecuencia técnica: **el sistema necesita dos sistemas de diseño**, no uno.

---

## 3. Sistema de diseño del producto (SILLAR)

Cubre el panel de administración, el instalador, la documentación y el material de venta.

**Atributos:** sólido, ordenado, sobrio, técnico sin ser frío, con raíz local sin caer en lo folclórico.

**Dirección visual sugerida:**

- La paleta nace de la piedra: blancos cálidos y grises con temperatura, no el gris azulado corporativo. El sillar recién tallado es blanco crema; envejecido, gris cálido.
- Un color de acento que corte esa neutralidad y sirva para acciones y estados.
- Geometría de bloque: esquinas apenas redondeadas, módulos visualmente apilables, retículas visibles. La interfaz debería *sentirse* construida por piezas.
- Tipografía de alta legibilidad: el panel de administración se usa muchas horas seguidas y muestra tablas densas.
- Densidad de escritorio: quien administra el negocio trabaja en pantalla grande, no en móvil.

**Qué evitar:** iconografía inca literal, texturas de piedra, degradados, cualquier cosa que parezca souvenir turístico. La raíz cultural está en el nombre y en la paleta, no en los adornos.

---

## 4. Sistema de diseño de cada cliente

Cubre la web pública que ven los clientes finales. **No se define aquí**: cada instalación documenta su propia identidad en su repositorio (ADR-008), a partir de lo que pida ese negocio.

Lo único que el producto impone es el contrato técnico:

- El tema se expresa como un conjunto de variables CSS con los mismos nombres de rol que usa SILLAR: `--bg`, `--text`, `--primary`, `--border-strong`, y las demás.
- Todo par de color que produzca texto debe cumplir el mínimo AA de 4.5:1, y los bordes de controles interactivos 3:1. Si el tema de un cliente no lo cumple, no se publica.
- La geometría y el espaciado pueden variar; la estructura de los componentes, no.

Un cliente puede verse completamente distinto sin que ningún componente cambie.

---

## 5. Cómo se implementa la doble identidad

Lo que hace posible tener dos marcas sobre un mismo código es que **el tema es un dato, no código**.

- Los componentes del frontend no llevan colores escritos: consumen variables CSS.
- El tema de cada cliente se define en el repositorio de su instalación y se carga en tiempo de compilación o desde `core.site_settings`.
- El panel de administración usa siempre el tema del producto; la web pública, siempre el del cliente.
- Un cliente nuevo se ve distinto cambiando su archivo de tema, sin tocar un solo componente.

Esta separación es la que permite vender el mismo sistema a dos negocios que compiten entre sí sin que se parezcan.

---

## 6. Pendientes de marca

- [ ] Registrar el dominio antes de comunicar el nombre.
- [ ] Definir la paleta y la tipografía de SILLAR.
- [ ] Construir el sistema de diseño del producto como biblioteca de componentes.
- [ ] Logo de SILLAR.
- [ ] Decidir si el panel de administración lleva marca del producto, del cliente, o ambas.
- [ ] Publicar la plantilla de tema para instalaciones nuevas.
