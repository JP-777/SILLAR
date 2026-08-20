# M01 · Entrega 04A — Marcas

**Refina el SPEC de M01 para su alcance.** Donde ambos hablen del mismo punto, manda este
documento. Las reglas de datos y el contrato **no se repiten aquí**: están en el §7 y el §8 del
SPEC y en `DATOS.md`, que siguen siendo la fuente.

---

## 1. Por qué esta pantalla primero, siendo la menos importante

Marcas es el vertical completo más pequeño del módulo: listar, crear, editar, dar de baja y
asociar una imagen de la galería de CORE. Nada más.

Y es la primera entrega del paso 4, lo que la convierte en algo bastante más grande de lo que
parece:

> **M01 es el primer módulo, aparte de CORE, que se monta a sí mismo en el frontend.**

Todo lo que se decida aquí lo van a repetir cinco pantallas más. Por eso el criterio con el
que se revisa esta entrega **no es «¿funciona?» sino «¿se repite sin pensarlo?»**. Un patrón
torcido descubierto en marcas cuesta una pantalla; descubierto en productos cuesta seis.

De ahí también que no se generalice nada todavía: **marcas y categorías son casos uno y dos.**
Lo que parezca común después de la primera se anota; se extrae cuando exista el segundo caso
real, no antes.

---

## 2. Alcance

**Entra:**

- La ruta `/admin/catalogo/marcas` y su pantalla completa.
- La capa de servicios del módulo para marcas, sobre el cliente HTTP de `shared/http`.
- El `routes.ts` del módulo y su entrada de menú, **construida desde `GET /api/capabilities`**.
- La prueba de extremo a extremo de la pantalla, en `e2e/`.

**No entra:** categorías, productos, variantes, las tres rutas públicas y el selector de
categorías. El API ya existe entera desde el paso 3; esta entrega **no toca backend** salvo que
aparezca un hallazgo, y entonces se decide antes de tocar nada.

---

## 3. Los cuatro patrones que quedan fijados

Estos son el verdadero entregable. Están en `CLAUDE.md` y aquí se aplican por primera vez
fuera de CORE:

1. **Ningún `fetch` suelto en un componente.** El módulo tiene su capa de servicios y todo pasa
   por el cliente HTTP de `shared/http`.
2. **El módulo no importa de otro módulo.** Lo compartido vive en `src/shared/`. Si algo de
   CORE hace falta y no está expuesto, es un hallazgo, no una excusa para importar de través.
3. **El menú no se escribe a mano.** Sale de `GET /api/capabilities`, igual que el home y el
   pie.
4. **Ningún color escrito en un componente.** Solo variables de `shared/styles/tokens.css`.
   Si un estado necesita un color que no existe como variable, **es un hallazgo** — se decide,
   no se inventa.

---

## 4. Los cuatro estados de la pantalla

Obligatorios, y los cuatro se ven en la prueba:

| Estado | Qué se ve |
|---|---|
| **Vacía** | No hay ninguna marca todavía. Invita a crear la primera. No es un error y no lo parece |
| **Con datos** | El listado. Navegable con teclado, con el foco siempre visible |
| **Cargando** | Por debajo de un segundo, **nada**. El indicador entra hacia el segundo, no antes: un parpadeo hace que una respuesta rápida se perciba como lenta |
| **Conflicto** | Una frase que dice qué lo impide y qué hacer |

Los dos conflictos que esta pantalla tiene de verdad:

- **Nombre repetido.** Crear `ARTESCO` existiendo `Artesco` falla, porque la colación de
  identidad ignora mayúsculas. La frase tiene que decir **que ya existe con otra grafía**, no
  un mensaje genérico de duplicado: quien lo escribió no ve por qué choca.
- **Baja lógica con productos detrás.** Se avisa y no se actúa en cascada. El sistema nombra
  el obstáculo; la persona ordena.

Y lo transversal, que ya afirma la prueba del §9: **ningún «Ha ocurrido un error» y ningún
botón «Aceptar».** Cada botón nombra la acción que ejecuta.

---

## 5. La imagen

Se elige **de la galería de CORE**, no se sube desde aquí. Y se aplican dos cosas ya decididas:

- **Quitar la asociación no borra el archivo.**
- **Borrar el archivo desde la galería deja a la marca sin imagen, sin fallo de base de datos.**
  Ya está resuelto en datos; aquí solo hay que comprobar que la pantalla lo aguanta y no
  muestra un hueco roto.
- **Ningún identificador a la vista.** Los medios usan `uuid` y en ninguna pantalla puede
  aparecer uno. La prueba transversal ya lo vigila; conviene no reintroducirlo.

---

## 6. Verificación de esta entrega

Cierra con lo suyo, no con lo del módulo:

- [ ] La prueba de extremo a extremo pasa, y **dos ejecuciones seguidas limpias**
- [ ] Los cuatro estados se ven en la prueba, no solo el de «con datos»
- [ ] `ARTESCO` sobre `Artesco` da la frase que explica la grafía, no un duplicado genérico
- [ ] Dar de baja una marca con productos avisa y no actúa en cascada
- [ ] Recorrido completo con teclado, foco siempre visible
- [ ] `axe` limpio en los dos temas
- [ ] **Con M01 desactivado:** la aplicación arranca, no hay entrada de menú, no hay ruta
      muerta y el home no queda con un hueco
- [ ] Ningún color escrito a mano en los componentes nuevos

**Y una honestidad que pide el proyecto:** esta entrega **toca** cinco de los diecisiete
criterios de cierre de M01 y **no cierra ninguno**, porque todos necesitan también productos y
categorías. No marcar ninguna casilla del SPEC todavía. Anotar cuáles quedan a medias.

---

## 7. Lo que hay que devolver

- Qué archivos se crearon y cómo se prueba.
- **Qué patrón te obligó a dudar.** Si algo se resolvió de dos maneras posibles y elegiste una,
  eso es lo que van a copiar cinco pantallas: dilo aunque parezca menor.
- Lo que hiciste falta y `src/shared/` no tenía.
- El informe de decisiones de siempre, con `REVERSIBLE` primero si alguna no lo es.
