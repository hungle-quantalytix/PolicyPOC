using System.Security.Claims;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PolicyPOC.Attributes;
using PolicyPOC.Data;
using PolicyPOC.Models;

namespace PolicyPOC.Authorization;

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

    public async Task InvokeAsync(HttpContext context, ApplicationDbContext dbContext)
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

        _logger.LogInformation(
            "Evaluating dynamic policy for user {UserId} ({UserEmail}) with roles [{Roles}] - Resource: {Resource}, Action: {Action}",
            userId, userEmail, string.Join(", ", userRoles), 
            policyAttribute.ResourceName, policyAttribute.Action);

        // Query policies from database dynamically
        var applicablePolicies = await dbContext.PolicyResources
            .Include(pr => pr.Policy)
            .Where(pr => pr.ResourceName == policyAttribute.ResourceName 
                      && pr.Action == policyAttribute.Action)
            .ToListAsync();

        if (!applicablePolicies.Any())
        {
            _logger.LogInformation(
                "No policies found for resource {Resource} and action {Action} - Access granted",
                policyAttribute.ResourceName, policyAttribute.Action);
            
            // If no policies are found, continue to next middleware/endpoint
            await _next(context);
            return;
        }

        // For current state, no care about allow/deny policies, just check if any policies are found


        bool? isAllowed = null;
        foreach (var policy in applicablePolicies)
        {
            _logger.LogInformation(
                "Policy found for resource {Resource} and action {Action}: {Policy}",
                policy.ResourceName, policy.Action, policy.Policy.Description);

            _logger.LogInformation(policy.Policy.PolicyData);

            PolicyRule? policyRules = null;
            try
            {
                policyRules = JsonSerializer.Deserialize<PolicyRule>(policy.Policy.PolicyData);
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Failed to deserialize policy data for policy {PolicyId}", policy.PolicyId);
            }

            if (policyRules == null)
            {
                _logger.LogError("Failed to deserialize policy data for policy {PolicyId}", policy.PolicyId);
                continue;
            }

            if (policyRules is PolicyGroup policyGroup)
            {
                foreach (var rule in policyGroup.Rules)
                {
                    if (rule is ComparisonRule comparisonRule)
                    {
                        _logger.LogInformation("Comparison rule found: {Rule}", comparisonRule.Field);
                    }
                }
            }
        }

        if (isAllowed == true)
        {
            _logger.LogInformation(
                "Access granted for user {UserId} to {Resource}:{Action}",
                userId, policyAttribute.ResourceName, policyAttribute.Action);
            await _next(context);
            return;
        }
        else if (isAllowed == false || isAllowed == null) // For now if not match deny, later pass to permission middleware
        {
            _logger.LogInformation(
                "Access denied for user {UserId} to {Resource}:{Action}",
                userId, policyAttribute.ResourceName, policyAttribute.Action);
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsJsonAsync(new { error = "Access denied" });
            return;
        }


        // // Evaluate policies
        // bool hasAllowPolicy = false;
        // bool hasDenyPolicy = false;

        // foreach (var policyResource in applicablePolicies)
        // {
        //     _logger.LogInformation(
        //         "Evaluating policy {PolicyId}: Effect={Effect}, Resource={Resource}, Action={Action}",
        //         policyResource.PolicyId, policyResource.Effect, 
        //         policyResource.ResourceName, policyResource.Action);

        //     if (policyResource.Effect?.ToLower() == "allow")
        //     {
        //         hasAllowPolicy = true;
        //     }
        //     else if (policyResource.Effect?.ToLower() == "deny")
        //     {
        //         hasDenyPolicy = true;
        //     }
        // }

        // // Deny takes precedence over allow
        // if (hasDenyPolicy)
        // {
        //     _logger.LogWarning(
        //         "Access denied for user {UserId} to {Resource}:{Action} - Explicit deny policy found",
        //         userId, policyAttribute.ResourceName, policyAttribute.Action);
        //     context.Response.StatusCode = StatusCodes.Status403Forbidden;
        //     await context.Response.WriteAsJsonAsync(new 
        //     { 
        //         error = "Access denied - explicit deny policy",
        //         resource = policyAttribute.ResourceName,
        //         action = policyAttribute.Action
        //     });
        //     return;
        // }

        // if (!hasAllowPolicy)
        // {
        //     _logger.LogWarning(
        //         "Access denied for user {UserId} to {Resource}:{Action} - No allow policy found",
        //         userId, policyAttribute.ResourceName, policyAttribute.Action);
        //     context.Response.StatusCode = StatusCodes.Status403Forbidden;
        //     await context.Response.WriteAsJsonAsync(new 
        //     { 
        //         error = "Access denied - no allow policy found",
        //         resource = policyAttribute.ResourceName,
        //         action = policyAttribute.Action
        //     });
        //     return;
        // }

        _logger.LogInformation(
            "Access granted for user {UserId} to {Resource}:{Action}",
            userId, policyAttribute.ResourceName, policyAttribute.Action);

        // Continue to next middleware/endpoint
        await _next(context);
    }
}

