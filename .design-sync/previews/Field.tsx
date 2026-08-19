import { Field, Input } from 'sillar-frontend';

export function Basico() {
  return (
    <div style={{ maxWidth: 360 }}>
      <Field label="Nombre del producto" hint="Como aparecerá en el catálogo público">
        {(props) => <Input placeholder="Cuaderno universitario cuadriculado A4" {...props} />}
      </Field>
    </div>
  );
}

export function Requerido() {
  return (
    <div style={{ maxWidth: 360 }}>
      <Field label="Correo electrónico" required>
        {(props) => <Input type="email" placeholder="admin@sillar.pe" {...props} />}
      </Field>
    </div>
  );
}

export function ConError() {
  return (
    <div style={{ maxWidth: 360 }}>
      <Field label="Código SKU" required error="Ya existe un producto con este código">
        {(props) => <Input defaultValue="CUAD-A4-100" {...props} />}
      </Field>
    </div>
  );
}

export function Formulario() {
  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 16, maxWidth: 360 }}>
      <Field label="Nombre" required>
        {(props) => <Input placeholder="Nombre completo" {...props} />}
      </Field>
      <Field label="Rol" hint="Define qué puede administrar esta cuenta">
        {(props) => <Input placeholder="Administrador" {...props} />}
      </Field>
      <Field label="Contraseña temporal" required error="Debe tener al menos 12 caracteres">
        {(props) => <Input type="password" {...props} />}
      </Field>
    </div>
  );
}
