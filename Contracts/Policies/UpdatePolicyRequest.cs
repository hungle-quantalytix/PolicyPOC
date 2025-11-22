using System.ComponentModel.DataAnnotations;

namespace PolicyPOC.Contracts.Policies;

public class UpdatePolicyRequest
{
    [MaxLength(500)]
    public string? Description { get; set; }

    [Required]
    public required string PolicyData { get; set; }
}

