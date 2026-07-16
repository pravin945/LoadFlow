using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using LoadFlow.Api.Authorization;
using LoadFlow.Api.Models;
using LoadFlow.Api.Services;

namespace LoadFlow.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController(AuthService authService) : ControllerBase
{
    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<AuthResponse>> Login(LoginRequest request)
    {
        var result = await authService.LoginAsync(request.Email, request.Password);
        if (result == null) return Unauthorized(new { message = "Invalid credentials." });
        var profile = await authService.GetProfileAsync(result.Value.user.Id);
        return Ok(new AuthResponse(result.Value.token, profile));
    }

    [HttpPost("register/org")]
    [AllowAnonymous]
    public async Task<ActionResult<AuthResponse>> RegisterOrg(RegisterOrgRequest request)
    {
        try
        {
            var result = await authService.RegisterOrgAdminAsync(request);
            var profile = await authService.GetProfileAsync(result!.Value.user.Id);
            return Ok(new AuthResponse(result.Value.token, profile));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("register/shipper")]
    [AllowAnonymous]
    public async Task<ActionResult<AuthResponse>> RegisterShipper(RegisterShipperRequest request)
    {
        try
        {
            var result = await authService.RegisterShipperAsync(request);
            var profile = await authService.GetProfileAsync(result!.Value.user.Id);
            return Ok(new AuthResponse(result.Value.token, profile));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<ActionResult<UserProfileDto>> Me()
    {
        var userId = Guid.Parse(User.FindFirst(ClaimTypes.UserId)!.Value);
        return Ok(await authService.GetProfileAsync(userId));
    }

    [HttpGet("permissions")]
    [AllowAnonymous]
    public ActionResult<object> GetPermissionCatalog() =>
        Ok(Models.PermissionCatalog.All.Select(p => new { key = p, description = Models.PermissionCatalog.Descriptions[p] }));
}
