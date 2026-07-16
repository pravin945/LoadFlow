using System.Security.Claims;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using LoadFlow.Api.Authorization;
using LoadFlow.Api.Data;
using LoadFlow.Api.Models;
using AppClaimTypes = LoadFlow.Api.Authorization.ClaimTypes;

namespace LoadFlow.Api.Services;

public class CurrentUserService(IHttpContextAccessor httpContextAccessor)
{
    public ClaimsPrincipal? Principal => httpContextAccessor.HttpContext?.User;

    public Guid UserId => Guid.Parse(Principal?.FindFirst(AppClaimTypes.UserId)?.Value
        ?? throw new UnauthorizedAccessException());

    public string Email => Principal?.FindFirst("email")?.Value
        ?? Principal?.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value ?? "";

    public AccountType AccountType =>
        Enum.Parse<AccountType>(Principal?.FindFirst(AppClaimTypes.AccountType)?.Value ?? "Shipper");

    public Guid? OrganizationId
    {
        get
        {
            var val = Principal?.FindFirst(AppClaimTypes.OrganizationId)?.Value;
            return val == null ? null : Guid.Parse(val);
        }
    }

    public bool IsOrgAdmin => Principal?.FindFirst(AppClaimTypes.IsOrgAdmin)?.Value == "true";
}

public class ComplianceService(AppDbContext db)
{
    public async Task<(ComplianceFlagStatus flag, string? reason)> EvaluateCarrierAsync(
        Guid carrierOrgId, string equipmentType, string commodityType)
    {
        var compliance = await db.CarrierCompliances.FirstOrDefaultAsync(c => c.OrganizationId == carrierOrgId);
        if (compliance == null)
            return (ComplianceFlagStatus.Flagged, "No compliance record on file");

        var reasons = new List<string>();

        if (compliance.InsuranceExpiry < DateTime.UtcNow)
            reasons.Add("Insurance expired");

        if (compliance.McAuthorityStatus != AuthorityStatus.Active)
            reasons.Add($"MC authority is {compliance.McAuthorityStatus}");

        if (compliance.DotAuthorityStatus != AuthorityStatus.Active)
            reasons.Add($"DOT authority is {compliance.DotAuthorityStatus}");

        var equipment = JsonSerializer.Deserialize<List<string>>(compliance.ApprovedEquipmentTypes) ?? [];
        if (!equipment.Contains(equipmentType, StringComparer.OrdinalIgnoreCase))
            reasons.Add($"Equipment type '{equipmentType}' not authorized");

        var commodities = JsonSerializer.Deserialize<List<string>>(compliance.ApprovedCommodityTypes) ?? [];
        if (!commodities.Contains(commodityType, StringComparer.OrdinalIgnoreCase))
            reasons.Add($"Commodity type '{commodityType}' not authorized");

        return reasons.Count == 0
            ? (ComplianceFlagStatus.None, null)
            : (ComplianceFlagStatus.Flagged, string.Join("; ", reasons));
    }

    public async Task<List<ComplianceAlertDto>> GetExpiryAlertsAsync(Guid? brokerOrgId)
    {
        var query = db.CarrierCompliances
            .Include(c => c.Organization)
            .AsQueryable();

        if (brokerOrgId.HasValue)
        {
            var carrierIds = await db.Loads
                .Where(l => l.BrokerOrganizationId == brokerOrgId && l.CarrierOrganizationId != null)
                .Select(l => l.CarrierOrganizationId!.Value)
                .Distinct()
                .ToListAsync();
            query = query.Where(c => carrierIds.Contains(c.OrganizationId));
        }

        var threshold = DateTime.UtcNow.AddDays(30);
        var records = await query
            .Where(c => c.InsuranceExpiry <= threshold
                || c.McAuthorityStatus != AuthorityStatus.Active
                || c.DotAuthorityStatus != AuthorityStatus.Active)
            .ToListAsync();

        return records.Select(c => new ComplianceAlertDto(
            c.OrganizationId,
            c.Organization.Name,
            c.InsuranceExpiry,
            c.McAuthorityStatus.ToString(),
            c.DotAuthorityStatus.ToString(),
            c.InsuranceExpiry <= DateTime.UtcNow ? "Expired" :
            c.InsuranceExpiry <= threshold ? "Expiring soon" : "Authority issue"
        )).ToList();
    }
}

