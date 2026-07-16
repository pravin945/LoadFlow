import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { api } from '../api';
import { useAuth } from '../auth';
import type { DashboardAlerts, Load } from '../types';

export default function DashboardPage() {
  const { user } = useAuth();
  const [alerts, setAlerts] = useState<DashboardAlerts | null>(null);
  const [loads, setLoads] = useState<Load[]>([]);

  useEffect(() => {
    api.getDashboardAlerts().then(setAlerts).catch(() => {});
    api.getLoads().then(setLoads).catch(() => {});
  }, []);

  const title = user?.accountType === 'Broker' ? 'Broker Operations'
    : user?.accountType === 'Carrier' ? 'Carrier Dashboard'
    : 'My Shipments';

  return (
    <>
      <div className="page-header">
        <h1>{title}</h1>
        <p>{user?.organizationName || user?.fullName} — {user?.roleNames.join(', ') || 'Shipper'}</p>
      </div>

      <div className="stats-grid">
        <div className="stat-card">
          <div className="value">{alerts?.activeLoads ?? loads.length}</div>
          <div className="label">Active Loads</div>
        </div>
        {user?.accountType === 'Broker' && (
          <div className="stat-card danger">
            <div className="value">{alerts?.complianceFlaggedLoads ?? 0}</div>
            <div className="label">Compliance Flagged</div>
          </div>
        )}
        {user?.accountType === 'Broker' && (
          <div className="stat-card">
            <div className="value">{alerts?.complianceAlerts.length ?? 0}</div>
            <div className="label">Expiry Alerts</div>
          </div>
        )}
      </div>

      {user?.accountType === 'Broker' && alerts && alerts.complianceAlerts.length > 0 && (
        <div className="card" style={{ marginBottom: '1.5rem' }}>
          <h3 style={{ marginBottom: '0.75rem' }}>Compliance Expiry Alerts</h3>
          <div className="table-wrap">
            <table>
              <thead>
                <tr><th>Carrier</th><th>Insurance Expiry</th><th>MC</th><th>DOT</th><th>Alert</th></tr>
              </thead>
              <tbody>
                {alerts.complianceAlerts.map(a => (
                  <tr key={a.carrierOrganizationId}>
                    <td>{a.carrierName}</td>
                    <td>{new Date(a.insuranceExpiry).toLocaleDateString()}</td>
                    <td>{a.mcAuthorityStatus}</td>
                    <td>{a.dotAuthorityStatus}</td>
                    <td><span className="badge badge-flagged">{a.alertType}</span></td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </div>
      )}

      <div className="card">
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '1rem' }}>
          <h3>Recent Loads</h3>
          <Link to="/loads" className="btn-secondary" style={{ padding: '0.4rem 0.8rem' }}>View All</Link>
        </div>
        <div className="table-wrap">
          <table>
            <thead>
              <tr>
                <th>Ref #</th><th>Route</th><th>Status</th>
                {user?.accountType !== 'Shipper' && <th>Shipper</th>}
                {user?.accountType === 'Broker' && <th>Carrier</th>}
                <th>Compliance</th>
              </tr>
            </thead>
            <tbody>
              {loads.slice(0, 8).map(l => (
                <tr key={l.id}>
                  <td><Link to={`/loads/${l.id}`}>{l.referenceNumber}</Link></td>
                  <td>{l.origin} → {l.destination}</td>
                  <td>{l.status.replace(/([A-Z])/g, ' $1').trim()}</td>
                  {user?.accountType !== 'Shipper' && <td>{l.shipperName}</td>}
                  {user?.accountType === 'Broker' && <td>{l.carrierName || '—'}</td>}
                  <td>{l.complianceFlag === 'Flagged' ? <span className="badge badge-flagged">Flagged</span> : 'OK'}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </div>
    </>
  );
}
