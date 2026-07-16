const API_BASE = '/api';

function getToken(): string | null {
  return localStorage.getItem('loadflow_token');
}

export function setToken(token: string | null) {
  if (token) localStorage.setItem('loadflow_token', token);
  else localStorage.removeItem('loadflow_token');
}

async function request<T>(path: string, options: RequestInit = {}): Promise<T> {
  const headers: Record<string, string> = {
    ...(options.headers as Record<string, string> || {})
  };
  if (!(options.body instanceof FormData)) {
    headers['Content-Type'] = 'application/json';
  }
  const token = getToken();
  if (token) headers['Authorization'] = `Bearer ${token}`;

  const res = await fetch(`${API_BASE}${path}`, { ...options, headers });
  if (!res.ok) {
    const err = await res.json().catch(() => ({ message: res.statusText }));
    throw new Error(err.message || 'Request failed');
  }
  if (res.status === 204) return undefined as T;
  return res.json();
}

export const api = {
  login: (email: string, password: string) =>
    request<{ token: string; user: import('./types').UserProfile }>('/auth/login', {
      method: 'POST',
      body: JSON.stringify({ email, password })
    }),

  me: () => request<import('./types').UserProfile>('/auth/me'),

  registerOrg: (data: object) =>
    request<{ token: string; user: import('./types').UserProfile }>('/auth/register/org', {
      method: 'POST',
      body: JSON.stringify(data)
    }),

  registerShipper: (data: object) =>
    request<{ token: string; user: import('./types').UserProfile }>('/auth/register/shipper', {
      method: 'POST',
      body: JSON.stringify(data)
    }),

  getPermissions: () => request<{ key: string; description: string }[]>('/auth/permissions'),

  getLoads: (params?: Record<string, string>) => {
    const qs = params ? '?' + new URLSearchParams(params).toString() : '';
    return request<import('./types').Load[]>(`/loads${qs}`);
  },

  getLoad: (id: string) => request<import('./types').Load>(`/loads/${id}`),

  createLoad: (data: object) =>
    request<import('./types').Load>('/loads', { method: 'POST', body: JSON.stringify(data) }),

  assignCarrier: (id: string, carrierOrganizationId: string) =>
    request<import('./types').Load>(`/loads/${id}/assign-carrier`, {
      method: 'POST',
      body: JSON.stringify({ carrierOrganizationId })
    }),

  overrideCompliance: (id: string, notes?: string) =>
    request<import('./types').Load>(`/loads/${id}/override-compliance`, {
      method: 'POST',
      body: JSON.stringify({ notes })
    }),

  confirmRate: (id: string, data: object) =>
    request<import('./types').Load>(`/loads/${id}/confirm-rate`, {
      method: 'POST',
      body: JSON.stringify(data)
    }),

  transition: (id: string, targetStatus: string, notes?: string) =>
    request<import('./types').Load>(`/loads/${id}/transition`, {
      method: 'POST',
      body: JSON.stringify({ targetStatus, notes })
    }),

  acceptLoad: (id: string) =>
    request<import('./types').Load>(`/loads/${id}/accept`, { method: 'POST' }),

  declineLoad: (id: string, notes?: string) =>
    request<import('./types').Load>(`/loads/${id}/decline`, {
      method: 'POST',
      body: JSON.stringify({ notes })
    }),

  getDashboardAlerts: () => request<import('./types').DashboardAlerts>('/loads/dashboard/alerts'),

  getShippers: () => request<{ id: string; email: string; fullName: string }[]>('/staff/shippers'),
  getCarriers: () => request<{ id: string; name: string }[]>('/staff/carriers'),

  getStaff: () => request<import('./types').StaffMember[]>('/staff'),
  createStaff: (data: object) =>
    request<import('./types').StaffMember>('/staff', { method: 'POST', body: JSON.stringify(data) }),

  getRoles: () => request<import('./types').Role[]>('/staff/roles'),
  createRole: (data: object) =>
    request<import('./types').Role>('/staff/roles', { method: 'POST', body: JSON.stringify(data) }),

  getCompliance: (carrierOrganizationId?: string) => {
    const qs = carrierOrganizationId ? `?carrierOrganizationId=${carrierOrganizationId}` : '';
    return request<import('./types').ComplianceRecord>(`/compliance${qs}`);
  },

  upsertCompliance: (data: object) =>
    request<import('./types').ComplianceRecord>('/compliance', {
      method: 'PUT',
      body: JSON.stringify(data)
    }),

  uploadPod: (loadId: string, file: File) => {
    const form = new FormData();
    form.append('file', file);
    return request<import('./types').Pod>(`/pods/${loadId}`, { method: 'POST', body: form });
  },

  downloadPod: async (loadId: string, podId: string, fileName: string) => {
    const token = getToken();
    const res = await fetch(`${API_BASE}/pods/${loadId}/${podId}`, {
      headers: token ? { Authorization: `Bearer ${token}` } : {}
    });
    if (!res.ok) throw new Error('Unable to download POD');
    const url = URL.createObjectURL(await res.blob());
    const anchor = document.createElement('a');
    anchor.href = url;
    anchor.download = fileName;
    anchor.click();
    URL.revokeObjectURL(url);
  },

  getPermissionDeniedLogs: () =>
    request<{ id: string; userEmail: string; permission: string; method: string; path: string; timestamp: string }[]>(
      '/auditlogs/permission-denied'
    )
};
