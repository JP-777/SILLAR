import { Route } from 'react-router-dom';
import { CustomerEmailVerificationPage } from './pages/CustomerEmailVerificationPage';
import { CustomerInvitationPage } from './pages/CustomerInvitationPage';
import { CustomerLoginPage } from './pages/CustomerLoginPage';
import { CustomerPasswordResetConfirmPage } from './pages/CustomerPasswordResetConfirmPage';
import { CustomerPasswordResetRequestPage } from './pages/CustomerPasswordResetRequestPage';
import { CustomerRegisterPage } from './pages/CustomerRegisterPage';

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
  </>
);
