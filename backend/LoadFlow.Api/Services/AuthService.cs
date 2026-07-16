using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using LoadFlow.Api.Authorization;
using LoadFlow.Api.Data;
using LoadFlow.Api.Models;
using AppClaimTypes = LoadFlow.Api.Authorization.ClaimTypes;

namespace LoadFlow.Api.Services;

public class AuthService(AppDbContext db, IConfiguration config)
{
    public async Task<(User user, string token)?> LoginAsync(string email, string password)
    {
        var user = await db.Users
            .Include(u => u.UserRoles)
            .ThenInclude(ur => ur.Role)
            .ThenInclude(r => r.RolePermissions)
            .FirstOrDefaultAsync(u => u.Email == email && u.IsActive);

        if (user == null || !BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
            return null;

        return (user, GenerateToken(user));
    }

    public async Task<(User user, string token)?> RegisterOrgAdminAsync(RegisterOrgRequest request)
    {
        if (request.AccountType is not (AccountType.Broker or AccountType.Carrier))
            throw new InvalidOperationException("Only Broker or Carrier org registration is supported.");

        if (await db.Users.AnyAsync(u => u.Email == request.Email))
            throw new InvalidOperationException("Email already registered.");

        var org = new Organization
        {
            Id = Guid.NewGuid(),
            Name = request.OrganizationName,
            Type = request.AccountType
        };
        db.Organizations.Add(org);

        var adminRole = new Role
        {
            Id = Guid.NewGuid(),
            OrganizationId = org.Id,
            Name = "Admin",
            Description = "Organization administrator",
            IsSystemAdminRole = true,
            RolePermissions = PermissionCatalog.All.Select(p => new RolePermission
            {
                RoleId = Guid.Empty,
                Permission = p
            }).ToList()
        };
        foreach (var rp in adminRole.RolePermissions) rp.RoleId = adminRole.Id;
        db.Roles.Add(adminRole);

        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = request.Email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            FullName = request.FullName,
            AccountType = request.AccountType,
            OrganizationId = org.Id,
            IsOrgAdmin = true
        };
        db.Users.Add(user);
        db.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = adminRole.Id });

        if (request.AccountType == AccountType.Carrier)
        {
            db.CarrierCompliances.Add(new CarrierCompliance
            {
                Id = Guid.NewGuid(),
                OrganizationId = org.Id,
                InsuranceExpiry = DateTime.UtcNow.AddYears(1),
                McAuthorityStatus = AuthorityStatus.Active,
                DotAuthorityStatus = AuthorityStatus.Active,
                ApprovedEquipmentTypes = "[]",
                ApprovedCommodityTypes = "[]",
                UpdatedByUserId = user.Id
            });
        }

        await db.SaveChangesAsync();
        return (user, GenerateToken(user));
    }

    public async Task<(User user, string token)?> RegisterShipperAsync(RegisterShipperRequest request)
    {
        if (await db.Users.AnyAsync(u => u.Email == request.Email))
            throw new InvalidOperationException("Email already registered.");

        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = request.Email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            FullName = request.FullName,
            AccountType = AccountType.Shipper
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return (user, GenerateToken(user));
    }

    public async Task<UserProfileDto> GetProfileAsync(Guid userId)
    {
        var user = await db.Users
            .Include(u => u.Organization)
            .Include(u => u.UserRoles).ThenInclude(ur => ur.Role).ThenInclude(r => r.RolePermissions)
            .FirstAsync(u => u.Id == userId);

        return UserProfileDto.From(user);
    }

    private string GenerateToken(User user)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(config["Jwt:Key"]!));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email),
            new(AppClaimTypes.UserId, user.Id.ToString()),
            new(AppClaimTypes.AccountType, user.AccountType.ToString()),
            new(AppClaimTypes.IsOrgAdmin, user.IsOrgAdmin.ToString().ToLower())
        };

        if (user.OrganizationId.HasValue)
            claims.Add(new Claim(AppClaimTypes.OrganizationId, user.OrganizationId.Value.ToString()));

        if (!user.IsOrgAdmin)
        {
            var perms = user.UserRoles.SelectMany(ur => ur.Role.RolePermissions).Select(rp => rp.Permission).Distinct();
            claims.AddRange(perms.Select(p => new Claim(AppClaimTypes.Permissions, p)));
        }

        var token = new JwtSecurityToken(
            issuer: config["Jwt:Issuer"],
            audience: config["Jwt:Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddHours(double.Parse(config["Jwt:ExpiryHours"] ?? "12")),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}

public record RegisterOrgRequest(string Email, string Password, string FullName, string OrganizationName, AccountType AccountType);
public record RegisterShipperRequest(string Email, string Password, string FullName);
public record LoginRequest(string Email, string Password);

public record UserProfileDto(
    Guid Id,
    string Email,
    string FullName,
    string AccountType,
    Guid? OrganizationId,
    string? OrganizationName,
    bool IsOrgAdmin,
    IReadOnlyList<string> Permissions,
    IReadOnlyList<string> RoleNames)
{
    public static UserProfileDto From(User user)
    {
        var permissions = user.IsOrgAdmin
            ? PermissionCatalog.All.ToList()
            : user.UserRoles.SelectMany(ur => ur.Role.RolePermissions).Select(rp => rp.Permission).Distinct().ToList();

        return new UserProfileDto(
            user.Id,
            user.Email,
            user.FullName,
            user.AccountType.ToString(),
            user.OrganizationId,
            user.Organization?.Name,
            user.IsOrgAdmin,
            permissions,
            user.UserRoles.Select(ur => ur.Role.Name).ToList());
    }
}

public record AuthResponse(string Token, UserProfileDto User);
