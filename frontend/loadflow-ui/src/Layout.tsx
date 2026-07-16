import { NavLink, Outlet } from 'react-router-dom';
import { useAuth } from './auth';
import { hasPermission, PERMISSIONS } from './types';

export default function Layout() {
  const { user, logout } = useAuth();

  return (
    <div className="layout">
      <aside className="sidebar">
        <div className="sidebar-brand">Load<span>Flow</span></div>
        <nav>
          <NavLink to="/" end>Dashboard</NavLink>
          <NavLink to="/loads">Loads</NavLink>
          {hasPermission(user, PERMISSIONS.staffManage) && (
            <NavLink to="/staff">Staff & Roles</NavLink>
          )}
          {hasPermission(user, PERMISSIONS.complianceManage) && user?.accountType === 'Carrier' && (
            <NavLink to="/compliance">Compliance</NavLink>
          )}
        </nav>
        <div className="user-chip">
          <div>{user?.fullName}</div>
          <div className="email">{user?.email}</div>
          <div className="roles">{user?.accountType} · {user?.roleNames.join(', ') || 'Shipper'}</div>
          <button className="btn-secondary" style={{ marginTop: '0.75rem', width: '100%' }} onClick={logout}>Sign Out</button>
        </div>
      </aside>
      <main className="main">
        <Outlet />
      </main>
    </div>
  );
}
