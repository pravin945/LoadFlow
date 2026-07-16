namespace LoadFlow.Api.Models;

public class Organization
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public AccountType Type { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<User> Users { get; set; } = [];
    public ICollection<Role> Roles { get; set; } = [];
    public ICollection<Load> BrokerLoads { get; set; } = [];
    public ICollection<Load> CarrierLoads { get; set; } = [];
    public CarrierCompliance? Compliance { get; set; }
}

public class User
{
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public AccountType AccountType { get; set; }
    public Guid? OrganizationId { get; set; }
    public bool IsOrgAdmin { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Organization? Organization { get; set; }
    public ICollection<UserRole> UserRoles { get; set; } = [];
    public ICollection<Load> ShipperLoads { get; set; } = [];
}

public class Role
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsSystemAdminRole { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Organization Organization { get; set; } = null!;
    public ICollection<RolePermission> RolePermissions { get; set; } = [];
    public ICollection<UserRole> UserRoles { get; set; } = [];
}

public class RolePermission
{
    public Guid RoleId { get; set; }
    public string Permission { get; set; } = string.Empty;

    public Role Role { get; set; } = null!;
}

public class UserRole
{
    public Guid UserId { get; set; }
    public Guid RoleId { get; set; }

    public User User { get; set; } = null!;
    public Role Role { get; set; } = null!;
}

public class CarrierCompliance
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public DateTime InsuranceExpiry { get; set; }
    public AuthorityStatus McAuthorityStatus { get; set; }
    public AuthorityStatus DotAuthorityStatus { get; set; }
    public string ApprovedEquipmentTypes { get; set; } = "[]";
    public string ApprovedCommodityTypes { get; set; } = "[]";
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public Guid? UpdatedByUserId { get; set; }

    public Organization Organization { get; set; } = null!;
}

public class Load
{
    public Guid Id { get; set; }
    public string ReferenceNumber { get; set; } = string.Empty;
    public Guid BrokerOrganizationId { get; set; }
    public Guid ShipperUserId { get; set; }
    public Guid? CarrierOrganizationId { get; set; }
    public LoadStatus Status { get; set; } = LoadStatus.Posted;
    public string Origin { get; set; } = string.Empty;
    public string Destination { get; set; } = string.Empty;
    public string EquipmentType { get; set; } = string.Empty;
    public string CommodityType { get; set; } = string.Empty;
    public decimal WeightLbs { get; set; }
    public DateTime PickupDate { get; set; }
    public DateTime DeliveryDate { get; set; }
    public ComplianceFlagStatus ComplianceFlag { get; set; } = ComplianceFlagStatus.None;
    public string? ComplianceFlagReason { get; set; }
    public Guid? ActiveRateConfirmationVersionId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public Guid CreatedByUserId { get; set; }

    public Organization BrokerOrganization { get; set; } = null!;
    public Organization? CarrierOrganization { get; set; }
    public User ShipperUser { get; set; } = null!;
    public RateConfirmationVersion? ActiveRateConfirmationVersion { get; set; }
    public ICollection<LoadAuditEntry> AuditEntries { get; set; } = [];
    public ICollection<RateConfirmation> RateConfirmations { get; set; } = [];
    public ICollection<PodDocument> PodDocuments { get; set; } = [];
}

public class LoadAuditEntry
{
    public Guid Id { get; set; }
    public Guid LoadId { get; set; }
    public LoadStatus? FromStatus { get; set; }
    public LoadStatus? ToStatus { get; set; }
    public string Action { get; set; } = string.Empty;
    public string? Details { get; set; }
    public Guid UserId { get; set; }
    public string UserEmail { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    public Load Load { get; set; } = null!;
}

public class RateConfirmation
{
    public Guid Id { get; set; }
    public Guid LoadId { get; set; }
    public int CurrentVersionNumber { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Load Load { get; set; } = null!;
    public ICollection<RateConfirmationVersion> Versions { get; set; } = [];
}

public class RateConfirmationVersion
{
    public Guid Id { get; set; }
    public Guid RateConfirmationId { get; set; }
    public int VersionNumber { get; set; }
    public decimal BaseRate { get; set; }
    public string AccessorialsJson { get; set; } = "[]";
    public string? Notes { get; set; }
    public Guid ConfirmedByUserId { get; set; }
    public DateTime ConfirmedAt { get; set; } = DateTime.UtcNow;

    public RateConfirmation RateConfirmation { get; set; } = null!;
}

public class PodDocument
{
    public Guid Id { get; set; }
    public Guid LoadId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string StoredPath { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public Guid UploadedByUserId { get; set; }
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;

    public Load Load { get; set; } = null!;
}

public class PermissionDeniedLog
{
    public Guid Id { get; set; }
    public Guid? UserId { get; set; }
    public string? UserEmail { get; set; }
    public string Permission { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public string Method { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
