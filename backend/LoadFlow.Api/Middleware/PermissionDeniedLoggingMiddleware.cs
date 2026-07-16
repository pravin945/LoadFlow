using Microsoft.AspNetCore.Authorization;
using LoadFlow.Api.Data;
using LoadFlow.Api.Models;

namespace LoadFlow.Api.Middleware;

public class PermissionDeniedLoggingMiddleware(RequestDelegate next, ILogger<PermissionDeniedLoggingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context, AppDbContext db)
    {
        await next(context);

        if (context.Response.StatusCode == StatusCodes.Status403Forbidden && context.User.Identity?.IsAuthenticated == true)
        {
            var endpoint = context.GetEndpoint();
            var authorizeData = endpoint?.Metadata.GetOrderedMetadata<IAuthorizeData>() ?? [];
            var permission = authorizeData
                .Select(a => a.Policy)
                .FirstOrDefault(p => p?.StartsWith(Authorization.PermissionPolicies.PolicyPrefix) == true)
                ?.Replace(Authorization.PermissionPolicies.PolicyPrefix, "") ?? "unknown";

            var userId = context.User.FindFirst(Authorization.ClaimTypes.UserId)?.Value;
            var email = context.User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value
                ?? context.User.FindFirst("email")?.Value;

            logger.LogWarning(
                "PERMISSION DENIED | User: {Email} ({UserId}) | Permission: {Permission} | {Method} {Path}",
                email, userId, permission, context.Request.Method, context.Request.Path);

            db.PermissionDeniedLogs.Add(new PermissionDeniedLog
            {
                Id = Guid.NewGuid(),
                UserId = userId != null ? Guid.Parse(userId) : null,
                UserEmail = email,
                Permission = permission,
                Path = context.Request.Path,
                Method = context.Request.Method
            });
            await db.SaveChangesAsync();
        }
    }
}
