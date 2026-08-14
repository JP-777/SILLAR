# ADR-007 — Nombre del producto: SILLAR

- **Estado:** Aceptada
- **Fecha:** 2026-08-14
- **Decide:** JP

## Contexto

Todo el trabajo previo se nombraba como el negocio que originó el encargo: repositorio, base de datos, documentos y nombres previstos para los proyectos .NET. Con el giro a producto modular, ese negocio pasa a ser **la primera instalación**, no el sistema. Un producto que se venderá a otros negocios no puede llevar el nombre de uno de sus clientes.

Requisitos fijados por JP: nombre con raíz cultural peruana o arequipeña, y que además funcione como **acrónimo**.

## Decisión

El producto se llama **SILLAR**.

> **S**istema **I**ntegrado y **L**icenciable de **L**ogística, **A**dministración y **R**etail

El sillar es la piedra volcánica blanca con la que está construida Arequipa. Es, literalmente, **un bloque modular con el que se levantan edificios**: la metáfora exacta de un sistema que se arma por módulos independientes.

## Aplicación

```
Repositorio        sillar  (instalaciones: sillar-cliente-<negocio>, ver ADR-008)
Solución .NET      Sillar.sln
Proyectos          Sillar.Api · Sillar.Shared · Sillar.Core
Módulos            Sillar.Modules.Catalog · Sillar.Modules.Sales · …
Contenedores       sillar_db · sillar_pgadmin
Base de datos      nombre del negocio instalado, no del producto
```

La base de datos conserva el nombre del cliente porque, bajo el modelo de una instancia por cliente (ADR-001), la base pertenece al negocio instalado y no al producto.

## Alternativas evaluadas y descartadas

| Nombre | Motivo del descarte |
|---|---|
| **Quipu** | Ocupado por un software español de facturación y contabilidad. Colisión directa de rubro. |
| **Tambo** | Tambo+ es una cadena de tiendas de conveniencia de gran presencia en Perú. Inviable en el mercado objetivo. |
| **Chaski / Chasqui** | Existen chaskiSoft y ChasquiSoft, empresas peruanas de sistemas de gestión, además de la logística Chazki. Colisión directa. |
| **Misti** | Buen acrónimo y sin producto de gestión homónimo, pero existe la consultora Misti Code y el programa MISTI del MIT. Quedó como segunda opción. |
| **Ampato**, **Apu** | Válidos, pero menos reconocibles o con demasiado poco margen de marca. |

## Verificación realizada

Búsqueda de conflictos en software: no se encontró ningún producto ni empresa de tecnología llamada Sillar. Los únicos resultados son constructoras e inmobiliarias españolas y un topónimo boliviano, sin relación con el rubro.

## Pendiente

- Registrar el dominio antes de comunicar el nombre públicamente.
- Definir la identidad visual del producto, distinta de la de cualquier cliente. Ver `docs/MARCA.md`.
