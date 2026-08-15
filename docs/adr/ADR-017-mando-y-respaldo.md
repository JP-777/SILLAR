# ADR-017 — Mando y respaldo entre ERP y WEB

- **Estado:** Aceptada
- **Fecha:** 15 de agosto de 2026
- **Decide:** JP
- **Enmienda:** la ADR-015 — la fila «Internet» de su tabla comparativa y la sección «Sincronización — M16»
- **Estado del trabajo:** SILLAR ERP queda aparcado. Esta decisión se registra ahora para no perderla, no para construirla.

## Qué corrige de la ADR-015

La ADR-015 tomó como punto de partida que el ERP **funciona sin internet** y que cada sucursal es un nodo autónomo que vende desconectado con normalidad.

La corrección: **internet es casi obligatorio.** El uso sin conexión queda reservado a acciones específicas y a emergencias.

No es un matiz. Cambia el modelo entero:

| Si la desconexión es lo normal | Si la desconexión es la excepción |
|---|---|
| Hay que diseñar para la divergencia: dos nodos editando lo mismo es el caso esperado | Se diseña el camino conectado, y la desconexión es un **modo degradado con reglas estrictas** |
| Reconciliación automática, con las trampas que eso arrastra | Reconciliación rara, revisable por una persona |
| Satélites con proyección parcial de los datos | Una base de mando y copias de lo compartido |

La ADR-015 describía satélites que reciben una proyección y devuelven lo suyo. Este modelo es otro: **mando y copia.**

## Decisión

**1. El mando es uno solo, y es explícito.**

| Situación del cliente | Dónde está el mando |
|---|---|
| Solo SILLAR WEB | En la nube. Esa base manda mientras no exista ERP |
| SILLAR ERP instalado | En el ERP, siempre |

Comprar el ERP no es activar módulos: es un **traspaso de mando**. Operación planificada y supervisada, con el negocio parado un rato. Nunca automática.

**2. La WEB conserva una copia viva de los datos compartidos** — catálogo, clientes y existencias. Sirve para que la tienda en línea siga funcionando cuando el nodo de mando no responde.

**3. En modo degradado la copia sirve lecturas y acumula lo suyo. No toma el mando.** Los pedidos que entren por la web se guardan y se aplican cuando el mando vuelve. La copia **no descuenta existencias como si mandara**: anota la intención, no el hecho.

**4. Retomar el mando es un acto humano.** Ningún nodo se declara mando a sí mismo porque dejó de oír al otro. Esa es la puerta por la que entra el problema de las dos bases que creen mandar a la vez.

## Dos cosas distintas que la palabra «respaldo» junta

Es la precisión más útil de esta decisión, porque las dos hacen falta y ninguna sustituye a la otra:

| | **Réplica en caliente** | **Respaldo** |
|---|---|---|
| Qué es | Una base viva con los datos compartidos | Un volcado periódico de la base de mando entera |
| Contra qué protege | El mando **no responde ahora** | El mando **se perdió** — disco muerto, robo, incendio |
| Quién la usa | La tienda en línea, sola y al momento | Una persona, para reconstruir |
| Cuán al día está | Segundos o minutos | Horas |
| Dónde vive | La instalación web del cliente | Almacenamiento aparte, fuera de la máquina del negocio |

Que la web tenga una réplica **no es tener respaldo**. Una réplica copia fielmente también el borrado accidental de doscientos productos.

## «Al final habrá dos bases y ambas serán la misma»

Con una precisión: **los mismos datos compartidos, no el mismo esquema.**

| Solo en el mando (ERP) | Compartido — se replica | Solo en la WEB |
|---|---|---|
| Caja, turnos, arqueos | **M01 Catálogo** | Contenido y banners |
| Compras y proveedores | **M04 Clientes** | Captación y suscripciones |
| Comprobantes emitidos | **M09 Existencias** | Carrito y sesiones de compra |

Son exactamente los tres módulos que la ADR-013 ya había señalado como comunes. Aquello sobrevive.

