namespace PolicyPOC.Contracts.Policies;

public class PolicyResponse
{
    public required Guid Id { get; set; }
    public string? Description { get; set; }
    public required string PolicyData { get; set; }
    public List<PolicyResourceResponse>? PolicyResources { get; set; }
}

public class PolicyResourceResponse
{
    public required Guid Id { get; set; }
    public required Guid PolicyId { get; set; }
    public string? ResourceName { get; set; }
    public string? ResourceColumns { get; set; }
}

