namespace PolicyPOC.Contracts.Fields;

public class FieldResponse
{
    public Guid Id { get; set; }
    public required string FieldName { get; set; }
    
    /// <summary>
    /// Mask format for denied access:
    /// - null: Normal field (no policy = open, when denied = hidden)
    /// - "": Protected field (requires policy, when denied = empty)
    /// - "***-**-{last4}": Protected field (requires policy, when denied = masked)
    /// </summary>
    public string? MaskFormat { get; set; }
    
    public Guid ResourceId { get; set; }
    
    // Policy IDs assigned for read action
    public List<Guid>? ReadPolicyIds { get; set; }
    
    // Policy IDs assigned for write action
    public List<Guid>? WritePolicyIds { get; set; }
}

