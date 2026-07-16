using Microsoft.EntityFrameworkCore;
using LoadFlow.Api.Models;

namespace LoadFlow.Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Organization> Organizations => Set<Organization>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<CarrierCompliance> CarrierCompliances => Set<CarrierCompliance>();
    public DbSet<Load> Loads => Set<Load>();
    public DbSet<LoadAuditEntry> LoadAuditEntries => Set<LoadAuditEntry>();
    public DbSet<RateConfirmation> RateConfirmations => Set<RateConfirmation>();
    public DbSet<RateConfirmationVersion> RateConfirmationVersions => Set<RateConfirmationVersion>();
    public DbSet<PodDocument> PodDocuments => Set<PodDocument>();
    public DbSet<PermissionDeniedLog> PermissionDeniedLogs => Set<PermissionDeniedLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Organization>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.Name, x.Type });
        });

        modelBuilder.Entity<User>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.Email).IsUnique();
            e.HasOne(x => x.Organization).WithMany(o => o.Users).HasForeignKey(x => x.OrganizationId);
        });

        modelBuilder.Entity<Role>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.OrganizationId, x.Name }).IsUnique();
            e.HasOne(x => x.Organization).WithMany(o => o.Roles).HasForeignKey(x => x.OrganizationId);
        });

        modelBuilder.Entity<RolePermission>(e =>
        {
            e.HasKey(x => new { x.RoleId, x.Permission });
            e.HasOne(x => x.Role).WithMany(r => r.RolePermissions).HasForeignKey(x => x.RoleId);
        });

        modelBuilder.Entity<UserRole>(e =>
        {
            e.HasKey(x => new { x.UserId, x.RoleId });
            e.HasOne(x => x.User).WithMany(u => u.UserRoles).HasForeignKey(x => x.UserId);
            e.HasOne(x => x.Role).WithMany(r => r.UserRoles).HasForeignKey(x => x.RoleId);
        });

        modelBuilder.Entity<CarrierCompliance>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasOne(x => x.Organization).WithOne(o => o.Compliance).HasForeignKey<CarrierCompliance>(x => x.OrganizationId);
        });

        modelBuilder.Entity<Load>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.ReferenceNumber).IsUnique();
            e.HasOne(x => x.BrokerOrganization).WithMany(o => o.BrokerLoads).HasForeignKey(x => x.BrokerOrganizationId);
            e.HasOne(x => x.CarrierOrganization).WithMany(o => o.CarrierLoads).HasForeignKey(x => x.CarrierOrganizationId);
            e.HasOne(x => x.ShipperUser).WithMany(u => u.ShipperLoads).HasForeignKey(x => x.ShipperUserId);
            e.HasOne(x => x.ActiveRateConfirmationVersion).WithMany().HasForeignKey(x => x.ActiveRateConfirmationVersionId).OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<LoadAuditEntry>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasOne(x => x.Load).WithMany(l => l.AuditEntries).HasForeignKey(x => x.LoadId);
        });

        modelBuilder.Entity<RateConfirmation>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasOne(x => x.Load).WithMany(l => l.RateConfirmations).HasForeignKey(x => x.LoadId);
        });

        modelBuilder.Entity<RateConfirmationVersion>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasOne(x => x.RateConfirmation).WithMany(r => r.Versions).HasForeignKey(x => x.RateConfirmationId);
        });

        modelBuilder.Entity<PodDocument>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasOne(x => x.Load).WithMany(l => l.PodDocuments).HasForeignKey(x => x.LoadId);
        });

        modelBuilder.Entity<PermissionDeniedLog>(e =>
        {
            e.HasKey(x => x.Id);
        });
    }
}
