import type { ClienteDeLaBandeja } from '../services/contratos';

/**
 * Quién es el cliente de una fila de la bandeja.
 *
 * **Puede no haberlo, y entonces se dice.** `ICustomerIdentityReader` devuelve
 * solo fichas activas: una de baja, bloqueada o inexistente no llega, y el
 * contrato no distingue el motivo a propósito. Así que la frase tampoco lo
 * afirma — dice lo que se sabe («no hay ficha activa») y qué mirar, en vez de
 * rellenar el hueco con el correo, con el identificador o con un nombre que
 * nadie escribió.
 */
export function Cliente({ cliente }: { cliente: ClienteDeLaBandeja | null }) {
  if (!cliente) {
    return (
      <span className="b2b-dato" title="El cliente no tiene ficha activa en Clientes: puede estar de baja o bloqueado.">
        Sin ficha activa
      </span>
    );
  }

  return (
    <span className="b2b-cliente">
      <span>{cliente.fullName}</span>
      <span className="b2b-cliente__contacto">{cliente.phone ?? cliente.email}</span>
    </span>
  );
}
