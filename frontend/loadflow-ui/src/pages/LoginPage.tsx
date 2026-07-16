import { useState } from 'react';
import { useAuth } from '../auth';
import { api, setToken } from '../api';

type Tab = 'login' | 'register-broker' | 'register-carrier' | 'register-shipper';

const DEMO_ACCOUNTS = [
  { label: 'Broker Admin', email: 'broker.admin@loadflow.demo' },
  { label: 'Dispatcher', email: 'dispatcher@loadflow.demo' },
  { label: 'Carrier Admin', email: 'carrier.admin@loadflow.demo' },
  { label: 'Driver', email: 'driver@loadflow.demo' },
  { label: 'Shipper', email: 'shipper@loadflow.demo' }
];

export default function LoginPage() {
  const { login } = useAuth();
  const [tab, setTab] = useState<Tab>('login');
  const [error, setError] = useState('');
  const [loading, setLoading] = useState(false);
  const [form, setForm] = useState({
    email: '',
    password: 'Demo123!',
    fullName: '',
    organizationName: ''
  });

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError('');
    setLoading(true);
    try {
      if (tab === 'login') {
        await login(form.email, form.password);
      } else if (tab === 'register-broker') {
        const res = await api.registerOrg({
          email: form.email,
          password: form.password,
          fullName: form.fullName,
          organizationName: form.organizationName,
          accountType: 1
        });
        setToken(res.token);
        window.location.reload();
      } else if (tab === 'register-carrier') {
        const res = await api.registerOrg({
          email: form.email,
          password: form.password,
          fullName: form.fullName,
          organizationName: form.organizationName,
          accountType: 2
        });
        setToken(res.token);
        window.location.reload();
      } else {
        const res = await api.registerShipper({
          email: form.email,
          password: form.password,
          fullName: form.fullName
        });
        setToken(res.token);
        window.location.reload();
      }
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed');
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="auth-page">
      <div className="auth-card">
        <h1>Load<span style={{ color: 'var(--accent)' }}>Flow</span></h1>
        <p className="subtitle">Freight brokerage operations suite</p>

        <div className="auth-tabs">
          <button type="button" className={tab === 'login' ? 'active' : ''} onClick={() => setTab('login')}>Sign In</button>
          <button type="button" className={tab.startsWith('register') ? 'active' : ''} onClick={() => setTab('register-broker')}>Register</button>
        </div>

        {tab.startsWith('register') && (
          <div className="auth-tabs" style={{ marginBottom: '1rem' }}>
            <button type="button" className={tab === 'register-broker' ? 'active' : ''} onClick={() => setTab('register-broker')}>Broker</button>
            <button type="button" className={tab === 'register-carrier' ? 'active' : ''} onClick={() => setTab('register-carrier')}>Carrier</button>
            <button type="button" className={tab === 'register-shipper' ? 'active' : ''} onClick={() => setTab('register-shipper')}>Shipper</button>
          </div>
        )}

        {error && <div className="error-banner">{error}</div>}

        <form onSubmit={handleSubmit}>
          {tab !== 'login' && (
            <div className="form-group">
              <label>Full Name</label>
              <input value={form.fullName} onChange={e => setForm({ ...form, fullName: e.target.value })} required />
            </div>
          )}
          {tab.startsWith('register') && tab !== 'register-shipper' && (
            <div className="form-group">
              <label>Organization Name</label>
              <input value={form.organizationName} onChange={e => setForm({ ...form, organizationName: e.target.value })} required />
            </div>
          )}
          <div className="form-group">
            <label>Email</label>
            <input type="email" value={form.email} onChange={e => setForm({ ...form, email: e.target.value })} required />
          </div>
          <div className="form-group">
            <label>Password</label>
            <input type="password" value={form.password} onChange={e => setForm({ ...form, password: e.target.value })} required />
          </div>
          <button type="submit" className="btn-primary" style={{ width: '100%', marginTop: '0.5rem' }} disabled={loading}>
            {loading ? 'Please wait…' : tab === 'login' ? 'Sign In' : 'Create Account'}
          </button>
        </form>

        {tab === 'login' && (
          <div style={{ marginTop: '1.5rem', fontSize: '0.8rem', color: 'var(--muted)' }}>
            <p style={{ marginBottom: '0.5rem' }}>Demo accounts (password: Demo123!):</p>
            {DEMO_ACCOUNTS.map(a => (
              <button
                key={a.email}
                type="button"
                className="btn-secondary"
                style={{ margin: '0.25rem', fontSize: '0.75rem', padding: '0.35rem 0.6rem' }}
                onClick={() => setForm({ ...form, email: a.email })}
              >
                {a.label}
              </button>
            ))}
          </div>
        )}
      </div>
    </div>
  );
}
