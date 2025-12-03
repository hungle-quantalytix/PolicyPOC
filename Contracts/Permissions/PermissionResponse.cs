namespace PolicyPOC.Contracts.Permissions;

public class PermissionResponse
{
    public required Guid Id { get; set; }
    public required string ResourceType { get; set; }
    public string? ResourceId { get; set; }
    public string? FieldName { get; set; }
    public required string Action { get; set; }
    public required string SubjectType { get; set; }
    public required string SubjectId { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? Description { get; set; }
    
    // Resolved display names for UI
    public string? SubjectDisplayName { get; set; }
}

