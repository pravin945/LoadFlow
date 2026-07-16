using Microsoft.EntityFrameworkCore;
using LoadFlow.Api.Data;
using LoadFlow.Api.Models;

namespace LoadFlow.Api.Services;

public class StaffService(AppDbContext db, CurrentUserService currentUser)
{
    public async Task<List<StaffMemberDto>> ListStaffAsync()
    {
        EnsureOrgAdmin();
        return await db.Users
            .Where(u => u.OrganizationId == currentUser.OrganizationId)
            .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
            .Select(u => StaffMemberDto.From(u))
            .ToListAsync();
    }

    public async Task<StaffMemberDto> CreateStaffAsync(CreateStaffRequest request)
    {
        EnsureOrgAdmin();
        if (await db.Users.AnyAsync(u => u.Email == request.Email))
            throw new InvalidOperationException("Email already exists.");

        var roles = await db.Roles
            .Where(r => request.RoleIds.Contains(r.Id) && r.OrganizationId == currentUser.OrganizationId)
            .ToListAsync();
        if (roles.Count != request.RoleIds.Count)
            throw new ArgumentException("One or more roles are invalid.");

        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = request.Email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            FullName = request.FullName,
            AccountType = currentUser.AccountType,
            OrganizationId = currentUser.OrganizationId,
            IsOrgAdmin = false
        };
        db.Users.Add(user);
        foreach (var role in roles)
            db.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = role.Id });

        await db.SaveChangesAsync();
        return StaffMemberDto.From(await db.Users.Include(u => u.UserRoles).ThenInclude(ur => ur.Role).FirstAsync(u => u.Id == user.Id));
    }

    public async Task<List<RoleDto>> ListRolesAsync()
    {
        EnsureOrgContext();
        return await db.Roles
            .Where(r => r.OrganizationId == currentUser.OrganizationId)
            .Include(r => r.RolePermissions)
            .Select(r => RoleDto.From(r))
            .ToListAsync();
    }

    public async Task<RoleDto> CreateRoleAsync(CreateRoleRequest request)
    {
        EnsureOrgAdmin();
        ValidatePermissions(request.Permissions);

        if (await db.Roles.AnyAsync(r => r.OrganizationId == currentUser.OrganizationId && r.Name == request.Name))
            throw new InvalidOperationException("Role name already exists.");

        var role = new Role
        {
            Id = Guid.NewGuid(),
            OrganizationId = currentUser.OrganizationId!.Value,
            Name = request.Name,
            Description = request.Description,
            RolePermissions = request.Permissions.Distinct().Select(p => new RolePermission { Permission = p }).ToList()
        };
        foreach (var rp in role.RolePermissions) rp.RoleId = role.Id;

        db.Roles.Add(role);
        await db.SaveChangesAsync();
        return RoleDto.From(await db.Roles.Include(r => r.RolePermissions).FirstAsync(r => r.Id == role.Id));
    }

    public async Task<RoleDto> UpdateRoleAsync(Guid roleId, UpdateRoleRequest request)
    {
        EnsureOrgAdmin();
        ValidatePermissions(request.Permissions);

        var role = await db.Roles
            .Include(r => r.RolePermissions)
            .FirstOrDefaultAsync(r => r.Id == roleId && r.OrganizationId == currentUser.OrganizationId)
            ?? throw new KeyNotFoundException("Role not found.");

        if (role.IsSystemAdminRole)
            throw new InvalidOperationException("Cannot modify the system Admin role.");

        role.Name = request.Name;
        role.Description = request.Description;
        db.RolePermissions.RemoveRange(role.RolePermissions);
        role.RolePermissions = request.Permissions.Distinct().Select(p => new RolePermission { RoleId = roleId, Permission = p }).ToList();

        await db.SaveChangesAsync();
        return RoleDto.From(role);
    }

    public async Task<List<ShipperLookupDto>> ListShippersAsync()
    {
        return await db.Users
            .Where(u => u.AccountType == AccountType.Shipper && u.IsActive)
            .Select(u => new ShipperLookupDto(u.Id, u.Email, u.FullName))
            .ToListAsync();
    }

    public async Task<List<CarrierLookupDto>> ListCarriersAsync()
    {
        return await db.Organizations
            .Where(o => o.Type == AccountType.Carrier)
            .Select(o => new CarrierLookupDto(o.Id, o.Name))
            .ToListAsync();
    }

    private void EnsureOrgAdmin()
    {
        if (!currentUser.IsOrgAdmin || currentUser.OrganizationId == null)
            throw new UnauthorizedAccessException("Organization admin required.");
    }

    private void EnsureOrgContext()
    {
        if (currentUser.OrganizationId == null)
            throw new UnauthorizedAccessException("Organization context required.");
    }

    private static void ValidatePermissions(IEnumerable<string> permissions)
    {
        foreach (var p in permissions)
            if (!PermissionCatalog.All.Contains(p))
                throw new ArgumentException($"Unknown permission: {p}");
    }
}