public record ComplianceAlertDto(
    Guid CarrierOrganizationId,
    string CarrierName,
    DateTime InsuranceExpiry,
    string McAuthorityStatus,
    string DotAuthorityStatus,
    string AlertType);

public class LoadService(AppDbContext db, CurrentUserService currentUser, ComplianceService compliance)
{
    private static readonly Dictionary<LoadStatus, LoadStatus[]> AllowedTransitions = new()
    {
        [LoadStatus.Posted] = [LoadStatus.CarrierAssigned],
        [LoadStatus.CarrierAssigned] = [LoadStatus.RateConfirmed],
        [LoadStatus.RateConfirmed] = [LoadStatus.Dispatched],
        [LoadStatus.Dispatched] = [LoadStatus.InTransit],
        [LoadStatus.InTransit] = [LoadStatus.Delivered],
        [LoadStatus.Delivered] = [LoadStatus.PodVerified],
        [LoadStatus.PodVerified] = [LoadStatus.InvoicedClosed]
    };

    public IQueryable<Load> ScopedLoads()
    {
        var query = db.Loads
            .Include(l => l.BrokerOrganization)
            .Include(l => l.CarrierOrganization)
            .Include(l => l.ShipperUser)
            .AsQueryable();

        return currentUser.AccountType switch
        {
            AccountType.Broker => query.Where(l => l.BrokerOrganizationId == currentUser.OrganizationId),
            AccountType.Carrier => query.Where(l => l.CarrierOrganizationId == currentUser.OrganizationId),
            AccountType.Shipper => query.Where(l => l.ShipperUserId == currentUser.UserId),
            _ => query.Where(_ => false)
        };
    }

    public async Task<Load?> GetScopedLoadAsync(Guid loadId) =>
        await ScopedLoads().FirstOrDefaultAsync(l => l.Id == loadId);

    public async Task<LoadDto> CreateLoadAsync(CreateLoadRequest request)
    {
        if (currentUser.AccountType != AccountType.Broker)
            throw new UnauthorizedAccessException("Only broker staff can create loads.");

        var shipper = await db.Users.FirstOrDefaultAsync(u =>
            u.Id == request.ShipperUserId && u.AccountType == AccountType.Shipper);
        if (shipper == null) throw new ArgumentException("Invalid shipper.");

        var refNum = $"LF-{DateTime.UtcNow:yyyy}-{await db.Loads.CountAsync() + 1:D4}";
        var load = new Load
        {
            Id = Guid.NewGuid(),
            ReferenceNumber = refNum,
            BrokerOrganizationId = currentUser.OrganizationId!.Value,
            ShipperUserId = request.ShipperUserId,
            Origin = request.Origin,
            Destination = request.Destination,
            EquipmentType = request.EquipmentType,
            CommodityType = request.CommodityType,
            WeightLbs = request.WeightLbs,
            PickupDate = request.PickupDate,
            DeliveryDate = request.DeliveryDate,
            CreatedByUserId = currentUser.UserId,
            Status = LoadStatus.Posted
        };

        db.Loads.Add(load);
        await AddAudit(load.Id, null, LoadStatus.Posted, "load.created", "Load posted to board");
        await db.SaveChangesAsync();
        return await MapLoadDto(load.Id);
    }

    public async Task<LoadDto> UpdateLoadAsync(Guid loadId, UpdateLoadRequest request)
    {
        var load = await GetScopedLoadAsync(loadId) ?? throw new KeyNotFoundException("Load not found.");
        if (load.Status != LoadStatus.Posted)
            throw new InvalidOperationException("Only posted loads can be edited.");

        load.Origin = request.Origin;
        load.Destination = request.Destination;
        load.EquipmentType = request.EquipmentType;
        load.CommodityType = request.CommodityType;
        load.WeightLbs = request.WeightLbs;
        load.PickupDate = request.PickupDate;
        load.DeliveryDate = request.DeliveryDate;

        await AddAudit(load.Id, load.Status, load.Status, "load.updated", "Load details updated");
        await db.SaveChangesAsync();
        return await MapLoadDto(load.Id);
    }

