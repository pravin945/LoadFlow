using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using LoadFlow.Api.Authorization;
using LoadFlow.Api.Data;
using LoadFlow.Api.Models;
using LoadFlow.Api.Services;

namespace LoadFlow.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class StaffController(StaffService staffService) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = PermissionPolicies.StaffManage)]
    public async Task<ActionResult<List<StaffMemberDto>>> ListStaff() =>
        Ok(await staffService.ListStaffAsync());

    [HttpPost]
    [Authorize(Policy = PermissionPolicies.StaffManage)]
    public async Task<ActionResult<StaffMemberDto>> CreateStaff(CreateStaffRequest request)
    {
        try
        {
            return Ok(await staffService.CreateStaffAsync(request));
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("roles")]
    [Authorize(Policy = PermissionPolicies.StaffManage)]
    public async Task<ActionResult<List<RoleDto>>> ListRoles() =>
        Ok(await staffService.ListRolesAsync());

    [HttpPost("roles")]
    [Authorize(Policy = PermissionPolicies.StaffManage)]
    public async Task<ActionResult<RoleDto>> CreateRole(CreateRoleRequest request)
    {
        try
        {
            return Ok(await staffService.CreateRoleAsync(request));
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPut("roles/{roleId:guid}")]
    [Authorize(Policy = PermissionPolicies.StaffManage)]
    public async Task<ActionResult<RoleDto>> UpdateRole(Guid roleId, UpdateRoleRequest request)
    {
        try
        {
            return Ok(await staffService.UpdateRoleAsync(roleId, request));
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("shippers")]
    [Authorize(Policy = PermissionPolicies.LoadCreate)]
    public async Task<ActionResult<List<ShipperLookupDto>>> ListShippers() =>
        Ok(await staffService.ListShippersAsync());

    [HttpGet("carriers")]
    [Authorize(Policy = PermissionPolicies.LoadAssignCarrier)]
    public async Task<ActionResult<List<CarrierLookupDto>>> ListCarriers() =>
        Ok(await staffService.ListCarriersAsync());
}

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ComplianceController(ComplianceRecordService complianceService, ComplianceService alerts) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = PermissionPolicies.ComplianceManage)]
    public async Task<ActionResult<ComplianceRecordDto>> Get([FromQuery] Guid? carrierOrganizationId)
    {
        var record = await complianceService.GetAsync(carrierOrganizationId);
        if (record == null) return NotFound();
        return Ok(record);
    }

    [HttpPut]
    [Authorize(Policy = PermissionPolicies.ComplianceManage)]
    public async Task<ActionResult<ComplianceRecordDto>> Upsert(UpsertComplianceRequest request)
    {
        try
        {
            return Ok(await complianceService.UpsertAsync(request));
        }
        catch (Exception ex) when (ex is ArgumentException or UnauthorizedAccessException)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("alerts")]
    [Authorize(Policy = PermissionPolicies.LoadView)]
    public async Task<ActionResult<List<ComplianceAlertDto>>> Alerts([FromServices] CurrentUserService currentUser)
    {
        var brokerOrgId = currentUser.AccountType == Models.AccountType.Broker ? currentUser.OrganizationId : null;
        return Ok(await alerts.GetExpiryAlertsAsync(brokerOrgId));
    }
}

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class PodsController(PodService podService) : ControllerBase
{
    [HttpPost("{loadId:guid}")]
    [Authorize(Policy = PermissionPolicies.PodUpload)]
    [RequestSizeLimit(10 * 1024 * 1024)]
    public async Task<ActionResult<PodDto>> Upload(Guid loadId, IFormFile file)
    {
        try
        {
            return Ok(await podService.UploadAsync(loadId, file));
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

    [HttpGet("{loadId:guid}/{podId:guid}")]
    [Authorize(Policy = PermissionPolicies.LoadView)]
    public async Task<IActionResult> Download(Guid loadId, Guid podId)
    {
        var result = await podService.DownloadAsync(loadId, podId);
        if (result == null) return NotFound();
        var (stream, contentType, fileName) = result.Value;
        return File(stream, contentType, fileName);
    }
}

[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = PermissionPolicies.StaffManage)]
public class AuditLogsController(AppDbContext db, CurrentUserService currentUser) : ControllerBase
{
    [HttpGet("permission-denied")]
    public async Task<ActionResult<List<object>>> PermissionDenied([FromQuery] int take = 50)
    {
        var logs = await db.PermissionDeniedLogs
            .Where(l => l.UserId != null && db.Users.Any(u =>
                u.Id == l.UserId && u.OrganizationId == currentUser.OrganizationId))
            .OrderByDescending(l => l.Timestamp)
            .Take(Math.Min(take, 200))
            .Select(l => new
            {
                l.Id,
                l.UserEmail,
                l.Permission,
                l.Method,
                l.Path,
                l.Timestamp
            })
            .ToListAsync();
        return Ok(logs);
    }
}
