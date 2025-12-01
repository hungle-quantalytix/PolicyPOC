using System.ComponentModel.DataAnnotations;

namespace PolicyPOC.Contracts.Fields;

public class UpdateFieldRequest
{
    [Required]
    [MaxLength(100)]
    public required string FieldName { get; set; }
    
    public bool IsPublic { get; set; }
}

