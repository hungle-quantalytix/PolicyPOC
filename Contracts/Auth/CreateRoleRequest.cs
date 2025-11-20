using System.ComponentModel.DataAnnotations;

namespace PolicyPOC.Contracts.Auth;

public class CreateRoleRequest
{
    [Required]
    [MinLength(2)]
    public string Name { get; set; } = string.Empty;
}


