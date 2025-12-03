using System.Security.Claims;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PolicyPOC.Data;
using PolicyPOC.Models;

namespace PolicyPOC.Services;

public class PermissionService : IPermissionService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly ILogger<PermissionService> _logger;

    public PermissionService(ApplicationDbContext dbContext, ILogger<PermissionService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<PermissionResult> EvaluateResourceAccessAsync(
        string resourceType,
        string? resourceId,
        string action,
        ClaimsPrincipal user)
    {
        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";
        var userRoles = user.FindAll(ClaimTypes.Role).Select(c => c.Value).ToHashSet();

        _logger.LogInformation(
            "Evaluating resource permission: {ResourceType}:{ResourceId}:{Action} for user {UserId} with roles [{Roles}]",
            resourceType, resourceId ?? "*", action, userId, string.Join(", ", userRoles));

        // 1. Load all matching resource-level permissions (FieldName IS NULL)
        var permissions = await _dbContext.Permissions
            .Where(p => p.ResourceType == resourceType)
            .Where(p => p.ResourceId == null || p.ResourceId == resourceId)
            .Where(p => p.FieldName == null) // Resource-level only
            .Where(p => p.Action == action)
            .ToListAsync();

        if (!permissions.Any())
        {
            _logger.LogWarning(
                "No permissions found for {ResourceType}:{Action}",
                resourceType, action);
            return new PermissionResult 
            { 
                IsAllowed = false,
                DeniedReason = $"No permissions configured for resource '{resourceType}' with action '{action}'"
            };
        }

        // 2. Evaluate by priority order: Policy > Role > User
        foreach (var subjectType in SubjectTypes.PriorityOrder)
        {
            var subjectPermissions = permissions
                .Where(p => p.SubjectType == subjectType)
                .ToList();

            if (!subjectPermissions.Any())
                continue;

            _logger.LogDebug(
                "Evaluating {Count} {SubjectType} permissions for {ResourceType}:{Action}",
                subjectPermissions.Count, subjectType, resourceType, action);

            var result = subjectType switch
            {
                SubjectTypes.Policy => await EvaluatePolicyPermissionsAsync(subjectPermissions, user),
                SubjectTypes.Role => EvaluateRolePermissions(subjectPermissions, userRoles),
                SubjectTypes.User => EvaluateUserPermissions(subjectPermissions, userId),
                _ => null
            };

            if (result?.IsAllowed == true)
            {
                _logger.LogInformation(
                    "Access GRANTED via {SubjectType}:{SubjectId} for {ResourceType}:{Action}",
                    result.MatchedSubjectType, result.MatchedSubjectId, resourceType, action);
                return result;
            }
        }

        _logger.LogWarning(
            "Access DENIED for {ResourceType}:{Action} - no matching permission found",
            resourceType, action);
            
        return new PermissionResult 
        { 
            IsAllowed = false,
            DeniedReason = "No matching permission found"
        };
    }

    /// <inheritdoc />
    public async Task<List<FieldAccessRule>> EvaluateFieldAccessAsync(
        string resourceType,
        string? resourceId,
        string action,
        ClaimsPrincipal user)
    {
        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";
        var userRoles = user.FindAll(ClaimTypes.Role).Select(c => c.Value).ToHashSet();
        
        var fieldAccessRules = new List<FieldAccessRule>();

        // 1. Get all field-level permissions for this resource
        var fieldPermissions = await _dbContext.Permissions
            .Where(p => p.ResourceType == resourceType)
            .Where(p => p.ResourceId == null || p.ResourceId == resourceId)
            .Where(p => p.FieldName != null) // Field-level only
            .Where(p => p.Action == action)
            .ToListAsync();

        if (!fieldPermissions.Any())
        {
            _logger.LogDebug("No field-level permissions found for {ResourceType}:{Action}", resourceType, action);
            return fieldAccessRules;
        }

        // 2. Get Field metadata (for MaskFormat when denied)
        var resource = await _dbContext.Resources
            .Include(r => r.Fields)
            .FirstOrDefaultAsync(r => r.ResourceName == resourceType);
        
        var fieldMetadata = resource?.Fields.ToDictionary(f => f.FieldName, f => f, StringComparer.OrdinalIgnoreCase) 
            ?? new Dictionary<string, Field>();

        // 3. Group permissions by field name and evaluate each
        var fieldGroups = fieldPermissions.GroupBy(p => p.FieldName!);

        foreach (var fieldGroup in fieldGroups)
        {
            var fieldName = fieldGroup.Key;
            var permissionsForField = fieldGroup.ToList();
            
            bool hasAccess = false;
            
            // Evaluate by priority order
            foreach (var subjectType in SubjectTypes.PriorityOrder)
            {
                var subjectPermissions = permissionsForField
                    .Where(p => p.SubjectType == subjectType)
                    .ToList();

                if (!subjectPermissions.Any())
                    continue;

                var result = subjectType switch
                {
                    SubjectTypes.Policy => await EvaluatePolicyPermissionsAsync(subjectPermissions, user),
                    SubjectTypes.Role => EvaluateRolePermissions(subjectPermissions, userRoles),
                    SubjectTypes.User => EvaluateUserPermissions(subjectPermissions, userId),
                    _ => null
                };

                if (result?.IsAllowed == true)
                {
                    hasAccess = true;
                    _logger.LogDebug(
                        "Field {FieldName}: Access granted via {SubjectType}:{SubjectId}",
                        fieldName, result.MatchedSubjectType, result.MatchedSubjectId);
                    break;
                }
            }

            // Determine access level based on evaluation result
            FieldAccessLevel accessLevel;
            string? maskFormat = null;

            if (hasAccess)
            {
                accessLevel = FieldAccessLevel.Full;
            }
            else
            {
                // Field permission exists but user doesn't match - apply restriction
                // Use MaskFormat from Field metadata if available
                if (fieldMetadata.TryGetValue(fieldName, out var field))
                {
                    accessLevel = DetermineAccessLevelFromMaskFormat(field.MaskFormat);
                    maskFormat = field.MaskFormat;
                }
                else
                {
                    // No field metadata, default to Hidden
                    accessLevel = FieldAccessLevel.Hidden;
                }
                
                _logger.LogDebug(
                    "Field {FieldName}: Access denied, applying {AccessLevel}",
                    fieldName, accessLevel);
            }

            fieldAccessRules.Add(new FieldAccessRule
            {
                FieldName = fieldName,
                AccessLevel = accessLevel,
                MaskFormat = accessLevel == FieldAccessLevel.Masked ? maskFormat : null
            });
        }

        return fieldAccessRules;
    }

    /// <inheritdoc />
    public async Task<List<string>> GetAllowedResourceIdsAsync(
        string resourceType,
        string action,
        string userId,
        IEnumerable<string> userRoles)
    {
        var rolesList = userRoles.ToList();

        // Get permissions with specific ResourceIds (not wildcards)
        var permissions = await _dbContext.Permissions
            .Where(p => p.ResourceType == resourceType)
            .Where(p => p.ResourceId != null)
            .Where(p => p.FieldName == null) // Resource-level only
            .Where(p => p.Action == action)
            .Where(p =>
                (p.SubjectType == SubjectTypes.User && p.SubjectId == userId) ||
                (p.SubjectType == SubjectTypes.Role && rolesList.Contains(p.SubjectId)))
            .Select(p => p.ResourceId!)
            .Distinct()
            .ToListAsync();

        return permissions;
    }

    #region Private Evaluation Methods

    /// <summary>
    /// Priority 1: Evaluate Policy permissions.
    /// SubjectId = PolicyId, load and evaluate policy rules against current user.
    /// </summary>
    private async Task<PermissionResult?> EvaluatePolicyPermissionsAsync(
        List<Permission> permissions,
        ClaimsPrincipal user)
    {
        foreach (var permission in permissions)
        {
            if (!Guid.TryParse(permission.SubjectId, out var policyId))
            {
                _logger.LogWarning("Invalid PolicyId format: {SubjectId}", permission.SubjectId);
                continue;
            }

            var policy = await _dbContext.Policies.FindAsync(policyId);
            if (policy == null)
            {
                _logger.LogWarning("Policy not found: {PolicyId}", policyId);
                continue;
            }

            try
            {
                var policyRules = JsonSerializer.Deserialize<PolicyRule>(policy.PolicyData);
                if (policyRules == null)
                {
                    _logger.LogWarning("Failed to deserialize policy rules for {PolicyId}", policyId);
                    continue;
                }

                // Evaluate policy rules against user
                var tempSecurityContext = new SecurityContextService();
                bool matches = await EvaluatePolicyRulesAsync(policyRules, user, tempSecurityContext);

                if (matches)
                {
                    return new PermissionResult
                    {
                        IsAllowed = true,
                        MatchedSubjectType = SubjectTypes.Policy,
                        MatchedSubjectId = permission.SubjectId,
                        RlsRules = tempSecurityContext.SecurityContext.RowLevelSecurityRules
                    };
                }
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Error deserializing policy {PolicyId}", policyId);
            }
        }

        return null;
    }

    /// <summary>
    /// Priority 2: Evaluate Role permissions.
    /// SubjectId = RoleName, check if user has any matching role.
    /// </summary>
    private PermissionResult? EvaluateRolePermissions(
        List<Permission> permissions,
        HashSet<string> userRoles)
    {
        var matchingPermission = permissions
            .FirstOrDefault(p => userRoles.Contains(p.SubjectId));

        if (matchingPermission != null)
        {
            return new PermissionResult
            {
                IsAllowed = true,
                MatchedSubjectType = SubjectTypes.Role,
                MatchedSubjectId = matchingPermission.SubjectId
            };
        }

        return null;
    }

    /// <summary>
    /// Priority 3: Evaluate User permissions.
    /// SubjectId = UserId, direct match.
    /// </summary>
    private PermissionResult? EvaluateUserPermissions(
        List<Permission> permissions,
        string userId)
    {
        var matchingPermission = permissions
            .FirstOrDefault(p => p.SubjectId == userId);

        if (matchingPermission != null)
        {
            return new PermissionResult
            {
                IsAllowed = true,
                MatchedSubjectType = SubjectTypes.User,
                MatchedSubjectId = matchingPermission.SubjectId
            };
        }

        return null;
    }

    #endregion

    #region Policy Rule Evaluation (reused from middleware logic)

    private async Task<bool> EvaluatePolicyRulesAsync(
        PolicyRule rule,
        ClaimsPrincipal user,
        ISecurityContextService securityContext)
    {
        return rule switch
        {
            ComparisonRule comparison => await EvaluateComparisonRuleAsync(comparison, user, securityContext),
            PolicyGroup group => await EvaluatePolicyGroupAsync(group, user, securityContext),
            _ => false
        };
    }

    private async Task<bool> EvaluatePolicyGroupAsync(
        PolicyGroup policyGroup,
        ClaimsPrincipal user,
        ISecurityContextService securityContext)
    {
        if (policyGroup.Condition == null)
            throw new InvalidOperationException("PolicyGroup.Condition must not be null.");
            
        var isAnd = policyGroup.Condition.Trim().ToUpperInvariant() switch
        {
            "AND" => true,
            "OR" => false,
            _ => throw new InvalidOperationException($"Unsupported condition: {policyGroup.Condition}"),
        };
        
        bool isAllowed = isAnd;

        foreach (var rule in policyGroup.Rules)
        {
            bool result = await EvaluatePolicyRulesAsync(rule, user, securityContext);

            if (isAnd && !result) return false;  // AND short-circuit
            if (!isAnd && result) return true;   // OR short-circuit

            isAllowed = isAnd ? (isAllowed && result) : (isAllowed || result);
        }

        return isAllowed;
    }

    private async Task<bool> EvaluateComparisonRuleAsync(
        ComparisonRule comparisonRule,
        ClaimsPrincipal user,
        ISecurityContextService securityContext)
    {
        // Standard policy rule: user.* fields
        if (comparisonRule.Field.StartsWith("user.", StringComparison.OrdinalIgnoreCase))
        {
            var fieldValue = await GetUserFieldValueAsync(comparisonRule.Field, user);
            
            return comparisonRule.Operator.ToLower() switch
            {
                "=" or "equals" => string.Equals(fieldValue, comparisonRule.Value, StringComparison.OrdinalIgnoreCase),
                "!=" or "notequals" or "not equals" => !string.Equals(fieldValue, comparisonRule.Value, StringComparison.OrdinalIgnoreCase),
                "in" => comparisonRule.Value.Split(',').Select(v => v.Trim()).Contains(fieldValue, StringComparer.OrdinalIgnoreCase),
                _ => false
            };
        }
        
        // Row-level security rule: resource.* fields
        if (comparisonRule.Field.StartsWith("resource.", StringComparison.OrdinalIgnoreCase))
        {
            var resourceField = comparisonRule.Field.Substring("resource.".Length);
            var resolvedValue = comparisonRule.Value;
            
            // Resolve user field references in value
            if (comparisonRule.Value.StartsWith("${user.", StringComparison.OrdinalIgnoreCase) && 
                comparisonRule.Value.EndsWith("}"))
            {
                var userField = comparisonRule.Value.Substring(2, comparisonRule.Value.Length - 3);
                resolvedValue = await GetUserFieldValueAsync(userField, user);
            }
            else if (comparisonRule.Value.StartsWith("user.", StringComparison.OrdinalIgnoreCase))
            {
                resolvedValue = await GetUserFieldValueAsync(comparisonRule.Value, user);
            }
            
            // Add to security context for data layer filtering
            securityContext.AddRowLevelSecurityRule(new RowLevelSecurityRule
            {
                ResourceField = resourceField,
                Operator = comparisonRule.Operator,
                Value = resolvedValue
            });
            
            // RLS rules don't block at this level, they filter data
            return true;
        }
        
        _logger.LogWarning("Unknown field prefix: {Field}", comparisonRule.Field);
        return false;
    }

    private async Task<string> GetUserFieldValueAsync(string field, ClaimsPrincipal user)
    {
        var fieldName = field.StartsWith("user.", StringComparison.OrdinalIgnoreCase)
            ? field.Substring("user.".Length).ToLower()
            : field.ToLower();

        return fieldName switch
        {
            "id" => user.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty,
            "email" => user.FindFirstValue(ClaimTypes.Email) ?? string.Empty,
            "role" or "roles" => string.Join(",", user.FindAll(ClaimTypes.Role).Select(c => c.Value)),
            "department" => await GetUserDepartmentAsync(user),
            _ => await GetUserPropertyAsync(fieldName, user)
        };
    }

    private async Task<string> GetUserDepartmentAsync(ClaimsPrincipal user)
    {
        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId)) return string.Empty;
        
        var appUser = await _dbContext.Users.FirstOrDefaultAsync(u => u.Id == userId);
        return appUser?.Department ?? string.Empty;
    }

    private async Task<string> GetUserPropertyAsync(string propertyName, ClaimsPrincipal user)
    {
        // First try claims
        var claim = user.FindFirstValue(propertyName);
        if (!string.IsNullOrEmpty(claim)) return claim;
        
        // Then try database
        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId)) return string.Empty;
        
        var appUser = await _dbContext.Users.FirstOrDefaultAsync(u => u.Id == userId);
        if (appUser == null) return string.Empty;
        
        var property = typeof(ApplicationUser).GetProperty(propertyName,
            System.Reflection.BindingFlags.IgnoreCase |
            System.Reflection.BindingFlags.Public |
            System.Reflection.BindingFlags.Instance);
            
        return property?.GetValue(appUser)?.ToString() ?? string.Empty;
    }

    #endregion

    #region Helpers

    private static FieldAccessLevel DetermineAccessLevelFromMaskFormat(string? maskFormat)
    {
        return maskFormat switch
        {
            null => FieldAccessLevel.Hidden,
            "" => FieldAccessLevel.Empty,
            _ => FieldAccessLevel.Masked
        };
    }

    #endregion
}

