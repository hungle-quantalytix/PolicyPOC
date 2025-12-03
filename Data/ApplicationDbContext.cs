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
    public DbSet<Field> Fields { get; set; }
    public DbSet<Policy> Policies { get; set; }
    public DbSet<Permission> Permissions { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configure Resource <-> Field one-to-many
        modelBuilder.Entity<Field>()
            .HasOne(f => f.Resource)
            .WithMany(r => r.Fields)
            .HasForeignKey(f => f.ResourceId)
            .OnDelete(DeleteBehavior.Cascade);

        // Configure Resource <-> Policy many-to-many for ReadPolicies
        modelBuilder.Entity<Resource>()
            .HasMany(r => r.ReadPolicies)
            .WithMany(p => p.ReadResources)
            .UsingEntity(j => j.ToTable("ResourceReadPolicies"));

        // Configure Resource <-> Policy many-to-many for WritePolicies
        modelBuilder.Entity<Resource>()
            .HasMany(r => r.WritePolicies)
            .WithMany(p => p.WriteResources)
            .UsingEntity(j => j.ToTable("ResourceWritePolicies"));

        // Configure Field <-> Policy many-to-many for ReadPolicies
        modelBuilder.Entity<Field>()
            .HasMany(f => f.ReadPolicies)
            .WithMany(p => p.ReadFields)
            .UsingEntity(j => j.ToTable("FieldReadPolicies"));

        // Configure Field <-> Policy many-to-many for WritePolicies
        modelBuilder.Entity<Field>()
            .HasMany(f => f.WritePolicies)
            .WithMany(p => p.WriteFields)
            .UsingEntity(j => j.ToTable("FieldWritePolicies"));

        // Configure Permission entity
        modelBuilder.Entity<Permission>(entity =>
        {
            entity.HasKey(p => p.Id);
            
            // Unique constraint: one permission per resource/field/action/subject combination
            entity.HasIndex(p => new { 
                p.ResourceType, 
                p.ResourceId, 
                p.FieldName, 
                p.Action, 
                p.SubjectType, 
                p.SubjectId 
            }).IsUnique();
            
            // Performance index: lookup by subject (for "what can user X access?")
            entity.HasIndex(p => new { p.SubjectType, p.SubjectId });
            
            // Performance index: lookup by resource (for "who can access resource Y?")
            entity.HasIndex(p => new { p.ResourceType, p.ResourceId, p.Action });
            
            // Performance index: field-level lookups
            entity.HasIndex(p => new { p.ResourceType, p.FieldName, p.Action })
                .HasFilter("\"FieldName\" IS NOT NULL");
        });
    }
}


