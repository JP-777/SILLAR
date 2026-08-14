# Módulos del frontend

Vacío a propósito. Que exista sin contenido comunica dónde va lo que viene.

Cada módulo traerá su propia carpeta con esta forma:

```
modules/<código>/
├── components/
├── pages/
├── services/     nada de fetch suelto: la capa de servicios del módulo
├── types/
└── routes.ts     rutas y entradas de menú que exporta
```

Reglas (ADR-005):

1. Un módulo **nunca importa** de otro módulo. Lo compartido vive en `shared/`.
2. Cada módulo exporta sus rutas; la aplicación monta solo las de los módulos
   activos, según `GET /api/capabilities`.
3. El menú se construye desde ahí. Nada escrito a mano en el armazón.
4. Un módulo desactivado no deja rutas muertas ni huecos visuales.
