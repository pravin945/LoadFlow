import { useEffect, useState } from 'react';
import { api } from '../api';
import { useAuth } from '../auth';
import type { Role, StaffMember } from '../types';

export default function StaffPage() {
  const { user } = useAuth();
  const [staff, setStaff] = useState<StaffMember[]>([]);
  const [roles, setRoles] = useState<Role[]>([]);
  const [permissions, setPermissions] = useState<{ key: string; description: string }[]>([]);
  const [tab, setTab] = useState<'staff' | 'roles'>('staff');
  const [showStaff, setShowStaff] = useState(false);
  const [showRole, setShowRole] = useState(false);
  const [staffForm, setStaffForm] = useState({ email: '', password: '', fullName: '', roleIds: [] as string[] });
  const [roleForm, setRoleForm] = useState({ name: '', description: '', permissions: [] as string[] });
  const [error, setError] = useState('');

  const refresh = () => {
    api.getStaff().then(setStaff).catch(console.error);
    api.getRoles().then(setRoles).catch(console.error);
  };

  useEffect(() => {
    refresh();
    api.getPermissions().then(setPermissions).catch(() => {});
  }, []);

  const handleCreateStaff = async (e: React.FormEvent) => {
    e.preventDefault();
    setError('');
    try {
      await api.createStaff(staffForm);
      setShowStaff(false);
      refresh();
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed');
    }
  };

  const handleCreateRole = async (e: React.FormEvent) => {
    e.preventDefault();
    setError('');
    try {
      await api.createRole(roleForm);
      setShowRole(false);
      refresh();
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed');
    }
  };

  const togglePerm = (p: string) => {
    setRoleForm(f => ({
      ...f,
      permissions: f.permissions.includes(p) ? f.permissions.filter(x => x !== p) : [...f.permissions, p]
    }));
  };

  return (
    <>
      <div className="page-header">
        <h1>Staff & Roles</h1>
        <p>Manage {user?.organizationName} team members and custom RBAC roles</p>
      </div>

      <div className="auth-tabs" style={{ maxWidth: 320, marginBottom: '1.5rem' }}>
        <button type="button" className={tab === 'staff' ? 'active' : ''} onClick={() => setTab('staff')}>Staff</button>
        <button type="button" className={tab === 'roles' ? 'active' : ''} onClick={() => setTab('roles')}>Roles</button>
      </div>

      {tab === 'staff' && (
        <>
          <button className="btn-primary" style={{ marginBottom: '1rem' }} onClick={() => setShowStaff(true)}>+ Add Staff</button>
          <div className="table-wrap">
            <table>
              <thead><tr><th>Name</th><th>Email</th><th>Roles</th><th>Admin</th></tr></thead>
              <tbody>
                {staff.map(s => (
                  <tr key={s.id}>
                    <td>{s.fullName}</td>
                    <td>{s.email}</td>
                    <td>{s.roleNames.join(', ') || '—'}</td>
                    <td>{s.isOrgAdmin ? 'Yes' : 'No'}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </>
      )}

      {tab === 'roles' && (
        <>
          <button className="btn-primary" style={{ marginBottom: '1rem' }} onClick={() => setShowRole(true)}>+ Create Role</button>
          <div className="table-wrap">
            <table>
              <thead><tr><th>Role</th><th>Description</th><th>Permissions</th></tr></thead>
              <tbody>
                {roles.map(r => (
                  <tr key={r.id}>
                    <td>{r.name}{r.isSystemAdminRole && ' (System)'}</td>
                    <td>{r.description || '—'}</td>
                    <td style={{ fontSize: '0.75rem' }}>{r.permissions.join(', ')}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </>
      )}

      {showStaff && (
        <div className="modal-overlay" onClick={() => setShowStaff(false)}>
          <div className="modal" onClick={e => e.stopPropagation()}>
            <h2>Add Staff Member</h2>
            {error && <div className="error-banner">{error}</div>}
            <form onSubmit={handleCreateStaff}>
              <div className="form-group"><label>Full Name</label><input value={staffForm.fullName} onChange={e => setStaffForm({ ...staffForm, fullName: e.target.value })} required /></div>
              <div className="form-group"><label>Email</label><input type="email" value={staffForm.email} onChange={e => setStaffForm({ ...staffForm, email: e.target.value })} required /></div>
              <div className="form-group"><label>Password</label><input type="password" value={staffForm.password} onChange={e => setStaffForm({ ...staffForm, password: e.target.value })} required /></div>
              <div className="form-group">
                <label>Roles</label>
                {roles.filter(r => !r.isSystemAdminRole).map(r => (
                  <label key={r.id} style={{ display: 'flex', gap: '0.5rem', marginBottom: '0.35rem', color: 'var(--text)' }}>
                    <input type="checkbox" checked={staffForm.roleIds.includes(r.id)}
                      onChange={e => setStaffForm({
                        ...staffForm,
                        roleIds: e.target.checked ? [...staffForm.roleIds, r.id] : staffForm.roleIds.filter(id => id !== r.id)
                      })} />
                    {r.name}
                  </label>
                ))}
              </div>
              <div className="modal-actions">
                <button type="button" className="btn-secondary" onClick={() => setShowStaff(false)}>Cancel</button>
                <button type="submit" className="btn-primary">Create</button>
              </div>
            </form>
          </div>
        </div>
      )}

      {showRole && (
        <div className="modal-overlay" onClick={() => setShowRole(false)}>
          <div className="modal" onClick={e => e.stopPropagation()} style={{ maxWidth: 560 }}>
            <h2>Create Custom Role</h2>
            {error && <div className="error-banner">{error}</div>}
            <form onSubmit={handleCreateRole}>
              <div className="form-group"><label>Role Name</label><input value={roleForm.name} onChange={e => setRoleForm({ ...roleForm, name: e.target.value })} required placeholder="e.g. Dispatcher" /></div>
              <div className="form-group"><label>Description</label><input value={roleForm.description} onChange={e => setRoleForm({ ...roleForm, description: e.target.value })} /></div>
              <div className="form-group">
                <label>Permissions (from catalog)</label>
                <div className="permission-grid">
                  {permissions.map(p => (
                    <label key={p.key}>
                      <input type="checkbox" checked={roleForm.permissions.includes(p.key)} onChange={() => togglePerm(p.key)} />
                      {p.key}
                    </label>
                  ))}
                </div>
              </div>
              <div className="modal-actions">
                <button type="button" className="btn-secondary" onClick={() => setShowRole(false)}>Cancel</button>
                <button type="submit" className="btn-primary">Create Role</button>
              </div>
            </form>
          </div>
        </div>
      )}
    </>
  );
}
