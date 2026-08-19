import { ModuleCard } from 'sillar-frontend';

const nombres = new Map([
  ['core', 'CORE'],
  ['catalog', 'Catálogo'],
  ['sales', 'Ventas Online'],
  ['service_orders', 'Órdenes de Servicio'],
  ['tracking', 'Seguimiento'],
]);

export function Estados() {
  return (
    <div style={{ display: 'grid', gridTemplateColumns: 'repeat(2, 268px)', gap: 16 }}>
      <ModuleCard
        module={{
          code: 'core',
          displayName: 'CORE',
          description: 'Autenticación, usuarios, auditoría y medios.',
          version: '1.0.0',
          isCore: true,
          isActive: true,
          activatedAt: null,
          deactivatedAt: null,
          expiresAt: null,
          displayOrder: 0,
          hardDependencies: [],
          softDependencies: [],
          canActivate: false,
          canDeactivate: false,
          blockedBy: [],
          restartsAutomatically: true,
        }}
        displayNames={nombres}
        highlighted={false}
        busy={false}
        onToggle={() => {}}
        onJumpTo={() => {}}
      />

      <ModuleCard
        module={{
          code: 'catalog',
          displayName: 'Catálogo',
          description: 'Categorías, productos, imágenes y búsqueda.',
          version: '1.1.0',
          isCore: false,
          isActive: true,
          activatedAt: '2026-08-12T00:00:00Z',
          deactivatedAt: null,
          expiresAt: null,
          displayOrder: 1,
          hardDependencies: ['core'],
          softDependencies: [],
          canActivate: true,
          canDeactivate: true,
          blockedBy: [],
          restartsAutomatically: true,
        }}
        displayNames={nombres}
        highlighted={false}
        busy={false}
        onToggle={() => {}}
        onJumpTo={() => {}}
      />

      <ModuleCard
        module={{
          code: 'service_orders',
          displayName: 'Órdenes de Servicio',
          description: 'Registro de encargos: anillado, manualidades y trabajos personalizados.',
          version: '1.0.0',
          isCore: false,
          isActive: false,
          activatedAt: null,
          deactivatedAt: '2026-07-01T00:00:00Z',
          expiresAt: null,
          displayOrder: 5,
          hardDependencies: ['core'],
          softDependencies: [],
          canActivate: true,
          canDeactivate: false,
          blockedBy: [],
          restartsAutomatically: true,
        }}
        displayNames={nombres}
        highlighted={false}
        busy={false}
        onToggle={() => {}}
        onJumpTo={() => {}}
      />

      <ModuleCard
        module={{
          code: 'tracking',
          displayName: 'Seguimiento',
          description: 'Estados del trabajo y tablero para el cliente.',
          version: '1.0.0',
          isCore: false,
          isActive: false,
          activatedAt: null,
          deactivatedAt: null,
          expiresAt: null,
          displayOrder: 6,
          hardDependencies: ['core', 'service_orders'],
          softDependencies: [],
          canActivate: false,
          canDeactivate: false,
          blockedBy: ['service_orders'],
          restartsAutomatically: true,
        }}
        displayNames={nombres}
        highlighted={false}
        busy={false}
        onToggle={() => {}}
        onJumpTo={() => {}}
      />
    </div>
  );
}

export function Resaltada() {
  return (
    <div style={{ width: 268 }}>
      <ModuleCard
        module={{
          code: 'sales',
          displayName: 'Ventas Online',
          description: 'Carrito, pedidos y confirmación por WhatsApp.',
          version: '1.0.0',
          isCore: false,
          isActive: true,
          activatedAt: '2026-08-01T00:00:00Z',
          deactivatedAt: null,
          expiresAt: null,
          displayOrder: 2,
          hardDependencies: ['catalog'],
          softDependencies: [],
          canActivate: true,
          canDeactivate: true,
          blockedBy: [],
          restartsAutomatically: true,
        }}
        displayNames={nombres}
        highlighted={true}
        busy={false}
        onToggle={() => {}}
        onJumpTo={() => {}}
      />
    </div>
  );
}

export function ConVencimiento() {
  return (
    <div style={{ width: 268 }}>
      <ModuleCard
        module={{
          code: 'catalog',
          displayName: 'Catálogo',
          description: 'Categorías, productos, imágenes y búsqueda.',
          version: '1.1.0',
          isCore: false,
          isActive: true,
          activatedAt: '2026-08-12T00:00:00Z',
          deactivatedAt: null,
          expiresAt: '2027-08-12T00:00:00Z',
          displayOrder: 1,
          hardDependencies: ['core'],
          softDependencies: [],
          canActivate: true,
          canDeactivate: true,
          blockedBy: [],
          restartsAutomatically: true,
        }}
        displayNames={nombres}
        highlighted={false}
        busy={false}
        onToggle={() => {}}
        onJumpTo={() => {}}
      />
    </div>
  );
}
