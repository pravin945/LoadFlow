using Microsoft.AspNetCore.Authorization;
using LoadFlow.Api.Models;

namespace LoadFlow.Api.Authorization;

public class PermissionRequirement(string permission) : IAuthorizationRequirement
{
    public string Permission { get; } = permission;
}

public class PermissionAuthorizationHandler(ILogger<PermissionAuthorizationHandler> logger)
    : AuthorizationHandler<PermissionRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PermissionRequirement requirement)
    {
        if (context.User.Identity?.IsAuthenticated != true) return Task.CompletedTask;

        var accountType = context.User.FindFirst(ClaimTypes.AccountType)?.Value;
        if (accountType == AccountType.Shipper.ToString())
        {
            if (requirement.Permission == PermissionCatalog.LoadView)
            {
                context.Succeed(requirement);
            }
            return Task.CompletedTask;
        }

        var isAdmin = context.User.FindFirst(ClaimTypes.IsOrgAdmin)?.Value == "true";
        if (isAdmin)
        {
            context.Succeed(requirement);
            return Task.CompletedTask;
        }

        var permissions = context.User.FindAll(ClaimTypes.Permissions).Select(c => c.Value).ToHashSet();
        if (permissions.Contains(requirement.Permission))
        {
            context.Succeed(requirement);
        }
        else
        {
            logger.LogWarning(
                "Permission denied: user {Email} lacks {Permission}",
                context.User.Identity?.Name,
                requirement.Permission);
        }

        return Task.CompletedTask;
    }
}
