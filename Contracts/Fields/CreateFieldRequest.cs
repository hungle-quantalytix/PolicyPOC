using System.ComponentModel.DataAnnotations;

namespace PolicyPOC.Contracts.Fields;

public class CreateFieldRequest
{
    [Required]
    [MaxLength(100)]
    public required string FieldName { get; set; }
    
    /// <summary>
    /// Mask format for denied access:
    /// - null: Normal field (no policy = open, when denied = hidden)
    /// - "": Protected field (requires policy, when denied = empty)
    /// - "***-**-{last4}": Protected field (requires policy, when denied = masked)
    /// </summary>
    [MaxLength(200)]
    public string? MaskFormat { get; set; }
}

