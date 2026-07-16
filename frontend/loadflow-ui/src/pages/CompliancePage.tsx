import { useEffect, useState } from 'react';
import { api } from '../api';
import type { ComplianceRecord } from '../types';

export default function CompliancePage() {
  const [record, setRecord] = useState<ComplianceRecord | null>(null);
  const [form, setForm] = useState({
    insuranceExpiry: '',
    mcAuthorityStatus: 'Active',
    dotAuthorityStatus: 'Active',
    approvedEquipmentTypes: '["Dry Van","Reefer","Flatbed"]',
    approvedCommodityTypes: '["General Freight","Food Grade","Electronics"]'
  });
  const [error, setError] = useState('');
  const [saved, setSaved] = useState(false);

  useEffect(() => {
    api.getCompliance().then(r => {
      setRecord(r);
      setForm({
        insuranceExpiry: r.insuranceExpiry.split('T')[0],
        mcAuthorityStatus: r.mcAuthorityStatus,
        dotAuthorityStatus: r.dotAuthorityStatus,
        approvedEquipmentTypes: r.approvedEquipmentTypes,
        approvedCommodityTypes: r.approvedCommodityTypes
      });
    }).catch(() => {});
  }, []);

  const handleSave = async (e: React.FormEvent) => {
    e.preventDefault();
    setError('');
    setSaved(false);
    try {
      const updated = await api.upsertCompliance({
        carrierOrganizationId: null,
        insuranceExpiry: new Date(form.insuranceExpiry).toISOString(),
        mcAuthorityStatus: form.mcAuthorityStatus === 'Active' ? 1 : form.mcAuthorityStatus === 'Inactive' ? 2 : 3,
        dotAuthorityStatus: form.dotAuthorityStatus === 'Active' ? 1 : form.dotAuthorityStatus === 'Inactive' ? 2 : 3,
        approvedEquipmentTypes: form.approvedEquipmentTypes,
        approvedCommodityTypes: form.approvedCommodityTypes
      });
      setRecord(updated);
      setSaved(true);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed');
    }
  };

  return (
    <>
      <div className="page-header">
        <h1>Carrier Compliance</h1>
        <p>Insurance, authority status, and approved equipment/commodity types</p>
      </div>

      {error && <div className="error-banner">{error}</div>}
      {saved && <div className="card" style={{ marginBottom: '1rem', borderColor: 'var(--success)', color: 'var(--success)' }}>Compliance record saved.</div>}

      <div className="card" style={{ maxWidth: 560 }}>
        <form onSubmit={handleSave}>
          <div className="form-group">
            <label>Insurance Expiry</label>
            <input type="date" value={form.insuranceExpiry} onChange={e => setForm({ ...form, insuranceExpiry: e.target.value })} required />
          </div>
          <div className="form-group">
            <label>MC Authority Status</label>
            <select value={form.mcAuthorityStatus} onChange={e => setForm({ ...form, mcAuthorityStatus: e.target.value })}>
              {['Active','Inactive','Pending'].map(s => <option key={s}>{s}</option>)}
            </select>
          </div>
          <div className="form-group">
            <label>DOT Authority Status</label>
            <select value={form.dotAuthorityStatus} onChange={e => setForm({ ...form, dotAuthorityStatus: e.target.value })}>
              {['Active','Inactive','Pending'].map(s => <option key={s}>{s}</option>)}
            </select>
          </div>
          <div className="form-group">
            <label>Approved Equipment (JSON array)</label>
            <input value={form.approvedEquipmentTypes} onChange={e => setForm({ ...form, approvedEquipmentTypes: e.target.value })} />
          </div>
          <div className="form-group">
            <label>Approved Commodities (JSON array)</label>
            <input value={form.approvedCommodityTypes} onChange={e => setForm({ ...form, approvedCommodityTypes: e.target.value })} />
          </div>
          {record && <p style={{ fontSize: '0.8rem', color: 'var(--muted)', marginBottom: '1rem' }}>Last updated: {new Date(record.updatedAt).toLocaleString()}</p>}
          <button type="submit" className="btn-primary">Save Compliance Record</button>
        </form>
      </div>
    </>
  );
}
