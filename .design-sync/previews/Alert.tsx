import { Alert } from 'sillar-frontend';

export function Tonos() {
  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 12, maxWidth: 420 }}>
      <Alert tone="info" title="Información">
        El módulo Catálogo depende de CORE. Actívalo primero si aún no lo hiciste.
      </Alert>
      <Alert tone="success" title="Guardado">
        Los cambios del producto se guardaron correctamente.
      </Alert>
      <Alert tone="warning" title="Atención">
        Este producto no tiene fotos. La ficha pública se verá incompleta.
      </Alert>
      <Alert tone="danger" title="No se pudo guardar">
        Ya existe un producto con este código SKU en el catálogo.
      </Alert>
    </div>
  );
}

export function SoloTexto() {
  return (
    <div style={{ maxWidth: 420 }}>
      <Alert tone="info">Los precios se muestran sin impuestos incluidos.</Alert>
    </div>
  );
}