    public async Task<LoadDto> AssignCarrierAsync(Guid loadId, Guid carrierOrganizationId)
    {
        var load = await db.Loads.FirstAsync(l => l.Id == loadId && l.BrokerOrganizationId == currentUser.OrganizationId);
        if (load.Status != LoadStatus.Posted && load.Status != LoadStatus.CarrierAssigned)
            throw new InvalidOperationException("Cannot assign carrier at this status.");

        var carrier = await db.Organizations.FirstOrDefaultAsync(o =>
            o.Id == carrierOrganizationId && o.Type == AccountType.Carrier);
        if (carrier == null) throw new ArgumentException("Invalid carrier organization.");

        var (flag, reason) = await compliance.EvaluateCarrierAsync(
            carrierOrganizationId, load.EquipmentType, load.CommodityType);

        var from = load.Status;
        load.CarrierOrganizationId = carrierOrganizationId;
        load.Status = LoadStatus.CarrierAssigned;
        load.ComplianceFlag = flag;
        load.ComplianceFlagReason = reason;

        await AddAudit(load.Id, from, load.Status, "load.carrier_assigned",
            $"Assigned to {carrier.Name}" + (reason != null ? $" | Compliance: {reason}" : ""));
        await db.SaveChangesAsync();
        return await MapLoadDto(load.Id);
    }

    public async Task<LoadDto> OverrideComplianceAsync(Guid loadId, string? notes)
    {
        var load = await db.Loads.FirstAsync(l => l.Id == loadId && l.BrokerOrganizationId == currentUser.OrganizationId);
        if (load.ComplianceFlag != ComplianceFlagStatus.Flagged)
            throw new InvalidOperationException("Load is not compliance-flagged.");

        load.ComplianceFlag = ComplianceFlagStatus.Overridden;
        await AddAudit(load.Id, load.Status, load.Status, "load.compliance_overridden", notes ?? "Compliance override approved");
        await db.SaveChangesAsync();
        return await MapLoadDto(load.Id);
    }

    public async Task<LoadDto> ConfirmRateAsync(Guid loadId, ConfirmRateRequest request)
    {
        var load = await db.Loads
            .Include(l => l.RateConfirmations)
            .FirstAsync(l => l.Id == loadId && l.BrokerOrganizationId == currentUser.OrganizationId);

        if (load.Status != LoadStatus.CarrierAssigned && load.Status != LoadStatus.RateConfirmed)
            throw new InvalidOperationException("Rate can only be confirmed after carrier assignment.");

        if (request.BaseRate <= 0)
            throw new InvalidOperationException("Base rate must be greater than zero.");

        if (load.ComplianceFlag == ComplianceFlagStatus.Flagged)
        {
            var (flag, reason) = await compliance.EvaluateCarrierAsync(
                load.CarrierOrganizationId!.Value, load.EquipmentType, load.CommodityType);
            load.ComplianceFlag = flag;
            load.ComplianceFlagReason = reason;
            if (flag == ComplianceFlagStatus.Flagged)
                throw new InvalidOperationException($"Cannot confirm rate while compliance is flagged: {reason}");

            await AddAudit(load.Id, load.Status, load.Status, "load.compliance_resolved",
                "Carrier compliance record now passes validation");
        }

        var rc = load.RateConfirmations.FirstOrDefault();
        if (rc == null)
        {
            rc = new RateConfirmation { Id = Guid.NewGuid(), LoadId = load.Id, CurrentVersionNumber = 0 };
            db.RateConfirmations.Add(rc);
        }

        var versionNumber = rc.CurrentVersionNumber + 1;
        var version = new RateConfirmationVersion
        {
            Id = Guid.NewGuid(),
            RateConfirmationId = rc.Id,
            VersionNumber = versionNumber,
            BaseRate = request.BaseRate,
            AccessorialsJson = JsonSerializer.Serialize(request.Accessorials),
            Notes = request.Notes,
            ConfirmedByUserId = currentUser.UserId
        };
        db.RateConfirmationVersions.Add(version);
        rc.CurrentVersionNumber = versionNumber;
        load.ActiveRateConfirmationVersionId = version.Id;

        var from = load.Status;
        load.Status = LoadStatus.RateConfirmed;
        await AddAudit(load.Id, from, load.Status, "rate.confirmed", $"Rate v{versionNumber}: ${request.BaseRate}");
        await db.SaveChangesAsync();
        return await MapLoadDto(load.Id);
    }

