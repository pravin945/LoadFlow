import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { api } from '../api';
import { useAuth } from '../auth';
import { hasPermission, PERMISSIONS, type Load } from '../types';

export default function LoadsPage() {
  const { user } = useAuth();
  const [loads, setLoads] = useState<Load[]>([]);
  const [filters, setFilters] = useState({ search: '', status: '', complianceFlagged: false });
  const [showCreate, setShowCreate] = useState(false);
  const [shippers, setShippers] = useState<{ id: string; fullName: string }[]>([]);
  const [form, setForm] = useState({
    shipperUserId: '', origin: '', destination: '', equipmentType: 'Dry Van',
    commodityType: 'General Freight', weightLbs: 40000,
    pickupDate: '', deliveryDate: ''
  });
  const [error, setError] = useState('');

  const loadData = () => {
    const params: Record<string, string> = {};
    if (filters.search) params.search = filters.search;
    if (filters.status) params.status = filters.status;
    if (filters.complianceFlagged) params.complianceFlagged = 'true';
    api.getLoads(params).then(setLoads).catch(console.error);
  };

  useEffect(() => { loadData(); }, [filters]);
  useEffect(() => {
    if (hasPermission(user, PERMISSIONS.loadCreate)) {
      api.getShippers().then(s => setShippers(s)).catch(() => {});
    }
  }, [user]);

  const handleCreate = async (e: React.FormEvent) => {
    e.preventDefault();
    setError('');
    try {
      await api.createLoad(form);
      setShowCreate(false);
      loadData();
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed');
    }
  };

  return (
    <>
      <div className="page-header" style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start' }}>
        <div>
          <h1>Load Board</h1>
          <p>Search, filter, and manage freight loads</p>
        </div>
        {hasPermission(user, PERMISSIONS.loadCreate) && (
          <button className="btn-primary" onClick={() => setShowCreate(true)}>+ New Load</button>
        )}
      </div>

      <div className="filters">
        <div className="form-group">
          <label>Search</label>
          <input placeholder="Ref, origin, destination…" value={filters.search}
            onChange={e => setFilters({ ...filters, search: e.target.value })} />
        </div>
        <div className="form-group">
          <label>Status</label>
          <select value={filters.status} onChange={e => setFilters({ ...filters, status: e.target.value })}>
            <option value="">All</option>
            {['Posted','CarrierAssigned','RateConfirmed','Dispatched','InTransit','Delivered','PodVerified','InvoicedClosed'].map(s =>
              <option key={s} value={s}>{s.replace(/([A-Z])/g, ' $1').trim()}</option>)}
          </select>
        </div>
        {user?.accountType === 'Broker' && (
          <div className="form-group">
            <label>&nbsp;</label>
            <label style={{ display: 'flex', alignItems: 'center', gap: '0.5rem', color: 'var(--text)' }}>
              <input type="checkbox" checked={filters.complianceFlagged}
                onChange={e => setFilters({ ...filters, complianceFlagged: e.target.checked })} />
              Flagged only
            </label>
          </div>
        )}
      </div>

      <div className="table-wrap">
        <table>
          <thead>
            <tr>
              <th>Reference</th><th>Route</th><th>Equipment</th><th>Status</th>
              {user?.accountType === 'Broker' && <th>Carrier</th>}
              <th>Compliance</th><th>Pickup</th>
            </tr>
          </thead>
          <tbody>
            {loads.map(l => (
              <tr key={l.id}>
                <td><Link to={`/loads/${l.id}`}>{l.referenceNumber}</Link></td>
                <td>{l.origin} → {l.destination}</td>
                <td>{l.equipmentType}</td>
                <td>{l.status.replace(/([A-Z])/g, ' $1').trim()}</td>
                {user?.accountType === 'Broker' && <td>{l.carrierName || '—'}</td>}
                <td>{l.complianceFlag === 'Flagged' ? <span className="badge badge-flagged">Flagged</span> : l.complianceFlag}</td>
                <td>{new Date(l.pickupDate).toLocaleDateString()}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      {showCreate && (
        <div className="modal-overlay" onClick={() => setShowCreate(false)}>
          <div className="modal" onClick={e => e.stopPropagation()}>
            <h2>Create Load</h2>
            {error && <div className="error-banner">{error}</div>}
            <form onSubmit={handleCreate}>
              <div className="form-group">
                <label>Shipper</label>
                <select value={form.shipperUserId} onChange={e => setForm({ ...form, shipperUserId: e.target.value })} required>
                  <option value="">Select shipper…</option>
                  {shippers.map(s => <option key={s.id} value={s.id}>{s.fullName}</option>)}
                </select>
              </div>
              <div className="form-group"><label>Origin</label><input value={form.origin} onChange={e => setForm({ ...form, origin: e.target.value })} required /></div>
              <div className="form-group"><label>Destination</label><input value={form.destination} onChange={e => setForm({ ...form, destination: e.target.value })} required /></div>
              <div className="form-group"><label>Equipment</label>
                <select value={form.equipmentType} onChange={e => setForm({ ...form, equipmentType: e.target.value })}>
                  {['Dry Van','Reefer','Flatbed'].map(t => <option key={t}>{t}</option>)}
                </select>
              </div>
              <div className="form-group"><label>Commodity</label>
                <select value={form.commodityType} onChange={e => setForm({ ...form, commodityType: e.target.value })}>
                  {['General Freight','Food Grade','Electronics'].map(t => <option key={t}>{t}</option>)}
                </select>
              </div>
              <div className="form-group"><label>Weight (lbs)</label><input type="number" value={form.weightLbs} onChange={e => setForm({ ...form, weightLbs: +e.target.value })} /></div>
              <div className="form-group"><label>Pickup Date</label><input type="date" value={form.pickupDate} onChange={e => setForm({ ...form, pickupDate: e.target.value })} required /></div>
              <div className="form-group"><label>Delivery Date</label><input type="date" value={form.deliveryDate} onChange={e => setForm({ ...form, deliveryDate: e.target.value })} required /></div>
              <div className="modal-actions">
                <button type="button" className="btn-secondary" onClick={() => setShowCreate(false)}>Cancel</button>
                <button type="submit" className="btn-primary">Create Load</button>
              </div>
            </form>
          </div>
        </div>
      )}
    </>
  );
}
