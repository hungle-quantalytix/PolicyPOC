using System.ComponentModel.DataAnnotations;

namespace PolicyPOC.Contracts.Policies;

public class AssignPolicyRequest
{
    [Required]
    public required Guid PolicyId { get; set; }

    [Required]
    [MaxLength(100)]
    public required string ResourceName { get; set; }

    /// <summary>
    /// Action type: "read" or "write"
    /// </summary>
    [MaxLength(100)]
    public string? Action { get; set; }
}

