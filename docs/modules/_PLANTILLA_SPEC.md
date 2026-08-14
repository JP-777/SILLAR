# SPEC — M?? Nombre del módulo

- **Código:** `codigo_modulo`
- **Schema:** `codigo_modulo`
- **Versión:** 1.0.0
- **Estado:** Borrador · En revisión · Aprobado · Implementado · Cerrado
- **Fase:** —

---

## 1. Propósito

Qué resuelve este módulo, en dos o tres frases. Qué deja de ser posible si el cliente no lo compra.

## 2. Valor comercial

Por qué un negocio pagaría por esto por separado. A qué tipo de negocio le sirve aunque no compre los demás módulos.

---

## 3. Dependencias

| Módulo | Tipo | Qué necesita de él | Comportamiento si no está |
|---|---|---|---|
| CORE | Dura | Autenticación, settings | No aplica: CORE siempre está |
| M0? | Blanda | … | … |

**Módulos que dependen de este:** …

---

## 4. Modelo de datos

### Tablas

Una sección por tabla, con el diccionario completo en el formato acordado.

#### `codigo_modulo.nombre_tabla`

Propósito de la tabla.

| Campo | Tipo | Nulo | Clave | Descripción | Regla de negocio | Default |
|---|---|---|---|---|---|---|
| | | | | | | |

**Restricciones:** …
**Índices:** …

### Relaciones internas

```
tabla_a 1 ─── N tabla_b
```

### Relaciones cruzadas

| Origen | Destino | Tipo | FK física | Dónde se declara |
|---|---|---|---|---|
| | | dura / blanda | sí / no | script base / integración |

### Datos semilla

Qué datos mínimos necesita el módulo para funcionar recién instalado.

---

## 5. Contrato público

Lo único que otros módulos pueden ver de este.

```csharp
namespace Sillar.Modules.<Modulo>.Contracts;

public interface I<Modulo>Service
{
    // …
}
```

**Eventos publicados:** …
**Eventos consumidos:** …

---

## 6. Endpoints

| Método | Ruta | Descripción | Autenticación |
|---|---|---|---|
| GET | `/api/…` | | pública / admin |

Incluir para cada uno: parámetros, ejemplo de respuesta y códigos de error.

---

## 7. Interfaz de usuario

**Rutas públicas:** …
**Rutas de administración:** …
**Componentes principales:** …
**Qué desaparece de la web si el módulo se desactiva:** …

---

## 8. Reglas de negocio

Numeradas, verificables y trazables al PRD cuando corresponda.

---

## 9. Criterios de aceptación

- [ ] El schema se crea y se elimina sin afectar a otros módulos
- [ ] Los scripts son idempotentes
- [ ] Con el módulo desactivado, la aplicación arranca y no quedan rutas muertas ni enlaces rotos
- [ ] Con una dependencia blanda ausente, el módulo funciona en modo degradado sin errores
- [ ] Todos los endpoints documentados en Swagger
- [ ] La interfaz responde correctamente en móvil y escritorio
- [ ] …criterios específicos del módulo

---

## 10. Fuera de alcance

Qué queda explícitamente para después, y en qué módulo o fase.
