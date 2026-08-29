import { Link, Route } from 'react-router-dom';
import type { HomeSection } from '../../platform/homeSections';
import { EmptyState } from '../../shared/ui';
import { CustomerEmailVerificationPage } from './pages/CustomerEmailVerificationPage';
import { CustomerInvitationPage } from './pages/CustomerInvitationPage';
import { CustomerLoginPage } from './pages/CustomerLoginPage';
import { CustomerPasswordResetConfirmPage } from './pages/CustomerPasswordResetConfirmPage';
import { CustomerPasswordResetRequestPage } from './pages/CustomerPasswordResetRequestPage';
import { CustomerRegisterPage } from './pages/CustomerRegisterPage';
import { CustomerProfilePage } from './pages/CustomerProfilePage';
import { RequireCustomerAuth, useCustomerSession } from './session';

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
        <Link to={isAuthenticated ? '/mi-cuenta' : '/entrar'}>
          {isAuthenticated ? 'Ir a mi cuenta' : 'Entrar'}
        </Link>
      }
    />
  );
}

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

    <Route element={<RequireCustomerAuth />}>
      <Route path="/mi-cuenta" element={<CustomerProfilePage />} />
    </Route>
  </>
);
