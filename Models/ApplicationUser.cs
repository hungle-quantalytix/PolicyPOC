using Microsoft.AspNetCore.Identity;

namespace PolicyPOC.Models;

public class ApplicationUser : IdentityUser
{
    public string? DisplayName { get; set; }
}


