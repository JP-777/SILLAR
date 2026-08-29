import { Link, Route } from 'react-router-dom';
import type { ModuleNavigation } from '../../layout/navigation';
import type { HomeSection } from '../../platform/homeSections';
import { EmptyState } from '../../shared/ui';
import { RequireRole } from '../../session';
import { CustomerEmailVerificationPage } from './pages/CustomerEmailVerificationPage';
import { CustomerInvitationPage } from './pages/CustomerInvitationPage';
import { CustomerLoginPage } from './pages/CustomerLoginPage';
import { CustomerPasswordResetConfirmPage } from './pages/CustomerPasswordResetConfirmPage';
import { CustomerPasswordResetRequestPage } from './pages/CustomerPasswordResetRequestPage';
import { CustomerRegisterPage } from './pages/CustomerRegisterPage';
import { CustomerProfilePage } from './pages/CustomerProfilePage';
import { AdminCustomerDetailPage } from './pages/AdminCustomerDetailPage';
import { AdminCustomersPage } from './pages/AdminCustomersPage';
import { ContactPage } from './pages/ContactPage';
import { AdminContactMessagesPage } from './pages/AdminContactMessagesPage';
import { AdminContactMessageDetailPage } from './pages/AdminContactMessageDetailPage';
import { RequireCustomerAuth, useCustomerSession } from './session';

export const crmNavigation: ModuleNavigation = {
  moduleCode: 'crm',
  group: 'Clientes',
  items: [
    {
      to: '/admin/clientes',
      label: 'Clientes',
      minimumRole: 'admin',
    },
    {
      to: '/admin/mensajes',
      label: 'Mensajes',
      minimumRole: 'admin',
    },
  ],
};

export const crmHome: HomeSection = {
  moduleCode: 'crm',
  Component: CrmHomeSection,
};

function CrmHomeSection() {
  const { isAuthenticated } = useCustomerSession();

  return (
    <EmptyState
      title={isAuthenticated ? 'Tu cuenta' : 'Cuenta de cliente'}
      description={
        isAuthenticated
          ? 'Revisa tus datos y direcciones de entrega.'
          : 'Entra o crea una cuenta para guardar tus datos de compra.'
      }
      action={
        <div className="crm-home-actions">
          <Link to={isAuthenticated ? '/mi-cuenta' : '/entrar'}>
            {isAuthenticated ? 'Ir a mi cuenta' : 'Entrar'}
          </Link>
          <Link to="/contacto">Contacto</Link>
        </div>
      }
    />
  );
}

export const crmAdminRoutes = (
  <Route element={<RequireRole minimum="admin" />}>
    <Route path="clientes" element={<AdminCustomersPage />} />
    <Route path="clientes/:customerId" element={<AdminCustomerDetailPage />} />
    <Route path="mensajes" element={<AdminContactMessagesPage />} />
    <Route
      path="mensajes/:contactMessageId"
      element={<AdminContactMessageDetailPage />}
    />
  </Route>
);

export const crmPublicRoutes = (
  <>
    <Route path="/entrar" element={<CustomerLoginPage />} />
    <Route path="/crear-cuenta" element={<CustomerRegisterPage />} />
    <Route
      path="/recuperar-contrasena"
      element={<CustomerPasswordResetRequestPage />}
    />
    <Route
      path="/restablecer-contrasena"
      element={<CustomerPasswordResetConfirmPage />}
    />
    <Route
      path="/verificar-correo"
      element={<CustomerEmailVerificationPage />}
    />
    <Route path="/activar-cuenta" element={<CustomerInvitationPage />} />
    <Route path="/contacto" element={<ContactPage />} />

    <Route element={<RequireCustomerAuth />}>
      <Route path="/mi-cuenta" element={<CustomerProfilePage />} />
    </Route>
  </>
);
