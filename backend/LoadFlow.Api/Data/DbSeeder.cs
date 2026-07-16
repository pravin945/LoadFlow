using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using LoadFlow.Api.Models;

namespace LoadFlow.Api.Data;

public static class DbSeeder
{
    public static async Task SeedAsync(AppDbContext db)
    {
        await db.Database.EnsureCreatedAsync();
        if (await db.Users.AnyAsync()) return;

        var brokerOrg = new Organization { Id = Guid.NewGuid(), Name = "Acme Freight Brokerage", Type = AccountType.Broker };
        var carrierOrg = new Organization { Id = Guid.NewGuid(), Name = "Swift Haul Logistics", Type = AccountType.Carrier };
        db.Organizations.AddRange(brokerOrg, carrierOrg);

        var brokerAdminRole = CreateAdminRole(brokerOrg.Id, "Admin", allPermissions: true);
        var carrierAdminRole = CreateAdminRole(carrierOrg.Id, "Admin", allPermissions: true);

        var brokerDispatcherRole = new Role
        {
            Id = Guid.NewGuid(),
            OrganizationId = brokerOrg.Id,
            Name = "Dispatcher",
            Description = "Post loads, assign carriers, confirm rates",
            RolePermissions =
            [
                Perm(brokerOrg.Id, PermissionCatalog.LoadView),
                Perm(brokerOrg.Id, PermissionCatalog.LoadCreate),
                Perm(brokerOrg.Id, PermissionCatalog.LoadAssignCarrier),
                Perm(brokerOrg.Id, PermissionCatalog.RateConfirm)
            ]
        };
        foreach (var rp in brokerDispatcherRole.RolePermissions) rp.RoleId = brokerDispatcherRole.Id;

        var brokerOpsLeadRole = new Role
        {
            Id = Guid.NewGuid(),
            OrganizationId = brokerOrg.Id,
            Name = "Ops Lead",
            Description = "Dispatcher permissions plus compliance override",
            RolePermissions =
            [
                Perm(brokerOrg.Id, PermissionCatalog.LoadView),
                Perm(brokerOrg.Id, PermissionCatalog.LoadCreate),
                Perm(brokerOrg.Id, PermissionCatalog.LoadAssignCarrier),
                Perm(brokerOrg.Id, PermissionCatalog.RateConfirm),
                Perm(brokerOrg.Id, PermissionCatalog.LoadOverrideCompliance)
            ]
        };
        foreach (var rp in brokerOpsLeadRole.RolePermissions) rp.RoleId = brokerOpsLeadRole.Id;

        var carrierDriverRole = new Role
        {
            Id = Guid.NewGuid(),
            OrganizationId = carrierOrg.Id,
            Name = "Driver",
            Description = "Update status and upload POD",
            RolePermissions =
            [
                Perm(carrierOrg.Id, PermissionCatalog.LoadView),
                Perm(carrierOrg.Id, PermissionCatalog.LoadUpdateStatus),
                Perm(carrierOrg.Id, PermissionCatalog.PodUpload)
            ]
        };
        foreach (var rp in carrierDriverRole.RolePermissions) rp.RoleId = carrierDriverRole.Id;

        var carrierDispatchRole = new Role
        {
            Id = Guid.NewGuid(),
            OrganizationId = carrierOrg.Id,
            Name = "Carrier Dispatch",
            Description = "Accept/decline loads and update status",
            RolePermissions =
            [
                Perm(carrierOrg.Id, PermissionCatalog.LoadView),
                Perm(carrierOrg.Id, PermissionCatalog.LoadUpdateStatus)
            ]
        };
        foreach (var rp in carrierDispatchRole.RolePermissions) rp.RoleId = carrierDispatchRole.Id;

        db.Roles.AddRange(brokerAdminRole, carrierAdminRole, brokerDispatcherRole, brokerOpsLeadRole, carrierDriverRole, carrierDispatchRole);

        var brokerAdmin = new User
        {
            Id = Guid.NewGuid(),
            Email = "broker.admin@loadflow.demo",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Demo123!"),
            FullName = "Broker Admin",
            AccountType = AccountType.Broker,
            OrganizationId = brokerOrg.Id,
            IsOrgAdmin = true
        };
        var brokerDispatcher = new User
        {
            Id = Guid.NewGuid(),
            Email = "dispatcher@loadflow.demo",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Demo123!"),
            FullName = "Jane Dispatcher",
            AccountType = AccountType.Broker,
            OrganizationId = brokerOrg.Id
        };
        var carrierAdmin = new User
        {
            Id = Guid.NewGuid(),
            Email = "carrier.admin@loadflow.demo",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Demo123!"),
            FullName = "Carrier Admin",
            AccountType = AccountType.Carrier,
            OrganizationId = carrierOrg.Id,
            IsOrgAdmin = true
        };
        var carrierDriver = new User
        {
            Id = Guid.NewGuid(),
            Email = "driver@loadflow.demo",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Demo123!"),
            FullName = "Mike Driver",
            AccountType = AccountType.Carrier,
            OrganizationId = carrierOrg.Id
        };
        var shipper = new User
        {
            Id = Guid.NewGuid(),
            Email = "shipper@loadflow.demo",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Demo123!"),
            FullName = "Global Shipper Inc",
            AccountType = AccountType.Shipper
        };

        db.Users.AddRange(brokerAdmin, brokerDispatcher, carrierAdmin, carrierDriver, shipper);

        db.UserRoles.AddRange(
            new UserRole { UserId = brokerAdmin.Id, RoleId = brokerAdminRole.Id },
            new UserRole { UserId = brokerDispatcher.Id, RoleId = brokerDispatcherRole.Id },
            new UserRole { UserId = carrierAdmin.Id, RoleId = carrierAdminRole.Id },
            new UserRole { UserId = carrierDriver.Id, RoleId = carrierDriverRole.Id }
        );

        var compliance = new CarrierCompliance
        {
            Id = Guid.NewGuid(),
            OrganizationId = carrierOrg.Id,
            InsuranceExpiry = DateTime.UtcNow.AddMonths(6),
            McAuthorityStatus = AuthorityStatus.Active,
            DotAuthorityStatus = AuthorityStatus.Active,
            ApprovedEquipmentTypes = JsonSerializer.Serialize(new[] { "Dry Van", "Reefer", "Flatbed" }),
            ApprovedCommodityTypes = JsonSerializer.Serialize(new[] { "General Freight", "Food Grade", "Electronics" }),
            UpdatedByUserId = carrierAdmin.Id
        };
        db.CarrierCompliances.Add(compliance);

        var sampleLoad = new Load
        {
            Id = Guid.NewGuid(),
            ReferenceNumber = "LF-2026-0001",
            BrokerOrganizationId = brokerOrg.Id,
            ShipperUserId = shipper.Id,
            Status = LoadStatus.Posted,
            Origin = "Chicago, IL",
            Destination = "Dallas, TX",
            EquipmentType = "Dry Van",
            CommodityType = "General Freight",
            WeightLbs = 42000,
            PickupDate = DateTime.UtcNow.AddDays(3),
            DeliveryDate = DateTime.UtcNow.AddDays(5),
            CreatedByUserId = brokerAdmin.Id
        };
        db.Loads.Add(sampleLoad);
        db.LoadAuditEntries.Add(new LoadAuditEntry
        {
            Id = Guid.NewGuid(),
            LoadId = sampleLoad.Id,
            ToStatus = LoadStatus.Posted,
            Action = "load.created",
            Details = "Seed load created",
            UserId = brokerAdmin.Id,
            UserEmail = brokerAdmin.Email
        });

        await db.SaveChangesAsync();
    }

    private static Role CreateAdminRole(Guid orgId, string name, bool allPermissions)
    {
        var role = new Role
        {
            Id = Guid.NewGuid(),
            OrganizationId = orgId,
            Name = name,
            Description = "Organization administrator with full permissions",
            IsSystemAdminRole = true
        };
        if (allPermissions)
        {
            role.RolePermissions = PermissionCatalog.All.Select(p => new RolePermission
            {
                RoleId = role.Id,
                Permission = p
            }).ToList();
        }
        return role;
    }

    private static RolePermission Perm(Guid _, string permission) =>
        new() { Permission = permission };
}
