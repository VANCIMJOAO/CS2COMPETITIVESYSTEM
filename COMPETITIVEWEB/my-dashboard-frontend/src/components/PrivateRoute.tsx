import React from 'react';
import { Navigate } from 'react-router-dom';

interface PrivateRouteProps {
  component: React.FC;
}

const PrivateRoute: React.FC<PrivateRouteProps> = ({ component: Component }) => {
  const isAuthenticated = !!localStorage.getItem('token');

  return isAuthenticated ? <Component /> : <Navigate to="/login" replace />;
};

export default PrivateRoute;