Hacer las dos bases idénticas obligaría a que la instalación web cargue el esquema de caja y compras que nunca va a usar, y que el ERP cargue el de banners y suscripciones. Se replica lo compartido, que es lo que de verdad tiene que coincidir.

## Lo que esto no salva, y conviene tenerlo claro

**Internet caído no es lo mismo que ERP caído.**

| Qué falla | Quién sigue vendiendo | Con qué |
|---|---|---|
| Se cae internet en la tienda | El mostrador | Su base local. La web queda con la réplica, sin recibir novedades |
| Se cae la máquina del ERP | La tienda en línea | La réplica de la nube. **El mostrador no vende** |
| Se cae el proveedor de comprobantes | Ambos | La venta se guarda; el comprobante se encola (ADR-014) |

La segunda fila es la que importa reconocer: **la copia de la web no rescata al mostrador.** Si el mostrador pierde internet, tampoco alcanza la nube. Al mostrador lo rescata su base local; a la tienda en línea la rescata la réplica. Son dos protecciones distintas para dos caídas distintas, y ninguna cubre a la otra.

## El conteo global es una vista, no una bolsa

Las existencias siguen siendo **por sucursal**: es lo que impide que dos locales vendan la misma última unidad. Sobre eso se añade un **conteo global**, que es la suma de lo que hay en cada sitio.

```
Cuaderno A4 con diseño ····· 1000 en total
   Principal          500
   Sucursal 2         250
   Sucursal 3         250
```

La distinción que sostiene todo lo demás:

| El conteo por sucursal | El conteo global |
|---|---|
| Es un **hecho**: hay 250 en ese estante | Es una **suma**: un número calculado |
| De ahí se descuenta al vender | **De ahí no se descuenta nunca** |
| Manda | Informa |

Nadie vende «del global». Se vende de una sucursal concreta, siempre. El global existe para **saber que la solución está en otro sitio** cuando aquí se acabó.

### Solo se suma lo que está conectado

Una sucursal desconectada **no entra en la suma**. No se muestra su último número conocido: se muestra el total de las demás y un aviso que dice qué falta y por qué.

Es más simple que arrastrar la antigüedad de cada réplica, y es más seguro por una razón concreta: **el error va siempre hacia abajo.** El sistema puede quedarse corto, nunca prometer de más. Quedarse corto se corrige con una llamada; prometer de más se corrige delante del cliente.

Y es el criterio de siempre —fallar ruidosamente antes que degradarse en silencio— aplicado a un número. Un total que baja sin explicación asusta; un total que baja **con la frase que lo explica** es información.

> Falta decidir qué cuenta como «conectada»: responde ahora, o sincronizó hace menos de N.
> Va en el SPEC de M17.

### Preguntar no es comprar

La mayoría de quien pregunta por un producto no lo va a llevar. Reservar en cada consulta llenaría las otras sucursales de apartados fantasma, que es el mismo problema al revés.

Así que la consulta es **libre y no compromete nada**, y lo que se le dice al cliente depende de cuánto hay:

| Situación | Qué se le dice |
|---|---|
| Hay bastante en otro local | «Sí hay, en la sucursal de X» |
| Quedan pocos | «Quedan pocos en X» — y ahí es donde la conversación cambia |

**La reserva nace de la intención, no de la consulta.** Cuando el cliente sí va a comprar, hay dos acciones y ninguna es automática:

1. **Comprar aquí y separarlo allá**, para que vaya a recogerlo.
2. **Pedir el traslado**, para que se lo traigan.

Las dos son documentos explícitos y las dos sí comprometen existencias en el otro local. El traslado no es un efecto secundario de haber mirado un número.

> Que la escasez se comunique como «quedan pocos» y no como «quedan 3» tiene un efecto útil
> además de conversacional: una banda no promete una cifra. Si en el intervalo se vendió uno,
> «quedan pocos» sigue siendo verdad.

