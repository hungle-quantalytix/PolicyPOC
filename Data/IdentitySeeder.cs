using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PolicyPOC.Models;

namespace PolicyPOC.Data;

public static class IdentitySeeder
{
    public static async Task EnsureDatabaseAsync(IServiceProvider services, ILogger logger, CancellationToken cancellationToken = default)
    {
        using var scope = services.CreateScope();
        var scopedServices = scope.ServiceProvider;
        var context = scopedServices.GetRequiredService<ApplicationDbContext>();

        await context.Database.MigrateAsync(cancellationToken);
        await SeedRolesAsync(scopedServices, logger, cancellationToken);
        await SeedDefaultAdminAsync(scopedServices, logger, cancellationToken);
    }

    private static async Task SeedRolesAsync(IServiceProvider services, ILogger logger, CancellationToken cancellationToken)
    {
        var configuration = services.GetRequiredService<IConfiguration>();
        var defaultRoles = configuration.GetSection("Identity:DefaultRoles").Get<string[]>() ?? [];
        if (defaultRoles.Length == 0)
        {
            return;
        }

        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
        foreach (var role in defaultRoles)
        {
            if (await roleManager.RoleExistsAsync(role))
            {
                continue;
            }

            var result = await roleManager.CreateAsync(new IdentityRole(role));
            if (!result.Succeeded)
            {
                logger.LogError("Failed creating role {Role}: {Errors}", role, string.Join(",", result.Errors.Select(e => e.Description)));
            }
        }
    }

    private static async Task SeedDefaultAdminAsync(IServiceProvider services, ILogger logger, CancellationToken cancellationToken)
    {
        var configuration = services.GetRequiredService<IConfiguration>();
        var adminSection = configuration.GetSection("Identity:DefaultAdmin");
        var email = adminSection.GetValue<string>("Email");
        var password = adminSection.GetValue<string>("Password");
        var roles = adminSection.GetSection("Roles").Get<string[]>() ?? [];

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            return;
        }

        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
        var existingUser = await userManager.FindByEmailAsync(email);
        if (existingUser is not null)
        {
            return;
        }

        var user = new ApplicationUser
        {
            Email = email,
            UserName = email,
            EmailConfirmed = true,
            DisplayName = "Administrator"
        };

        var createResult = await userManager.CreateAsync(user, password);
        if (!createResult.Succeeded)
        {
            logger.LogError("Failed creating default admin user {Email}: {Errors}", email, string.Join(",", createResult.Errors.Select(e => e.Description)));
            return;
        }

        foreach (var role in roles)
        {
            if (!await userManager.IsInRoleAsync(user, role))
            {
                var roleResult = await userManager.AddToRoleAsync(user, role);
                if (!roleResult.Succeeded)
                {
                    logger.LogError("Failed assigning role {Role} to default admin: {Errors}", role, string.Join(",", roleResult.Errors.Select(e => e.Description)));
                }
            }
        }
    }
}


