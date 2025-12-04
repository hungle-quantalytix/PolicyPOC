namespace PolicyPOC.Contracts.Policies;

public class PolicyResponse
{
    public required Guid Id { get; set; }
    public string? Description { get; set; }
    public required string PolicyData { get; set; }
    
    // Resources that have this policy assigned for read action
    public List<string>? ReadResourceNames { get; set; }
    
    // Resources that have this policy assigned for write action
    public List<string>? WriteResourceNames { get; set; }
}

