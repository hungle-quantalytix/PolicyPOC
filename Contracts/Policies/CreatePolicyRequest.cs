using System.ComponentModel.DataAnnotations;

namespace PolicyPOC.Contracts.Policies;

public class CreatePolicyRequest
{
    [MaxLength(500)]
    public string? Description { get; set; }

    [Required]
    public required string PolicyData { get; set; }
}

