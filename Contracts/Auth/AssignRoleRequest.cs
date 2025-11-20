using System.ComponentModel.DataAnnotations;

namespace PolicyPOC.Contracts.Auth;

public class AssignRoleRequest
{
    [Required]
    [EmailAddress]
    public string UserEmail { get; set; } = string.Empty;

    [Required]
    public string RoleName { get; set; } = string.Empty;
}


