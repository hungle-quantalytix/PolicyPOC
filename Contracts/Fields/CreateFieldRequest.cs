using System.ComponentModel.DataAnnotations;

namespace PolicyPOC.Contracts.Fields;

public class CreateFieldRequest
{
    [Required]
    [MaxLength(100)]
    public required string FieldName { get; set; }
    
    public bool IsPublic { get; set; } = false;
}

