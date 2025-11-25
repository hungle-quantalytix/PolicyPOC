using System.ComponentModel.DataAnnotations;

namespace PolicyPOC.Contracts.Policies;

public class UpdatePolicyAssignmentRequest
{
    [Required]
    public required Guid AssignmentId { get; set; }

    [MaxLength(100)]
    public string? ResourceName { get; set; }

    [MaxLength(500)]
    public string? ResourceColumns { get; set; }

    [MaxLength(100)]
    public string? Action { get; set; }

    [MaxLength(100)]
    public string? Effect { get; set; }
}