### Todo esto es el módulo M17 Sucursales

El conteo global, la consulta a otro local, la separación y el traslado **no se reparten entre M09 y M13: viven en un módulo propio**, `branches`.

**El módulo no aporta «sucursal»: aporta «más de una».** Sin él instalado hay una sola ubicación, M09 guarda las existencias contra ella y nada de esto aparece en la interfaz. Con él, hay varias y aparece todo junto.

La dependencia va de M17 hacia M09 —M17 le pregunta a M09 cuánto hay en cada ubicación— y **M09 no sabe qué es una sucursal**. Al revés contaminaría el inventario de un negocio de un solo local con un concepto que no usa.

### En la tienda en línea

La instalación web sigue atada a **una** ubicación para lo que despacha (ADR-015). El conteo global no cambia eso: cambia lo que puede *decir*. «Agotado» y «agotado aquí, disponible en tienda» son promesas distintas, y solo la segunda necesita M17. Con la misma regla del mostrador: mostrarlo no aparta nada; apartar es un acto del comprador.

## Lo que queda abierto

Se decide cuando el ERP se retome, no antes. Anotarlo es el punto de este documento:

1. **Sucursales.** Con internet casi obligatorio, ¿siguen siendo nodos autónomos con base propia, o hay un solo mando y las demás máquinas son terminales? La respuesta cambia por completo el tamaño de M16.
2. **Qué cuenta como sucursal «conectada»** para entrar en la suma: que responda ahora, o que haya sincronizado hace menos de N. Va en el SPEC de M17.
3. **Cuánto dura una separación** antes de liberarse sola, y qué pasa si el local que la concedió deja de responder con la separación viva.
4. **Dónde está el umbral de «quedan pocos».** Fijo, por producto o por rotación. Empezar por lo simple: un número en la configuración de M17.
5. **Los pedidos web acumulados en modo degradado.** Si entran cuarenta pedidos mientras el mando no responde y al volver no hay existencias para todos, ¿quién decide qué se atiende? Hay respuesta técnica y hay respuesta de negocio, y aquí manda la de negocio.
6. **Quién declara la vuelta del mando**, con qué pantalla y con qué comprobación previa de que la réplica no perdió nada.
7. **El traspaso de mando al comprar el ERP.** Procedimiento escrito, con marcha atrás.
8. **Cada cuánto se replica y qué se considera «desincronizado».** Un nodo que muestra datos de ayer sin avisar es peor que uno caído. Con el conteo global esto deja de ser cosmético: la antigüedad de la réplica es la calidad del número que se le enseña al cliente.

## Consecuencias

**Positivas.** Desaparece la reconciliación automática, que es donde fallan todos los sistemas de este tipo. Un solo escritor en cada momento hace que «ambas bases son la misma» sea una afirmación sostenible y no una aspiración. Y el cliente que empieza solo con la WEB tiene un camino de crecimiento definido en vez de una migración improvisada.

**Negativas.** Aparece una operación manual —el traspaso y la vuelta del mando— que hay que documentar, probar y saber ejecutar bajo presión, que es justo cuando ocurre. Y la web en modo degradado acepta pedidos que quizá no se puedan atender: es una promesa a medias, y hay que redactarla en la interfaz con honestidad.

## Lo que no cambia

- **La ADR-016 sigue en pie, y con más razón.** Aunque el mando sea uno, siguen naciendo filas en dos sitios: pedidos en la web, ventas en el mostrador. Claves `uuid` v7 en lo que se replica.
- **Las existencias por sucursal** siguen siendo la decisión correcta, y ahora además hacen que la réplica web se ate a un almacén concreto sin ambigüedad.
- **La cola de comprobantes de la ADR-014.** Lo que sí decae es la enmienda que decía que era «la única forma posible» por el trabajo sin internet. La cola se mantiene por otra razón, igual de buena: SUNAT y los proveedores se caen solos, y nadie debe esperar de pie en el mostrador a que responda un servicio ajeno.
