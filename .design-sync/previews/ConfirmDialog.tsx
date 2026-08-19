import { ConfirmDialog } from 'sillar-frontend';

export function Normal() {
  return (
    <ConfirmDialog
      open
      title="Activar módulo Catálogo"
      confirmLabel="Activar Catálogo"
      onConfirm={() => {}}
      onCancel={() => {}}
    >
      El módulo quedará disponible de inmediato para todos los administradores.
    </ConfirmDialog>
  );
}

export function Peligro() {
  return (
    <ConfirmDialog
      open
      title="Desactivar usuario"
      confirmLabel="Desactivar usuario"
      danger
      onConfirm={() => {}}
      onCancel={() => {}}
    >
      Se cerrarán sus sesiones abiertas de inmediato.
    </ConfirmDialog>
  );
}

export function Ocupado() {
  return (
    <ConfirmDialog
      open
      title="Desactivar usuario"
      confirmLabel="Desactivar usuario"
      danger
      busy
      onConfirm={() => {}}
      onCancel={() => {}}
    >
      Se cerrarán sus sesiones abiertas de inmediato.
    </ConfirmDialog>
  );
}
