using System.Security.Claims;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PolicyPOC.Attributes;
using PolicyPOC.Data;
using PolicyPOC.Models;
using PolicyPOC.Services;

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

    public async Task InvokeAsync(HttpContext context, ApplicationDbContext dbContext, ISecurityContextService securityContextService)
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


        bool policyMatched = false;
        
        // Track row-level security rules for matched policies
        var tempRlsRules = new List<RowLevelSecurityRule>();
        
        // OR logic between policies - if ANY policy matches, access is granted
        foreach (var policyResource in applicablePolicies)
        {
            _logger.LogInformation(
                "Evaluating policy for resource {Resource} and action {Action}: {Policy}",
                policyResource.ResourceName, policyResource.Action, policyResource.Policy.Description);

            _logger.LogInformation(policyResource.Policy.PolicyData);

            // Handle PII policies (policies with ResourceColumns)
            if (!string.IsNullOrEmpty(policyResource.ResourceColumns))
            {
                var columns = policyResource.ResourceColumns.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                securityContextService.AddPiiPolicyRule(new PiiPolicyRule
                {
                    ResourceName = policyResource.ResourceName ?? string.Empty,
                    Columns = columns,
                    Action = policyResource.Action ?? string.Empty
                });
                _logger.LogInformation(
                    "PII policy detected for resource {Resource} - Columns: [{Columns}]",
                    policyResource.ResourceName, string.Join(", ", columns));
                continue;
            }

            PolicyRule? policyRules = null;
            try
            {
                policyRules = JsonSerializer.Deserialize<PolicyRule>(policyResource.Policy.PolicyData);
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Failed to deserialize policy data for policy {PolicyId}", policyResource.PolicyId);
                continue;
            }

            if (policyRules == null)
            {
                _logger.LogError("Failed to deserialize policy data for policy {PolicyId}", policyResource.PolicyId);
                continue;
            }

            // Create a temporary security context for this policy evaluation
            var tempSecurityContext = new SecurityContextService();
            
            // Evaluate the policy rules
            bool policyResult = await EvaluateRuleAsync(policyRules, user, dbContext, tempSecurityContext);
            
            _logger.LogInformation("Policy evaluation result: {Result}", policyResult);
            
            // If this policy matches (user conditions pass), we need to:
            // 1. Grant access at middleware level
            // 2. Add any row-level security rules from this policy
            if (policyResult)
            {
                policyMatched = true;
                
                // Add row-level security rules from this matched policy
                foreach (var rlsRule in tempSecurityContext.SecurityContext.RowLevelSecurityRules)
                {
                    tempRlsRules.Add(rlsRule);
                    _logger.LogInformation(
                        "Adding RLS rule from matched policy: {Field} {Operator} {Value}",
                        rlsRule.ResourceField, rlsRule.Operator, rlsRule.Value);
                }
                
                // Note: We continue checking other policies for OR logic
                // Multiple policies may match and contribute RLS rules
            }
        }
        
        // Add all collected RLS rules to the security context
        // These will be combined with OR logic at the query level
        foreach (var rule in tempRlsRules)
        {
            securityContextService.AddRowLevelSecurityRule(rule);
        }
        
        bool? isAllowed = policyMatched ? true : null;

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

    private async Task<bool> EvaluatePolicyGroupAsync(PolicyGroup policyGroup, ClaimsPrincipal user, ApplicationDbContext dbContext, ISecurityContextService securityContextService)
    {
        bool isAllowed = false;
        if (policyGroup.Condition == null)
            throw new InvalidOperationException("PolicyGroup.Condition must not be null.");
        var isAnd = policyGroup.Condition.Trim().ToUpperInvariant() switch
        {
            "AND" => true,
            "OR" => false,
            _ => throw new InvalidOperationException($"Unsupported condition for PolicyGroup: {policyGroup.Condition}"),
        };
        isAllowed = isAnd;

        foreach (var rule in policyGroup.Rules)
        {
            if (rule is ComparisonRule comparisonRule)
            {
                bool result = await EvaluateComparisonRuleAsync(comparisonRule, user, dbContext, securityContextService);

                if (isAnd && !result)
                    return false; // AND short-circuit

                if (!isAnd && result)
                    return true; // OR short-circuit

                if (isAnd) isAllowed = isAllowed && result;
                else isAllowed = isAllowed || result;    
            }
            else if (rule is PolicyGroup nestedGroup)
            {
                bool result = await EvaluatePolicyGroupAsync(nestedGroup, user, dbContext, securityContextService);

                if (isAnd && !result)
                    return false; // AND short-circuit

                if (!isAnd && result)
                    return true; // OR short-circuit

                if (isAnd) isAllowed = isAllowed && result;
                else isAllowed = isAllowed || result;     
            }
            else
            {
                _logger.LogError("Unknown rule type in PolicyGroup: {Rule}", rule.GetType().Name);
                return false;
            }
        }

        return isAllowed;
    }

    private async Task<bool> EvaluateComparisonRuleAsync(ComparisonRule comparisonRule, ClaimsPrincipal user, ApplicationDbContext dbContext, ISecurityContextService securityContextService)
    {
        _logger.LogInformation("Comparison rule found: {Rule}", comparisonRule.Field);
        
        // Check if field starts with "user" - Standard policy rule
        if (comparisonRule.Field.StartsWith("user.", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogInformation("Standard policy rule detected - Field: {Field}", comparisonRule.Field);
            var fieldValue = await GetUserFieldValue(comparisonRule.Field, user, dbContext);
        
            return comparisonRule.Operator.ToLower() switch
            {
                "=" or "equals" => string.Equals(fieldValue, comparisonRule.Value, StringComparison.OrdinalIgnoreCase),
                "!=" or "notequals" or "not equals" => !string.Equals(fieldValue, comparisonRule.Value, StringComparison.OrdinalIgnoreCase),
                _ => false
            };
        }
        
        // Check if field starts with "resource" - Row-level security
        if (comparisonRule.Field.StartsWith("resource.", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogInformation(
                "Row-level security rule detected - Field: {Field}, Operator: {Operator}, Value: {Value}",
                comparisonRule.Field, comparisonRule.Operator, comparisonRule.Value);
            
            // Extract the resource field name (remove "resource." prefix)
            var resourceField = comparisonRule.Field.Substring("resource.".Length);
            
            // Resolve value if it references a user field
            var resolvedValue = comparisonRule.Value;
            
            // Support both "user.field" and "${user.field}" syntax
            if (comparisonRule.Value.StartsWith("${user.", StringComparison.OrdinalIgnoreCase) && 
                comparisonRule.Value.EndsWith("}"))
            {
                // Extract field name from ${user.field}
                var userField = comparisonRule.Value.Substring(2, comparisonRule.Value.Length - 3); // Remove "${ and }"
                resolvedValue = await GetUserFieldValue(userField, user, dbContext);
            }
            else if (comparisonRule.Value.StartsWith("user.", StringComparison.OrdinalIgnoreCase))
            {
                resolvedValue = await GetUserFieldValue(comparisonRule.Value, user, dbContext);
            }
            
            // Add to security context for controller/repository to use
            securityContextService.AddRowLevelSecurityRule(new RowLevelSecurityRule
            {
                ResourceField = resourceField,
                Operator = comparisonRule.Operator,
                Value = resolvedValue
            });
            
            // Row-level security rules don't block access at middleware level
            // They are applied at the data access layer
            return true;
        }
        
        // If field doesn't start with user or resource, log warning and deny
        _logger.LogWarning(
            "Unknown field prefix in comparison rule: {Field}. Expected 'user.' or 'resource.'",
            comparisonRule.Field);
        return false;
    }

    private async Task<bool> EvaluateRuleAsync(PolicyRule rule, ClaimsPrincipal user, ApplicationDbContext dbContext, ISecurityContextService securityContextService)
    {
        return rule switch
        {
            ComparisonRule comparison => await EvaluateComparisonRuleAsync(comparison, user, dbContext, securityContextService),
            PolicyGroup group => await EvaluatePolicyGroupAsync(group, user, dbContext, securityContextService),
            _ => false
        };
    }

    private async Task<string> GetUserFieldValue(string field, ClaimsPrincipal user, ApplicationDbContext dbContext)
    {
        // Field format: "user.{property}"
        var fieldName = field.StartsWith("user.", StringComparison.OrdinalIgnoreCase) 
            ? field.Substring("user.".Length).ToLower()
            : field.ToLower();

        // Handle common claims
        switch (fieldName)
        {
            case "id":
                return user.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
            
            case "email":
                return user.FindFirstValue(ClaimTypes.Email) ?? string.Empty;
            
            case "role":
            case "roles":
                var roles = user.FindAll(ClaimTypes.Role).Select(c => c.Value).ToList();
                return string.Join(",", roles);
            
            case "department":
                // Fetch department from database
                var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
                if (!string.IsNullOrEmpty(userId))
                {
                    var appUser = await dbContext.Users.FirstOrDefaultAsync(u => u.Id == userId);
                    return appUser?.Department ?? string.Empty;
                }
                return string.Empty;
            
            default:
                // Try to find as a custom claim
                var claim = user.FindFirstValue(fieldName);
                if (!string.IsNullOrEmpty(claim))
                    return claim;
                
                // If not found in claims, try to get from database
                var userIdForDb = user.FindFirstValue(ClaimTypes.NameIdentifier);
                if (!string.IsNullOrEmpty(userIdForDb))
                {
                    var appUser = await dbContext.Users.FirstOrDefaultAsync(u => u.Id == userIdForDb);
                    if (appUser != null)
                    {
                        var property = typeof(ApplicationUser).GetProperty(fieldName, 
                            System.Reflection.BindingFlags.IgnoreCase | 
                            System.Reflection.BindingFlags.Public | 
                            System.Reflection.BindingFlags.Instance);
                        
                        if (property != null)
                        {
                            var value = property.GetValue(appUser);
                            return value?.ToString() ?? string.Empty;
                        }
                    }
                }
                
                _logger.LogWarning("Unable to resolve user field: {Field}", field);
                return string.Empty;
        }
    }
}
