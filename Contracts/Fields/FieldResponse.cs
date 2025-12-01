namespace PolicyPOC.Contracts.Fields;

public class FieldResponse
{
    public Guid Id { get; set; }
    public required string FieldName { get; set; }
    public bool IsPublic { get; set; }
    public Guid ResourceId { get; set; }
    
    // Policy IDs assigned for read action
    public List<Guid>? ReadPolicyIds { get; set; }
    
    // Policy IDs assigned for write action
    public List<Guid>? WritePolicyIds { get; set; }
}

