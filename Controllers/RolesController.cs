using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using PolicyPOC.Contracts.Auth;
using PolicyPOC.Models;

namespace PolicyPOC.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Administrator,SuperAdmin")]
public class RolesController(
    RoleManager<IdentityRole> roleManager,
    UserManager<ApplicationUser> userManager,
    ILogger<RolesController> logger) : ControllerBase
{
    private readonly RoleManager<IdentityRole> _roleManager = roleManager;
    private readonly UserManager<ApplicationUser> _userManager = userManager;
    private readonly ILogger<RolesController> _logger = logger;

    [HttpGet]
    public ActionResult<IEnumerable<string>> GetRoles()
    {
        var roles = _roleManager.Roles.Select(r => r.Name!).ToArray();
        return Ok(roles);
    }

    [HttpPost]
    public async Task<IActionResult> CreateRole(CreateRoleRequest request)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        if (await _roleManager.RoleExistsAsync(request.Name))
        {
            return Conflict(new { message = "Role already exists." });
        }

        var result = await _roleManager.CreateAsync(new IdentityRole(request.Name));
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(error.Code, error.Description);
            }

            return ValidationProblem(ModelState);
        }

        return CreatedAtAction(nameof(CreateRole), new { name = request.Name }, new { request.Name });
    }

    [HttpPost("assign")]
    public async Task<IActionResult> AssignRole(AssignRoleRequest request)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var user = await _userManager.FindByEmailAsync(request.UserEmail);
        if (user is null)
        {
            return NotFound(new { message = $"User '{request.UserEmail}' not found." });
        }

        if (!await _roleManager.RoleExistsAsync(request.RoleName))
        {
            return NotFound(new { message = $"Role '{request.RoleName}' not found." });
        }

        var result = await _userManager.AddToRoleAsync(user, request.RoleName);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(error.Code, error.Description);
            }

            return ValidationProblem(ModelState);
        }

        _logger.LogInformation("Role {Role} assigned to user {Email}", request.RoleName, request.UserEmail);
        return NoContent();
    }
}


