import { Badge } from '../../../shared/ui';
import type { AdminModule } from '../services/modules';
import './modules.css';

/** Los cuatro estados que la tarjeta tiene que saber contar. */
export type ModuleState = 'core' | 'active' | 'inactive' | 'blocked';

/** Deduce el estado a partir de lo que dice el servidor. */
export function stateOf(module: AdminModule): ModuleState {
  if (module.isCore) {
    return 'core';
  }

  if (module.isActive) {
    return 'active';
  }

  return module.canActivate ? 'inactive' : 'blocked';
}

interface ModuleCardProps {
  module: AdminModule;
  /** Nombres visibles por código, para poder nombrar a quien bloquea. */
  displayNames: ReadonlyMap<string, string>;
  /** Resaltada porque se ha llegado desde otra tarjeta. */
  highlighted: boolean;
  busy: boolean;
  onToggle: (module: AdminModule) => void;
  onJumpTo: (code: string) => void;
}

/**
 * Tarjeta de módulo.
 *
 * No es una fila de configuración: es donde el negocio ve sus bloques, los que
 * tiene, los que podría tener y **por qué** uno está bloqueado.
 */
export function ModuleCard({
  module,
  displayNames,
  highlighted,
  busy,
  onToggle,
  onJumpTo,
}: ModuleCardProps) {
  const state = stateOf(module);

  const classes = [
    'mod',
    state === 'inactive' || state === 'blocked' ? 'is-off' : '',
    state === 'blocked' ? 'is-locked' : '',
    highlighted ? 'is-target' : '',
  ]
    .filter(Boolean)
    .join(' ');

  return (
    <article className={classes} id={`modulo-${module.code}`}>
      <div className="mod__top">
        <div>
          <div className="mod__code">{module.code}</div>
          <h2 className="mod__name">{module.displayName}</h2>
        </div>

        {/* CORE no lleva interruptor. No es uno deshabilitado: es que no lo
            tiene, porque desactivarlo no es una operación que exista. */}
        {state !== 'core' && (
          <button
            type="button"
            className="mod__switch"
            role="switch"
            aria-checked={module.isActive}
            disabled={busy || (!module.isActive && !module.canActivate) || (module.isActive && !module.canDeactivate)}
            onClick={() => onToggle(module)}
            aria-label={`${module.isActive ? 'Desactivar' : 'Activar'} ${module.displayName}`}
          />
        )}
      </div>

      <p className="mod__description">{module.description}</p>

      {/* Se muestra, pero no se evalúa: el control de vencimientos es de la
          fase 5, y fingir que ya funciona sería peor que no mostrarlo. */}
      {module.expiresAt && (
        <p className="mod__expiry">
          Licencia hasta el {new Date(module.expiresAt).toLocaleDateString('es-PE')}
        </p>
      )}

      <div className="mod__foot">
        <span className="mod__dep">
          <Dependencies module={module} displayNames={displayNames} onJumpTo={onJumpTo} />
        </span>

        <StateBadge state={state} />
      </div>
    </article>
  );
}

function StateBadge({ state }: { state: ModuleState }) {
  switch (state) {
    case 'core':
      return <Badge tone="neutral">Núcleo</Badge>;
    case 'active':
      return <Badge tone="success">Activo</Badge>;
    case 'blocked':
      return <Badge tone="warning">Bloqueado</Badge>;
    default:
      return <Badge tone="neutral">Inactivo</Badge>;
  }
}

/**
 * Qué requiere el módulo, o qué le falta.
 *
 * En el caso bloqueado **nombra** lo que falta y el nombre lleva a su tarjeta.
 * «Requisitos no cumplidos» obligaría a la persona a adivinar cuál.
 */
function Dependencies({
  module,
  displayNames,
  onJumpTo,
}: {
  module: AdminModule;
  displayNames: ReadonlyMap<string, string>;
  onJumpTo: (code: string) => void;
}) {
  const name = (code: string) => displayNames.get(code) ?? code;

  if (module.blockedBy.length > 0) {
    const verb = module.isActive ? 'Lo necesita' : 'Falta';

    return (
      <>
        {verb}{' '}
        {module.blockedBy.map((code, index) => (
          <span key={code}>
            {index > 0 && ', '}
            <button type="button" className="mod__jump" onClick={() => onJumpTo(code)}>
              {name(code)}
            </button>
          </span>
        ))}
      </>
    );
  }

  const hard = module.hardDependencies.filter((code) => code !== 'core');
  const soft = module.softDependencies;

  if (hard.length === 0 && soft.length === 0) {
    return <>Sin dependencias</>;
  }

  return (
    <>
      {hard.length > 0 && (
        <>
          Requiere <b>{hard.map(name).join(', ')}</b>
        </>
      )}
      {hard.length > 0 && soft.length > 0 && ' · '}
      {soft.length > 0 && (
        <>
          usa <b>{soft.map(name).join(', ')}</b>
        </>
      )}
    </>
  );
}
