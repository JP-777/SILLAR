import { Navigate, Outlet, useLocation } from 'react-router-dom';
import { useCustomerSession } from './useCustomerSession';

export function RequireCustomerAuth() {
  const { isAuthenticated } = useCustomerSession();
  const location = useLocation();

  if (!isAuthenticated) {
    return (
      <Navigate
        to="/entrar"
        replace
        state={{ from: location.pathname }}
      />
    );
  }

  return <Outlet />;
}
