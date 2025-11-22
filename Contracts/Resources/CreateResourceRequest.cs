using System.ComponentModel.DataAnnotations;

namespace PolicyPOC.Contracts.Resources;

public class CreateResourceRequest
{
    [Required]
    public required string ResourceName { get; set; }
}

