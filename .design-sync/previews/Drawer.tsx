import { Button, Drawer, Field, Input } from 'sillar-frontend';

export function Basico() {
  return (
    <Drawer open title="Nuevo usuario administrador" onClose={() => {}}>
      <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
        <Field label="Nombre" required>
          {(props) => <Input placeholder="Nombre completo" {...props} />}
        </Field>
        <Field label="Correo electrónico" required>
          {(props) => <Input type="email" placeholder="admin@sillar.pe" {...props} />}
        </Field>
      </div>
    </Drawer>
  );
}

export function ConFooter() {
  return (
    <Drawer
      open
      title="Editar producto"
      description="Los cambios se ven en el catálogo público al guardar."
      onClose={() => {}}
      footer={
        <div style={{ display: 'flex', justifyContent: 'flex-end', gap: 8 }}>
          <Button variant="secondary">Cancelar</Button>
          <Button>Guardar cambios</Button>
        </div>
      }
    >
      <Field label="Nombre del producto">
        {(props) => <Input defaultValue="Cuaderno universitario cuadriculado Stanford A4 100 hojas" {...props} />}
      </Field>
    </Drawer>
  );
}