public record CreateStaffRequest(string Email, string Password, string FullName, List<Guid> RoleIds);
public record CreateRoleRequest(string Name, string? Description, List<string> Permissions);
public record UpdateRoleRequest(string Name, string? Description, List<string> Permissions);

public record StaffMemberDto(Guid Id, string Email, string FullName, bool IsActive, bool IsOrgAdmin, List<string> RoleNames)
{
    public static StaffMemberDto From(User u) => new(
        u.Id, u.Email, u.FullName, u.IsActive, u.IsOrgAdmin,
        u.UserRoles.Select(ur => ur.Role.Name).ToList());
}

public record RoleDto(Guid Id, string Name, string? Description, bool IsSystemAdminRole, List<string> Permissions)
{
    public static RoleDto From(Role r) => new(
        r.Id, r.Name, r.Description, r.IsSystemAdminRole,
        r.RolePermissions.Select(rp => rp.Permission).ToList());
}

public record ShipperLookupDto(Guid Id, string Email, string FullName);
public record CarrierLookupDto(Guid Id, string Name);

public class ComplianceRecordService(AppDbContext db, CurrentUserService currentUser)
{
    public async Task<ComplianceRecordDto?> GetAsync(Guid? carrierOrgId = null)
    {
        var orgId = carrierOrgId ?? currentUser.OrganizationId;
        if (orgId == null) return null;

        if (currentUser.AccountType == AccountType.Carrier && orgId != currentUser.OrganizationId)
            throw new UnauthorizedAccessException("Cannot view another carrier's compliance.");

        if (currentUser.AccountType == AccountType.Broker && carrierOrgId == null)
            throw new ArgumentException("Broker must specify carrierOrganizationId.");

        var record = await db.CarrierCompliances
            .Include(c => c.Organization)
            .FirstOrDefaultAsync(c => c.OrganizationId == orgId);

        return record == null ? null : ComplianceRecordDto.From(record);
    }

    public async Task<ComplianceRecordDto> UpsertAsync(UpsertComplianceRequest request)
    {
        var orgId = currentUser.AccountType == AccountType.Carrier
            ? currentUser.OrganizationId!.Value
            : request.CarrierOrganizationId ?? throw new ArgumentException("CarrierOrganizationId required for broker.");

        if (currentUser.AccountType == AccountType.Carrier && orgId != currentUser.OrganizationId)
            throw new UnauthorizedAccessException();

        var record = await db.CarrierCompliances.FirstOrDefaultAsync(c => c.OrganizationId == orgId);
        if (record == null)
        {
            record = new CarrierCompliance { Id = Guid.NewGuid(), OrganizationId = orgId };
            db.CarrierCompliances.Add(record);
        }

        record.InsuranceExpiry = request.InsuranceExpiry;
        record.McAuthorityStatus = request.McAuthorityStatus;
        record.DotAuthorityStatus = request.DotAuthorityStatus;
        record.ApprovedEquipmentTypes = request.ApprovedEquipmentTypes;
        record.ApprovedCommodityTypes = request.ApprovedCommodityTypes;
        record.UpdatedAt = DateTime.UtcNow;
        record.UpdatedByUserId = currentUser.UserId;

        await db.SaveChangesAsync();
        return ComplianceRecordDto.From(await db.CarrierCompliances.Include(c => c.Organization).FirstAsync(c => c.Id == record.Id));
    }
}

