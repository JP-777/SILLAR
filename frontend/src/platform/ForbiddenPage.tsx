import { Link } from 'react-router-dom';
import { EmptyState } from '../shared/ui';
import type { Role } from '../session/SessionProvider';

const ROLE_NAMES: Record<Role, string> = {
  editor: 'editor',
  admin: 'administrador',
  super_admin: 'administrador principal',
};

/**
 * Sin el rol necesario.
 *
 * Una pantalla que lo explica, no un menú roto: quien llega aquí suele haber
 * seguido un enlace legítimo y merece saber por qué no puede pasar.
 */
export function ForbiddenPage({ minimum }: { minimum: Role }) {
  return (
    <EmptyState
      title="No tienes acceso a esta sección"
      description={`Hace falta el rol de ${ROLE_NAMES[minimum]}. Si crees que deberías tenerlo, pídeselo a quien administre el sistema.`}
      action={<Link to="/admin">Volver al inicio</Link>}
    />
  );
}
