import { FailureAlert } from 'sillar-frontend';

export function Red() {
  return (
    <div style={{ maxWidth: 420 }}>
      <FailureAlert
        failure={{
          kind: 'inline',
          message: 'No se pudo guardar el producto. Vuelve a intentarlo.',
          fieldErrors: null,
          blockedBy: null,
        }}
      />
    </div>
  );
}

export function Validacion() {
  return (
    <div style={{ maxWidth: 420 }}>
      <FailureAlert
        failure={{
          kind: 'validation',
          message: 'Ya existe un usuario con este correo.',
          fieldErrors: { email: 'Ya existe un usuario con este correo.' },
          blockedBy: null,
        }}
      />
    </div>
  );
}
