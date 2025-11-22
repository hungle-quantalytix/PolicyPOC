using System.ComponentModel.DataAnnotations;

namespace PolicyPOC.Contracts.Resources;

public class UpdateResourceRequest
{
    [Required]
    public required string ResourceName { get; set; }
}