    public async Task<LoadDto> TransitionStatusAsync(Guid loadId, LoadStatus targetStatus, string? notes)
    {
        var load = await GetScopedLoadAsync(loadId) ?? throw new KeyNotFoundException("Load not found.");

        if (targetStatus is LoadStatus.CarrierAssigned or LoadStatus.RateConfirmed)
            throw new InvalidOperationException("Use the carrier-assignment or rate-confirmation action for this transition.");

        if (load.ComplianceFlag == ComplianceFlagStatus.Flagged && targetStatus != LoadStatus.CarrierAssigned)
        {
            var allowedWhileFlagged = currentUser.AccountType == AccountType.Broker && targetStatus == load.Status;
            if (!allowedWhileFlagged)
                throw new InvalidOperationException("Compliance flag blocks progression past Carrier Assigned.");
        }

        if (!AllowedTransitions.TryGetValue(load.Status, out var allowed) || !allowed.Contains(targetStatus))
            throw new InvalidOperationException($"Invalid transition from {load.Status} to {targetStatus}.");

        ValidateTransitionActor(load, targetStatus);

        if (targetStatus == LoadStatus.PodVerified &&
            !await db.PodDocuments.AnyAsync(p => p.LoadId == load.Id))
            throw new InvalidOperationException("A POD document must be uploaded before POD verification.");

        var from = load.Status;
        load.Status = targetStatus;
        await AddAudit(load.Id, from, targetStatus, "load.status_changed", notes ?? $"{from} → {targetStatus}");
        await db.SaveChangesAsync();
        return await MapLoadDto(load.Id);
    }

    public async Task<LoadDto> AcceptLoadAsync(Guid loadId)
    {
        var load = await db.Loads.FirstOrDefaultAsync(l =>
            l.Id == loadId && l.CarrierOrganizationId == currentUser.OrganizationId);
        if (load == null) throw new KeyNotFoundException("Load not found or not assigned to your carrier.");

        if (load.Status != LoadStatus.CarrierAssigned)
            throw new InvalidOperationException("Load must be in Carrier Assigned status to accept.");

        await AddAudit(load.Id, load.Status, load.Status, "load.accepted", "Carrier accepted assignment");
        await db.SaveChangesAsync();
        return await MapLoadDto(load.Id);
    }

    public async Task<LoadDto> DeclineLoadAsync(Guid loadId, string? notes)
    {
        var load = await db.Loads.FirstOrDefaultAsync(l =>
            l.Id == loadId && l.CarrierOrganizationId == currentUser.OrganizationId);
        if (load == null) throw new KeyNotFoundException("Load not found or not assigned to your carrier.");
        if (load.Status != LoadStatus.CarrierAssigned)
            throw new InvalidOperationException("Only a pending carrier assignment can be declined.");

        var from = load.Status;
        load.CarrierOrganizationId = null;
        load.Status = LoadStatus.Posted;
        load.ComplianceFlag = ComplianceFlagStatus.None;
        load.ComplianceFlagReason = null;
        await AddAudit(load.Id, from, LoadStatus.Posted, "load.declined",
            notes ?? "Carrier declined assignment");
        await db.SaveChangesAsync();
        return await MapLoadDto(load.Id);
    }

    private void ValidateTransitionActor(Load load, LoadStatus target)
    {
        if (currentUser.AccountType == AccountType.Carrier)
        {
            var carrierStatuses = new[] { LoadStatus.InTransit, LoadStatus.Delivered };
            if (!carrierStatuses.Contains(target) && target != LoadStatus.Dispatched)
                throw new UnauthorizedAccessException("Carrier cannot set this status.");
        }
        else if (currentUser.AccountType == AccountType.Broker)
        {
            if (target is LoadStatus.InTransit or LoadStatus.Delivered)
                throw new UnauthorizedAccessException("Broker cannot set in-transit/delivered status.");
        }
    }

