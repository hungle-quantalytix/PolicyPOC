using System.Security.Claims;
using PolicyPOC.Attributes;
using PolicyPOC.Services;

namespace PolicyPOC.Authorization;

/// <summary>
/// Middleware that evaluates permissions using the unified Permission table.
/// Checks access in priority order: Policy > Role > User
/// </summary>
public class QtxPolicyMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<QtxPolicyMiddleware> _logger;

    public QtxPolicyMiddleware(
        RequestDelegate next,
        ILogger<QtxPolicyMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(
        HttpContext context, 
        IPermissionService permissionService,
        ISecurityContextService securityContextService)
    {
        var endpoint = context.GetEndpoint();
        if (endpoint == null)
        {
            await _next(context);
            return;
        }

        var policyAttribute = endpoint.Metadata.GetMetadata<PolicyAttribute>();
        if (policyAttribute == null)
        {
            await _next(context);
            return;
        }

        var user = context.User;

        if (!user.Identity?.IsAuthenticated ?? true)
        {
            _logger.LogWarning(
                "User is not authenticated - Request: {Method} {Path}",
                context.Request.Method, context.Request.Path);
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(new { error = "Authentication required" });
            return;
        }

        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
        var userEmail = user.FindFirstValue(ClaimTypes.Email);
        var userRoles = user.FindAll(ClaimTypes.Role).Select(c => c.Value).ToList();

        // Clear any previous security context (in case of request reuse)
        securityContextService.Clear();

        _logger.LogInformation(
            "Evaluating permission for user {UserId} ({UserEmail}) with roles [{Roles}] - Resource: {Resource}, Action: {Action}",
            userId, userEmail, string.Join(", ", userRoles),
            policyAttribute.ResourceName, policyAttribute.Action);

        // Get resourceId from route if available (for specific resource checks)
        var resourceId = context.Request.RouteValues["id"]?.ToString();

        // Evaluate resource-level permission using the unified Permission table
        var result = await permissionService.EvaluateResourceAccessAsync(
            policyAttribute.ResourceName,
            resourceId,
            policyAttribute.Action ?? "read",
            user);

        if (!result.IsAllowed)
        {
            _logger.LogWarning(
                "Access DENIED for user {UserId} to {Resource}:{Action} - {Reason}",
                userId, policyAttribute.ResourceName, policyAttribute.Action, result.DeniedReason);
            
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsJsonAsync(new 
            { 
                error = "Access denied",
                message = result.DeniedReason
            });
            return;
        }

        _logger.LogInformation(
            "Access GRANTED for user {UserId} to {Resource}:{Action} via {SubjectType}:{SubjectId}",
            userId, policyAttribute.ResourceName, policyAttribute.Action,
            result.MatchedSubjectType, result.MatchedSubjectId);

        // Add RLS rules from policy evaluation to security context
        foreach (var rlsRule in result.RlsRules)
        {
            securityContextService.AddRowLevelSecurityRule(rlsRule);
            _logger.LogDebug(
                "Adding RLS rule: {Field} {Operator} {Value}",
                rlsRule.ResourceField, rlsRule.Operator, rlsRule.Value);
        }

        // Evaluate field-level permissions
        var fieldRules = await permissionService.EvaluateFieldAccessAsync(
            policyAttribute.ResourceName,
            resourceId,
            policyAttribute.Action ?? "read",
            user);

        foreach (var fieldRule in fieldRules)
        {
            securityContextService.AddFieldAccess(fieldRule);
            _logger.LogDebug(
                "Field {FieldName}: Access level = {AccessLevel}",
                fieldRule.FieldName, fieldRule.AccessLevel);
        }

        // Continue to next middleware/endpoint
        await _next(context);
    }
}
