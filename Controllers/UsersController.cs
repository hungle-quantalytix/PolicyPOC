using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PolicyPOC.Contracts.Auth;
using PolicyPOC.Models;

namespace PolicyPOC.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Administrator,SuperAdmin")]
public class UsersController(
    UserManager<ApplicationUser> userManager,
    ILogger<UsersController> logger) : ControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager = userManager;
    private readonly ILogger<UsersController> _logger = logger;

    [HttpGet]
    public async Task<IActionResult> GetUsers()
    {
        var users = await _userManager.Users
            .Select(u => new
            {
                u.Id,
                u.Email,
                u.DisplayName,
                u.UserName
            })
            .ToListAsync();

        var usersWithRoles = new List<object>();
        foreach (var user in users)
        {
            var roles = await _userManager.GetRolesAsync(
                await _userManager.FindByIdAsync(user.Id) ?? new ApplicationUser()
            );

            usersWithRoles.Add(new
            {
                user.Id,
                user.Email,
                user.DisplayName,
                user.UserName,
                Roles = roles.ToArray()
            });
        }

        return Ok(usersWithRoles);
    }

    [HttpPost]
    public async Task<IActionResult> CreateUser([FromBody] CreateUserRequest request)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var existingUser = await _userManager.FindByEmailAsync(request.Email);
        if (existingUser is not null)
        {
            return Conflict(new { message = "A user with this email already exists." });
        }

        var user = new ApplicationUser
        {
            Email = request.Email,
            UserName = request.Email,
            EmailConfirmed = true,
            DisplayName = request.DisplayName ?? request.Email
        };

        var result = await _userManager.CreateAsync(user, request.Password);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(error.Code, error.Description);
            }

            return ValidationProblem(ModelState);
        }

        _logger.LogInformation("User {Email} created successfully", request.Email);
        return CreatedAtAction(nameof(GetUsers), new { id = user.Id }, new { user.Id, user.Email, user.DisplayName });
    }

    [HttpPost("{userId}/roles/{roleName}")]
    public async Task<IActionResult> RemoveRoleFromUser(string userId, string roleName)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user is null)
        {
            return NotFound(new { message = $"User with ID '{userId}' not found." });
        }

        var result = await _userManager.RemoveFromRoleAsync(user, roleName);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(error.Code, error.Description);
            }

            return ValidationProblem(ModelState);
        }

        _logger.LogInformation("Role {Role} removed from user {Email}", roleName, user.Email);
        return NoContent();
    }
}