public record UpsertComplianceRequest(
    Guid? CarrierOrganizationId,
    DateTime InsuranceExpiry,
    AuthorityStatus McAuthorityStatus,
    AuthorityStatus DotAuthorityStatus,
    string ApprovedEquipmentTypes,
    string ApprovedCommodityTypes);

public record ComplianceRecordDto(
    Guid Id, Guid OrganizationId, string OrganizationName,
    DateTime InsuranceExpiry, string McAuthorityStatus, string DotAuthorityStatus,
    string ApprovedEquipmentTypes, string ApprovedCommodityTypes, DateTime UpdatedAt)
{
    public static ComplianceRecordDto From(CarrierCompliance c) => new(
        c.Id, c.OrganizationId, c.Organization.Name,
        c.InsuranceExpiry, c.McAuthorityStatus.ToString(), c.DotAuthorityStatus.ToString(),
        c.ApprovedEquipmentTypes, c.ApprovedCommodityTypes, c.UpdatedAt);
}

public class PodService(AppDbContext db, CurrentUserService currentUser, IConfiguration config, IWebHostEnvironment env)
{
    public async Task<PodDto> UploadAsync(Guid loadId, IFormFile file)
    {
        var load = await db.Loads.FirstOrDefaultAsync(l =>
            l.Id == loadId && l.CarrierOrganizationId == currentUser.OrganizationId);
        if (load == null) throw new KeyNotFoundException("Load not found.");
        if (load.Status is not (LoadStatus.Delivered or LoadStatus.PodVerified))
            throw new InvalidOperationException("POD can only be uploaded after delivery.");
        if (file.Length == 0)
            throw new InvalidOperationException("The uploaded file is empty.");

        var allowedTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "application/pdf", "image/jpeg", "image/png", "image/webp"
        };
        if (!allowedTypes.Contains(file.ContentType))
            throw new InvalidOperationException("Only PDF, JPEG, PNG, or WebP POD files are allowed.");

        var uploadDir = Path.Combine(env.ContentRootPath, config["FileStorage:PodUploadPath"] ?? "uploads/pods");
        Directory.CreateDirectory(uploadDir);

        var safeName = $"{loadId}_{Guid.NewGuid()}{Path.GetExtension(file.FileName)}";
        var fullPath = Path.Combine(uploadDir, safeName);

        await using (var stream = new FileStream(fullPath, FileMode.Create))
            await file.CopyToAsync(stream);

        var doc = new PodDocument
        {
            Id = Guid.NewGuid(),
            LoadId = loadId,
            FileName = file.FileName,
            StoredPath = safeName,
            ContentType = file.ContentType,
            FileSizeBytes = file.Length,
            UploadedByUserId = currentUser.UserId
        };
        db.PodDocuments.Add(doc);
        await db.SaveChangesAsync();
        return PodDto.From(doc);
    }

    public async Task<(Stream stream, string contentType, string fileName)?> DownloadAsync(Guid loadId, Guid podId)
    {
        var loadService = db.Loads.AsQueryable();
        var load = await loadService.FirstOrDefaultAsync(l => l.Id == loadId);
        if (load == null) return null;

        var scoped = currentUser.AccountType switch
        {
            AccountType.Broker => load.BrokerOrganizationId == currentUser.OrganizationId,
            AccountType.Carrier => load.CarrierOrganizationId == currentUser.OrganizationId,
            AccountType.Shipper => load.ShipperUserId == currentUser.UserId,
            _ => false
        };
        if (!scoped) return null;

        var doc = await db.PodDocuments.FirstOrDefaultAsync(p => p.Id == podId && p.LoadId == loadId);
        if (doc == null) return null;

        var fullPath = Path.Combine(env.ContentRootPath, config["FileStorage:PodUploadPath"] ?? "uploads/pods", doc.StoredPath);
        if (!File.Exists(fullPath)) return null;

        return (new FileStream(fullPath, FileMode.Open, FileAccess.Read), doc.ContentType, doc.FileName);
    }
}
