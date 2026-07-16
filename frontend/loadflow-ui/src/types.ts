export interface UserProfile {
  id: string;
  email: string;
  fullName: string;
  accountType: 'Broker' | 'Carrier' | 'Shipper';
  organizationId?: string;
  organizationName?: string;
  isOrgAdmin: boolean;
  permissions: string[];
  roleNames: string[];
}

export interface Load {
  id: string;
  referenceNumber: string;
  status: string;
  origin: string;
  destination: string;
  equipmentType: string;
  commodityType: string;
  weightLbs: number;
  pickupDate: string;
  deliveryDate: string;
  complianceFlag: string;
  complianceFlagReason?: string;
  brokerOrganizationId: string;
  brokerName: string;
  carrierOrganizationId?: string;
  carrierName?: string;
  shipperUserId: string;
  shipperName: string;
  activeRate?: {
    versionNumber: number;
    baseRate: number;
    accessorialsJson: string;
    notes?: string;
    confirmedAt: string;
  };
  auditTrail: AuditEntry[];
  pods: Pod[];
}

export interface AuditEntry {
  id: string;
  fromStatus?: string;
  toStatus?: string;
  action: string;
  details?: string;
  userEmail: string;
  timestamp: string;
}

export interface Pod {
  id: string;
  fileName: string;
  contentType: string;
  fileSizeBytes: number;
  uploadedAt: string;
}

export interface Role {
  id: string;
  name: string;
  description?: string;
  isSystemAdminRole: boolean;
  permissions: string[];
}

export interface StaffMember {
  id: string;
  email: string;
  fullName: string;
  isActive: boolean;
  isOrgAdmin: boolean;
  roleNames: string[];
}

export interface ComplianceRecord {
  id: string;
  organizationId: string;
  organizationName: string;
  insuranceExpiry: string;
  mcAuthorityStatus: string;
  dotAuthorityStatus: string;
  approvedEquipmentTypes: string;
  approvedCommodityTypes: string;
  updatedAt: string;
}

export interface DashboardAlerts {
  activeLoads: number;
  complianceFlaggedLoads: number;
  complianceAlerts: {
    carrierOrganizationId: string;
    carrierName: string;
    insuranceExpiry: string;
    mcAuthorityStatus: string;
    dotAuthorityStatus: string;
    alertType: string;
  }[];
}

export const PERMISSIONS = {
  loadCreate: 'load.create',
  loadAssign: 'load.assign_carrier',
  loadOverride: 'load.override_compliance_flag',
  rateConfirm: 'rate.confirm',
  loadUpdate: 'load.update_status',
  staffManage: 'staff.manage',
  podUpload: 'pod.upload',
  loadView: 'load.view',
  complianceManage: 'compliance.manage'
} as const;

export function hasPermission(user: UserProfile | null, permission: string): boolean {
  if (!user) return false;
  if (user.isOrgAdmin) return true;
  if (user.accountType === 'Shipper' && permission === PERMISSIONS.loadView) return true;
  return user.permissions.includes(permission);
}
