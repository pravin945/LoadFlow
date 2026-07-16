import { useEffect, useState } from 'react';
import { useParams } from 'react-router-dom';
import { api } from '../api';
import { useAuth } from '../auth';
import { hasPermission, PERMISSIONS, type Load } from '../types';

const NEXT_STATUS: Record<string, string> = {
  RateConfirmed: 'Dispatched',
  Dispatched: 'InTransit',
  InTransit: 'Delivered',
  Delivered: 'PodVerified',
  PodVerified: 'InvoicedClosed'
};

export default function LoadDetailPage() {
  const { id } = useParams<{ id: string }>();
  const { user } = useAuth();
  const [load, setLoad] = useState<Load | null>(null);
  const [carriers, setCarriers] = useState<{ id: string; name: string }[]>([]);
  const [selectedCarrier, setSelectedCarrier] = useState('');
  const [rateAmount, setRateAmount] = useState('2500');
  const [error, setError] = useState('');
  const [podFile, setPodFile] = useState<File | null>(null);

  const refresh = () => id && api.getLoad(id).then(setLoad).catch(() => setLoad(null));

  useEffect(() => { refresh(); }, [id]);
  useEffect(() => {
    if (hasPermission(user, PERMISSIONS.loadAssign)) api.getCarriers().then(setCarriers).catch(() => {});
  }, [user]);

  const act = async (fn: () => Promise<Load>) => {
    setError('');
    try { await fn(); refresh(); }
    catch (err) { setError(err instanceof Error ? err.message : 'Action failed'); }
  };

  if (!load) return <p>Loading…</p>;

  const nextStatus = NEXT_STATUS[load.status];
  const canAdvance = nextStatus && (
    (user?.accountType === 'Carrier' && ['RateConfirmed', 'Dispatched', 'InTransit'].includes(load.status)) ||
    (user?.accountType === 'Broker' && ['RateConfirmed', 'Delivered', 'PodVerified'].includes(load.status))
  );

  return (
    <>
      <div className="page-header">
        <h1>{load.referenceNumber}</h1>
        <p>{load.origin} → {load.destination} · {load.status.replace(/([A-Z])/g, ' $1').trim()}</p>
      </div>

      {error && <div className="error-banner">{error}</div>}

      {load.complianceFlag === 'Flagged' && (
        <div className="error-banner" style={{ background: '#422006', borderColor: 'var(--warning)', color: '#fcd34d' }}>
          Compliance Flag: {load.complianceFlagReason}
          {hasPermission(user, PERMISSIONS.loadOverride) && (
            <button className="btn-secondary" style={{ marginLeft: '1rem' }}
              onClick={() => act(() => api.overrideCompliance(load.id, 'Ops lead override'))}>
              Override Flag
            </button>
          )}
        </div>
      )}

      <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '1rem', marginBottom: '1.5rem' }}>
        <div className="card">
          <h3 style={{ marginBottom: '0.75rem' }}>Load Details</h3>
          <p><strong>Shipper:</strong> {load.shipperName}</p>
          <p><strong>Broker:</strong> {load.brokerName}</p>
          <p><strong>Carrier:</strong> {load.carrierName || 'Unassigned'}</p>
          <p><strong>Equipment:</strong> {load.equipmentType} · {load.commodityType}</p>
          <p><strong>Weight:</strong> {load.weightLbs.toLocaleString()} lbs</p>
          <p><strong>Pickup:</strong> {new Date(load.pickupDate).toLocaleDateString()}</p>
          <p><strong>Delivery:</strong> {new Date(load.deliveryDate).toLocaleDateString()}</p>
          {load.activeRate && (
            <p style={{ marginTop: '0.5rem' }}><strong>Rate v{load.activeRate.versionNumber}:</strong> ${load.activeRate.baseRate.toLocaleString()}</p>
          )}
        </div>

        <div className="card">
          <h3 style={{ marginBottom: '0.75rem' }}>Actions</h3>

          {hasPermission(user, PERMISSIONS.loadAssign) && load.status === 'Posted' && (
            <div style={{ marginBottom: '1rem' }}>
              <div className="form-group">
                <label>Assign Carrier</label>
                <select value={selectedCarrier} onChange={e => setSelectedCarrier(e.target.value)}>
                  <option value="">Select carrier…</option>
                  {carriers.map(c => <option key={c.id} value={c.id}>{c.name}</option>)}
                </select>
              </div>
              <button className="btn-primary" disabled={!selectedCarrier}
                onClick={() => act(() => api.assignCarrier(load.id, selectedCarrier))}>
                Assign Carrier
              </button>
            </div>
          )}

          {hasPermission(user, PERMISSIONS.rateConfirm) && ['CarrierAssigned', 'RateConfirmed'].includes(load.status) && load.complianceFlag !== 'Flagged' && (
            <div style={{ marginBottom: '1rem' }}>
              <div className="form-group">
                <label>Base Rate ($)</label>
                <input type="number" value={rateAmount} onChange={e => setRateAmount(e.target.value)} />
              </div>
              <button className="btn-primary"
                onClick={() => act(() => api.confirmRate(load.id, { baseRate: +rateAmount, accessorials: [], notes: '' }))}>
                {load.activeRate ? 'Confirm New Rate Version' : 'Confirm Rate'}
              </button>
            </div>
          )}

          {hasPermission(user, PERMISSIONS.loadUpdate) && user?.accountType === 'Carrier' && load.status === 'CarrierAssigned' && (
            <div style={{ display: 'flex', gap: '0.5rem', marginBottom: '0.5rem' }}>
              <button className="btn-primary"
                onClick={() => act(() => api.acceptLoad(load.id))}>Accept Assignment</button>
              <button className="btn-secondary"
                onClick={() => act(() => api.declineLoad(load.id, 'Carrier declined assignment'))}>Decline</button>
            </div>
          )}

          {hasPermission(user, PERMISSIONS.loadUpdate) && canAdvance && (
            <button className="btn-secondary"
              onClick={() => act(() => api.transition(load.id, nextStatus))}>
              Advance to {nextStatus.replace(/([A-Z])/g, ' $1').trim()}
            </button>
          )}

          {hasPermission(user, PERMISSIONS.podUpload) && ['Delivered','PodVerified'].includes(load.status) && (
            <div style={{ marginTop: '1rem' }}>
              <div className="form-group">
                <label>Upload POD</label>
                <input type="file" accept="image/*,.pdf" onChange={e => setPodFile(e.target.files?.[0] || null)} />
              </div>
              <button className="btn-primary" disabled={!podFile}
                onClick={() => podFile && act(async () => { await api.uploadPod(load.id, podFile); return load; })}>
                Upload POD
              </button>
            </div>
          )}
        </div>
      </div>

      {load.pods.length > 0 && (
        <div className="card" style={{ marginBottom: '1.5rem' }}>
          <h3 style={{ marginBottom: '0.75rem' }}>Proof of Delivery</h3>
          {load.pods.map(p => (
            <p key={p.id}>
              <button className="btn-secondary" onClick={() => api.downloadPod(load.id, p.id, p.fileName)}>
                View / Download {p.fileName}
              </button>
              {' — '}{new Date(p.uploadedAt).toLocaleString()}
            </p>
          ))}
        </div>
      )}

      <div className="card">
        <h3 style={{ marginBottom: '0.75rem' }}>Audit Trail</h3>
        <ul className="audit-list">
          {load.auditTrail.map(a => (
            <li key={a.id}>
              <span className="time">{new Date(a.timestamp).toLocaleString()}</span>
              {' · '}<strong>{a.action}</strong> by {a.userEmail}
              {a.fromStatus && a.toStatus && ` (${a.fromStatus} → ${a.toStatus})`}
              {a.details && <div style={{ color: 'var(--muted)' }}>{a.details}</div>}
            </li>
          ))}
        </ul>
      </div>
    </>
  );
}
