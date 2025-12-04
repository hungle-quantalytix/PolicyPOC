namespace PolicyPOC.Contracts.Permissions;

public class UpdatePermissionRequest
{
    public string? ResourceType { get; set; }
    public string? ResourceId { get; set; }
    public string? FieldName { get; set; }
    public string? Action { get; set; }
    public string? SubjectType { get; set; }
    public string? SubjectId { get; set; }
    public string? Description { get; set; }
}