    private async Task AddAudit(Guid loadId, LoadStatus? from, LoadStatus? to, string action, string? details)
    {
        db.LoadAuditEntries.Add(new LoadAuditEntry
        {
            Id = Guid.NewGuid(),
            LoadId = loadId,
            FromStatus = from,
            ToStatus = to,
            Action = action,
            Details = details,
            UserId = currentUser.UserId,
            UserEmail = currentUser.Email
        });
        await Task.CompletedTask;
    }

    public async Task<LoadDto> MapLoadDto(Guid loadId)
    {
        var load = await db.Loads
            .Include(l => l.BrokerOrganization)
            .Include(l => l.CarrierOrganization)
            .Include(l => l.ShipperUser)
            .Include(l => l.ActiveRateConfirmationVersion)
            .Include(l => l.AuditEntries)
            .Include(l => l.PodDocuments)
            .FirstAsync(l => l.Id == loadId);
        return LoadDto.From(load);
    }

    public async Task<List<LoadAuditDto>> GetAuditTrailAsync(Guid loadId)
    {
        var load = await GetScopedLoadAsync(loadId) ?? throw new KeyNotFoundException();
        return load.AuditEntries.OrderBy(a => a.Timestamp).Select(LoadAuditDto.From).ToList();
    }
}

public record CreateLoadRequest(
    Guid ShipperUserId, string Origin, string Destination,
    string EquipmentType, string CommodityType, decimal WeightLbs,
    DateTime PickupDate, DateTime DeliveryDate);

public record UpdateLoadRequest(
    string Origin, string Destination, string EquipmentType, string CommodityType,
    decimal WeightLbs, DateTime PickupDate, DateTime DeliveryDate);

public record ConfirmRateRequest(decimal BaseRate, List<AccessorialDto> Accessorials, string? Notes);
public record AccessorialDto(string Name, decimal Amount);

public record LoadDto(
    Guid Id, string ReferenceNumber, string Status,
    string Origin, string Destination, string EquipmentType, string CommodityType,
    decimal WeightLbs, DateTime PickupDate, DateTime DeliveryDate,
    string ComplianceFlag, string? ComplianceFlagReason,
    Guid BrokerOrganizationId, string BrokerName,
    Guid? CarrierOrganizationId, string? CarrierName,
    Guid ShipperUserId, string ShipperName,
    RateVersionDto? ActiveRate, IReadOnlyList<LoadAuditDto> AuditTrail,
    IReadOnlyList<PodDto> Pods)
{
    public static LoadDto From(Load load) => new(
        load.Id, load.ReferenceNumber, load.Status.ToString(),
        load.Origin, load.Destination, load.EquipmentType, load.CommodityType,
        load.WeightLbs, load.PickupDate, load.DeliveryDate,
        load.ComplianceFlag.ToString(), load.ComplianceFlagReason,
        load.BrokerOrganizationId, load.BrokerOrganization.Name,
        load.CarrierOrganizationId, load.CarrierOrganization?.Name,
        load.ShipperUserId, load.ShipperUser.FullName,
        load.ActiveRateConfirmationVersion == null ? null : RateVersionDto.From(load.ActiveRateConfirmationVersion),
        load.AuditEntries.OrderBy(a => a.Timestamp).Select(LoadAuditDto.From).ToList(),
        load.PodDocuments.Select(PodDto.From).ToList());
}

public record RateVersionDto(int VersionNumber, decimal BaseRate, string AccessorialsJson, string? Notes, DateTime ConfirmedAt)
{
    public static RateVersionDto From(RateConfirmationVersion v) =>
        new(v.VersionNumber, v.BaseRate, v.AccessorialsJson, v.Notes, v.ConfirmedAt);
}

public record LoadAuditDto(Guid Id, string? FromStatus, string? ToStatus, string Action, string? Details, string UserEmail, DateTime Timestamp)
{
    public static LoadAuditDto From(LoadAuditEntry e) =>
        new(e.Id, e.FromStatus?.ToString(), e.ToStatus?.ToString(), e.Action, e.Details, e.UserEmail, e.Timestamp);
}

public record PodDto(Guid Id, string FileName, string ContentType, long FileSizeBytes, DateTime UploadedAt)
{
    public static PodDto From(PodDocument p) => new(p.Id, p.FileName, p.ContentType, p.FileSizeBytes, p.UploadedAt);
}
