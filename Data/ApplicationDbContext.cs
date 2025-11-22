using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using PolicyPOC.Models;

namespace PolicyPOC.Data;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser, IdentityRole, string>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<Loan> Loans { get; set; }
    public DbSet<Resource> Resources { get; set; }
    public DbSet<Policy> Policies { get; set; }
    public DbSet<PolicyResource> PolicyResources { get; set; }
}


