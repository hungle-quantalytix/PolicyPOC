namespace PolicyPOC.Contracts.Permissions;

public class CreatePermissionRequest
{
    public required string ResourceType { get; set; }
    public string? ResourceId { get; set; }
    public string? FieldName { get; set; }
    public required string Action { get; set; }
    public required string SubjectType { get; set; }
    public required string SubjectId { get; set; }
    public string? Description { get; set; }
}

