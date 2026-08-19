import { Toasts } from 'sillar-frontend';

export function Exito() {
  return <Toasts toasts={[{ id: 1, message: 'Producto guardado', tone: 'success' }]} />;
}

export function MezclaTonos() {
  return (
    <Toasts
      toasts={[
        { id: 1, message: 'Cambios guardados', tone: 'success' },
        { id: 2, message: 'No se pudo eliminar el archivo', tone: 'danger' },
      ]}
    />
  );
}
