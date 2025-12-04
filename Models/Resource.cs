namespace PolicyPOC.Models;

public class Resource
{
    public required Guid Id { get; set; }
    public required string ResourceName { get; set; }

    // Navigation property for Fields (one-to-many)
    public ICollection<Field> Fields { get; set; } = new List<Field>();
    
    // Same with below, In real implementation we do not separate
    // Read/Write Policies, we will have mapping policy with Action
    
    // Navigation properties for many-to-many relationships
    public ICollection<Policy> ReadPolicies { get; set; } = new List<Policy>();
    public ICollection<Policy> WritePolicies { get; set; } = new List<Policy>();
}

public class Field
{
    public required Guid Id { get; set; }
    public required string FieldName { get; set; }

    // Note this implementation is for POC only
    // We will need strong type and fully optimized for JSONB storage
    // It means that the following field will be a property in a class not directly in Field class

    // MaskFormat determines what to show when user doesn't have permission (denied):
    // - null: Hidden (field excluded from response)
    // - "": Empty (return null/empty)
    // - "***-**-{last4}": Masked (apply format)
    // 
    // Note: MaskFormat is ONLY used when field has policies AND user doesn't match any.
    //       If field has no policies, user gets full access (MaskFormat ignored).
    // 
    // Format placeholders: {value}, {last4}, {first4}, {first1}, etc.
    public string? MaskFormat { get; set; }
    // public required bool IsPII { get; set; }
    
    // Foreign key to Resource
    public required Guid ResourceId { get; set; }
    public Resource? Resource { get; set; }
    
    // Field-level policies - many-to-many relationships
    public ICollection<Policy> ReadPolicies { get; set; } = new List<Policy>();
    public ICollection<Policy> WritePolicies { get; set; } = new List<Policy>();
}