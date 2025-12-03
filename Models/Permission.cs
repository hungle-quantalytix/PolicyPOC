namespace PolicyPOC.Models;

/// <summary>
/// Unified permission table supporting both resource-level and field-level security.
/// 
/// Structure: (ResourceType, ResourceId, FieldName, Action, SubjectType, SubjectId)
/// 
/// - FieldName = null → Resource-level permission
/// - FieldName = "SSN" → Field/Column-level permission
/// 
/// SubjectType priority order: Policy > Role > User
/// - Policy: SubjectId is PolicyId, evaluated against user claims
/// - Role: SubjectId is role name, direct match
/// - User: SubjectId is userId, direct match
/// </summary>
public class Permission
{
    public required Guid Id { get; set; }
    
    // Resource identification
    public required string ResourceType { get; set; }   // e.g., "Loan", "Document"
    public string? ResourceId { get; set; }             // null = applies to all resources of this type
    
    // Field/Column identification (for field-level security)
    public string? FieldName { get; set; }              // null = resource-level, value = field-level
    
    // Action
    public required string Action { get; set; }         // e.g., "read", "write", "delete"
    
    // Subject - Policy is just another subject type with highest priority
    public required string SubjectType { get; set; }    // "Policy", "Role", "User"
    public required string SubjectId { get; set; }      // PolicyId (GUID), RoleName, or UserId
    
    // Metadata
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    // Description for admin UI
    public string? Description { get; set; }
}

/// <summary>
/// Subject type constants and priority order for permission evaluation.
/// </summary>
public static class SubjectTypes
{
    public const string Policy = "Policy";  // Priority 1 - Evaluated via policy rules against user
    public const string Role = "Role";      // Priority 2 - Direct role match
    public const string User = "User";      // Priority 3 - Direct user match
    
    /// <summary>
    /// Priority order for evaluation. First match wins.
    /// </summary>
    public static readonly string[] PriorityOrder = { Policy, Role, User };
}

/// <summary>
/// Common actions for permissions.
/// </summary>
public static class PermissionActions
{
    public const string Read = "read";
    public const string Write = "write";
    public const string Delete = "delete";
    public const string Create = "create";
}

