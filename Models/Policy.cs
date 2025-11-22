namespace PolicyPOC.Models;

public class Policy
{
    public required Guid Id { get; set; }
    public string? Description { get; set; }
    public required string PolicyData { get; set; } // JSON string JSONB in postgres

    public required ICollection<PolicyResource> PolicyResources { get; set; }
}

public class PolicyResource
{
    public required Guid Id { get; set; }
    public required Guid PolicyId { get; set; }
    public string? ResourceName { get; set; }
    public string? ResourceColumns { get; set; } 

    public required Policy Policy { get; set; }
}