using System.Security.Claims;

namespace PolicyPOC.Services;

public interface IPermissionService
{
    /// <summary>
    /// Evaluates all permissions for a resource request, respecting priority order (Policy > Role > User).
    /// Returns the result including any RLS rules from matched policies.
    /// </summary>
    Task<PermissionResult> EvaluateResourceAccessAsync(
        string resourceType,
        string? resourceId,
        string action,
        ClaimsPrincipal user);
    
    /// <summary>
    /// Evaluates field-level permissions for a specific resource.
    /// Returns field access rules for all protected fields.
    /// </summary>
    Task<List<FieldAccessRule>> EvaluateFieldAccessAsync(
        string resourceType,
        string? resourceId,
        string action,
        ClaimsPrincipal user);
    
    /// <summary>
    /// Gets all resource IDs the user has direct permission to (for RLS).
    /// Only checks Role and User permissions, not Policy (policy needs evaluation).
    /// </summary>
    Task<List<string>> GetAllowedResourceIdsAsync(
        string resourceType,
        string action,
        string userId,
        IEnumerable<string> userRoles);
}

/// <summary>
/// Result of permission evaluation for resource access.
/// </summary>
public class PermissionResult
{
    public bool IsAllowed { get; set; }
    public string? MatchedSubjectType { get; set; }     // Which type granted access (Policy, Role, User)
    public string? MatchedSubjectId { get; set; }       // Which subject granted access
    public List<RowLevelSecurityRule> RlsRules { get; set; } = new();
    public string? DeniedReason { get; set; }
}

