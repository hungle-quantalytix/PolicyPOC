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

        // Query policies from database dynamically - now using Resource with direct policy references
        var resource = await dbContext.Resources
            .Include(r => r.ReadPolicies)
            .Include(r => r.WritePolicies)
            .FirstOrDefaultAsync(r => r.ResourceName == policyAttribute.ResourceName);

        // Get applicable policies based on action type
        var applicablePolicies = policyAttribute.Action?.ToLower() switch
        {
            "read" => resource?.ReadPolicies?.ToList() ?? new List<Policy>(),
            "write" => resource?.WritePolicies?.ToList() ?? new List<Policy>(),
            _ => new List<Policy>()
        };

        // DENY BY DEFAULT: If no policies are found, access is denied
        // Previously: allowed access when no policies existed (commented out below)
        // if (!applicablePolicies.Any())
        // {
        //     _logger.LogInformation(
        //         "No policies found for resource {Resource} and action {Action} - Access granted",
        //         policyAttribute.ResourceName, policyAttribute.Action);
        //
        //     // If no policies are found, continue to next middleware/endpoint
        //     await _next(context);
        //     return;
        // }

        if (!applicablePolicies.Any())
        {
            _logger.LogWarning(
                "No policies found for resource {Resource} and action {Action} - Access DENIED (deny by default)",
                policyAttribute.ResourceName, policyAttribute.Action);

            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsJsonAsync(new
            {
                error = "Access denied",
                message = $"No policies configured for resource '{policyAttribute.ResourceName}' with action '{policyAttribute.Action}'. Access is denied by default."
            });
            return;
        }

        // For current state, no care about allow/deny policies, just check if any policies are found


        bool policyMatched = false;
        
        // Track row-level security rules for matched policies
        var tempRlsRules = new List<RowLevelSecurityRule>();
        
        // OR logic between policies - if ANY policy matches, access is granted
        foreach (var policy in applicablePolicies)
        {
            _logger.LogInformation(
                "Evaluating policy for resource {Resource} and action {Action}: {Policy}",
                policyAttribute.ResourceName, policyAttribute.Action, policy.Description);

            _logger.LogInformation(policy.PolicyData);

            PolicyRule? policyRules = null;
            try
            {
                policyRules = JsonSerializer.Deserialize<PolicyRule>(policy.PolicyData);
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Failed to deserialize policy data for policy {PolicyId}", policy.Id);
                continue;
            }

            if (policyRules == null)
            {
                _logger.LogError("Failed to deserialize policy data for policy {PolicyId}", policy.Id);
                continue;
            }

            // Create a temporary security context for this policy evaluation
            var tempSecurityContext = new SecurityContextService();
            
            // Evaluate the policy rules
            bool policyResult = await EvaluateRuleAsync(policyRules, user, dbContext, tempSecurityContext);
            
            _logger.LogInformation("Policy evaluation result: {Result}", policyResult);
            
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
            }
        }
        
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
            
            // Evaluate field-level policies after resource access is granted
            await EvaluateFieldLevelPoliciesAsync(
                resource!, 
                policyAttribute.Action!, 
                user, 
                dbContext, 
                securityContextService);
            
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

    /// <summary>
    /// Evaluates field-level policies and populates the security context with field access rules.
    /// 
    /// Logic:
    /// - No policies on field: Full access (MaskFormat is ignored)
    /// - Has policies + policy matched: Full access
    /// - Has policies + no match: Apply MaskFormat (null=hidden, ""=empty, value=masked)
    /// </summary>
    private async Task EvaluateFieldLevelPoliciesAsync(
        Resource resource,
        string action,
        ClaimsPrincipal user,
        ApplicationDbContext dbContext,
        ISecurityContextService securityContextService)
    {
        // Load fields with their policies
        var fields = await dbContext.Fields
            .Include(f => f.ReadPolicies)
            .Include(f => f.WritePolicies)
            .Where(f => f.ResourceId == resource.Id)
            .ToListAsync();

        if (!fields.Any())
        {
            _logger.LogDebug("No fields defined for resource {ResourceName}", resource.ResourceName);
            return;
        }

        var isReadAction = action.ToLower() == "read";

        foreach (var field in fields)
        {
            var fieldPolicies = isReadAction ? field.ReadPolicies : field.WritePolicies;
            
            FieldAccessLevel accessLevel;
            
            // Check if field has policies - policies determine protection, NOT MaskFormat
            if (!fieldPolicies.Any())
            {
                // No policies on field: Full access (MaskFormat is ignored)
                accessLevel = FieldAccessLevel.Full;
                _logger.LogDebug(
                    "Field {FieldName}: No policies assigned - Full access granted",
                    field.FieldName);
            }
            else
            {
                // Field has policies: Evaluate them
                bool policyMatched = false;
                
                foreach (var policy in fieldPolicies)
                {
                    try
                    {
                        var policyRules = JsonSerializer.Deserialize<PolicyRule>(policy.PolicyData);
                        if (policyRules != null)
                        {
                            // Create temp context for field policy evaluation (don't mix with RLS rules)
                            var tempContext = new SecurityContextService();
                            bool result = await EvaluateRuleAsync(policyRules, user, dbContext, tempContext);
                            
                            if (result)
                            {
                                policyMatched = true;
                                _logger.LogDebug(
                                    "Field {FieldName}: Policy {PolicyId} matched - Full access granted",
                                    field.FieldName, policy.Id);
                                break; // OR logic - one match is enough
                            }
                        }
                    }
                    catch (JsonException ex)
                    {
                        _logger.LogError(ex, "Failed to deserialize policy data for field policy {PolicyId}", policy.Id);
                    }
                }
                
                if (policyMatched)
                {
                    accessLevel = FieldAccessLevel.Full;
                }
                else
                {
                    // No policy matched: Apply MaskFormat
                    accessLevel = DetermineAccessLevelFromMaskFormat(field.MaskFormat);
                    _logger.LogDebug(
                        "Field {FieldName}: No policies matched - Access level: {AccessLevel} (MaskFormat: {MaskFormat})",
                        field.FieldName, accessLevel, field.MaskFormat ?? "null");
                }
            }
            
            // Add field access rule to security context
            securityContextService.AddFieldAccess(new FieldAccessRule
            {
                FieldName = field.FieldName,
                AccessLevel = accessLevel,
                MaskFormat = accessLevel == FieldAccessLevel.Masked ? field.MaskFormat : null
            });
        }
    }

    /// <summary>
    /// Determines the access level based on the MaskFormat value.
    /// - null: Hidden (but this shouldn't be called for null, as null = normal field)
    /// - "": Empty
    /// - Any other value: Masked
    /// </summary>
    private static FieldAccessLevel DetermineAccessLevelFromMaskFormat(string? maskFormat)
    {
        return maskFormat switch
        {
            null => FieldAccessLevel.Hidden,
            "" => FieldAccessLevel.Empty,
            _ => FieldAccessLevel.Masked
        };
    }
}
