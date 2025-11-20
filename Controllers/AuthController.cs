using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using PolicyPOC.Contracts.Auth;
using PolicyPOC.Models;
using PolicyPOC.Services;

namespace PolicyPOC.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController(
    UserManager<ApplicationUser> userManager,
    RoleManager<IdentityRole> roleManager,
    IJwtTokenService tokenService,
    IConfiguration configuration,
    ILogger<AuthController> logger) : ControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager = userManager;
    private readonly RoleManager<IdentityRole> _roleManager = roleManager;
    private readonly IJwtTokenService _tokenService = tokenService;
    private readonly IConfiguration _configuration = configuration;
    private readonly ILogger<AuthController> _logger = logger;

    [AllowAnonymous]
    [HttpPost("register")]
    public async Task<IActionResult> Register(RegisterUserRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var existingUser = await _userManager.FindByEmailAsync(request.Email);
        if (existingUser is not null)
        {
            return Conflict(new { message = "Email already registered." });
        }

        var user = new ApplicationUser
        {
            UserName = request.Email,
            Email = request.Email,
            DisplayName = request.DisplayName
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

        var defaultRole = _configuration.GetValue<string>("Identity:DefaultRegisterRole");
        if (!string.IsNullOrWhiteSpace(defaultRole) && await _roleManager.RoleExistsAsync(defaultRole))
        {
            var roleResult = await _userManager.AddToRoleAsync(user, defaultRole);
            if (!roleResult.Succeeded)
            {
                _logger.LogWarning("User {Email} created but role assignment failed: {Errors}", user.Email, string.Join(",", roleResult.Errors.Select(e => e.Description)));
            }
        }

        return CreatedAtAction(nameof(Register), new { email = user.Email }, new { user.Email, user.DisplayName });
    }

    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<ActionResult<AuthResponse>> Login(LoginRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var user = await _userManager.FindByEmailAsync(request.Email);
        if (user is null || !await _userManager.CheckPasswordAsync(user, request.Password))
        {
            return Unauthorized(new { message = "Invalid credentials." });
        }

        var roles = await _userManager.GetRolesAsync(user);
        var (token, expiresAtUtc) = _tokenService.CreateToken(user, roles);

        var response = new AuthResponse
        {
            Email = user.Email ?? string.Empty,
            DisplayName = user.DisplayName,
            Roles = roles.ToArray(),
            Token = token,
            ExpiresAtUtc = expiresAtUtc
        };

        return Ok(response);
    }
}


