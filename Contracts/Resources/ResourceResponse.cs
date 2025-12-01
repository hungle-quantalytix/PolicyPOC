using PolicyPOC.Contracts.Fields;

namespace PolicyPOC.Contracts.Resources;

public class ResourceResponse
{
    public Guid Id { get; set; }
    public required string ResourceName { get; set; }
    
    // Policy IDs assigned for read action
    public List<Guid>? ReadPolicyIds { get; set; }
    
    // Policy IDs assigned for write action
    public List<Guid>? WritePolicyIds { get; set; }
    
    // Fields belonging to this resource
    public List<FieldResponse>? Fields { get; set; }
}

