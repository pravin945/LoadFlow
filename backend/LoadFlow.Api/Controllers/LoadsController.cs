using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using LoadFlow.Api.Authorization;
using LoadFlow.Api.Models;
using LoadFlow.Api.Services;

namespace LoadFlow.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class LoadsController(LoadService loadService) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = PermissionPolicies.LoadView)]
    public async Task<ActionResult<List<LoadDto>>> List(
        [FromQuery] string? status,
        [FromQuery] string? search,
        [FromQuery] string? origin,
        [FromQuery] string? destination,
        [FromQuery] bool? complianceFlagged)
    {
        var query = loadService.ScopedLoads();

        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<LoadStatus>(status, true, out var st))
            query = query.Where(l => l.Status == st);

        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(l =>
                l.ReferenceNumber.Contains(search) ||
                l.Origin.Contains(search) ||
                l.Destination.Contains(search));

        if (!string.IsNullOrWhiteSpace(origin))
            query = query.Where(l => l.Origin.Contains(origin));

        if (!string.IsNullOrWhiteSpace(destination))
            query = query.Where(l => l.Destination.Contains(destination));

        if (complianceFlagged == true)
            query = query.Where(l => l.ComplianceFlag == ComplianceFlagStatus.Flagged);

        var loads = await query.OrderByDescending(l => l.CreatedAt).ToListAsync();
        var dtos = new List<LoadDto>();
        foreach (var load in loads)
            dtos.Add(await loadService.MapLoadDto(load.Id));
        return Ok(dtos);
    }

    [HttpGet("{id:guid}")]
    [Authorize(Policy = PermissionPolicies.LoadView)]
    public async Task<ActionResult<LoadDto>> Get(Guid id)
    {
        var load = await loadService.GetScopedLoadAsync(id);
        if (load == null) return NotFound();
        return Ok(await loadService.MapLoadDto(id));
    }

    [HttpPost]
    [Authorize(Policy = PermissionPolicies.LoadCreate)]
    public async Task<ActionResult<LoadDto>> Create(CreateLoadRequest request)
    {
        try
        {
            return Ok(await loadService.CreateLoadAsync(request));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = PermissionPolicies.LoadCreate)]
    public async Task<ActionResult<LoadDto>> Update(Guid id, UpdateLoadRequest request)
    {
        try
        {
            return Ok(await loadService.UpdateLoadAsync(id, request));
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("{id:guid}/assign-carrier")]
    [Authorize(Policy = PermissionPolicies.LoadAssignCarrier)]
    public async Task<ActionResult<LoadDto>> AssignCarrier(Guid id, [FromBody] AssignCarrierRequest request)
    {
        try
        {
            return Ok(await loadService.AssignCarrierAsync(id, request.CarrierOrganizationId));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("{id:guid}/override-compliance")]
    [Authorize(Policy = PermissionPolicies.LoadOverrideCompliance)]
    public async Task<ActionResult<LoadDto>> OverrideCompliance(Guid id, [FromBody] OverrideComplianceRequest? request)
    {
        try
        {
            return Ok(await loadService.OverrideComplianceAsync(id, request?.Notes));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("{id:guid}/confirm-rate")]
    [Authorize(Policy = PermissionPolicies.RateConfirm)]
    public async Task<ActionResult<LoadDto>> ConfirmRate(Guid id, ConfirmRateRequest request)
    {
        try
        {
            return Ok(await loadService.ConfirmRateAsync(id, request));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("{id:guid}/transition")]
    [Authorize(Policy = PermissionPolicies.LoadUpdateStatus)]
    public async Task<ActionResult<LoadDto>> Transition(Guid id, TransitionRequest request)
    {
        try
        {
            if (!Enum.TryParse<LoadStatus>(request.TargetStatus, true, out var target))
                return BadRequest(new { message = "Invalid status." });
            return Ok(await loadService.TransitionStatusAsync(id, target, request.Notes));
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (Exception ex) when (ex is InvalidOperationException or UnauthorizedAccessException)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("{id:guid}/accept")]
    [Authorize(Policy = PermissionPolicies.LoadUpdateStatus)]
    public async Task<ActionResult<LoadDto>> Accept(Guid id)
    {
        try
        {
            return Ok(await loadService.AcceptLoadAsync(id));
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("{id:guid}/decline")]
    [Authorize(Policy = PermissionPolicies.LoadUpdateStatus)]
    public async Task<ActionResult<LoadDto>> Decline(Guid id, [FromBody] DeclineLoadRequest? request)
    {
        try
        {
            return Ok(await loadService.DeclineLoadAsync(id, request?.Notes));
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("{id:guid}/audit")]
    [Authorize(Policy = PermissionPolicies.LoadView)]
    public async Task<ActionResult<List<LoadAuditDto>>> Audit(Guid id)
    {
        try
        {
            return Ok(await loadService.GetAuditTrailAsync(id));
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpGet("dashboard/alerts")]
    [Authorize(Policy = PermissionPolicies.LoadView)]
    public async Task<ActionResult<DashboardAlertsDto>> DashboardAlerts([FromServices] ComplianceService compliance, [FromServices] CurrentUserService currentUser)
    {
        var loads = loadService.ScopedLoads();
        var flagged = await loads.CountAsync(l => l.ComplianceFlag == ComplianceFlagStatus.Flagged);
        var active = await loads.CountAsync(l => l.Status != LoadStatus.InvoicedClosed);
        var complianceAlerts = currentUser.AccountType == AccountType.Broker
            ? await compliance.GetExpiryAlertsAsync(currentUser.OrganizationId)
            : [];

        return Ok(new DashboardAlertsDto(active, flagged, complianceAlerts));
    }
}

public record AssignCarrierRequest(Guid CarrierOrganizationId);
public record OverrideComplianceRequest(string? Notes);
public record TransitionRequest(string TargetStatus, string? Notes);
public record DeclineLoadRequest(string? Notes);
public record DashboardAlertsDto(int ActiveLoads, int ComplianceFlaggedLoads, List<ComplianceAlertDto> ComplianceAlerts);
