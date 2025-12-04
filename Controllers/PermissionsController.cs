using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PolicyPOC.Contracts.Permissions;
using PolicyPOC.Data;
using PolicyPOC.Models;
using System.Security.Claims;

namespace PolicyPOC.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class PermissionsController(
    ApplicationDbContext context,
    UserManager<ApplicationUser> userManager,
    RoleManager<IdentityRole> roleManager,
    ILogger<PermissionsController> logger) : ControllerBase
{
    private readonly ApplicationDbContext _context = context;
    private readonly UserManager<ApplicationUser> _userManager = userManager;
    private readonly RoleManager<IdentityRole> _roleManager = roleManager;
    private readonly ILogger<PermissionsController> _logger = logger;

    // GET: api/Permissions
    [HttpGet]
    public async Task<ActionResult<IEnumerable<PermissionResponse>>> GetPermissions(
        [FromQuery] string? resourceType = null,
        [FromQuery] string? subjectType = null,
        [FromQuery] string? action = null)
    {
        var query = _context.Permissions.AsQueryable();

        if (!string.IsNullOrEmpty(resourceType))
            query = query.Where(p => p.ResourceType == resourceType);
        
        if (!string.IsNullOrEmpty(subjectType))
            query = query.Where(p => p.SubjectType == subjectType);
        
        if (!string.IsNullOrEmpty(action))
            query = query.Where(p => p.Action == action);

        var permissions = await query
            .OrderBy(p => p.ResourceType)
            .ThenBy(p => p.FieldName)
            .ThenBy(p => p.Action)
            .ThenBy(p => p.SubjectType)
            .ToListAsync();

        var responses = new List<PermissionResponse>();
        foreach (var p in permissions)
        {
            responses.Add(new PermissionResponse
            {
                Id = p.Id,
                ResourceType = p.ResourceType,
                ResourceId = p.ResourceId,
                FieldName = p.FieldName,
                Action = p.Action,
                SubjectType = p.SubjectType,
                SubjectId = p.SubjectId,
                CreatedAt = p.CreatedAt,
                Description = p.Description,
                SubjectDisplayName = await GetSubjectDisplayNameAsync(p.SubjectType, p.SubjectId)
            });
        }

        return Ok(responses);
    }

    // GET: api/Permissions/{id}
    [HttpGet("{id}")]
    public async Task<ActionResult<PermissionResponse>> GetPermission(Guid id)
    {
        var permission = await _context.Permissions.FindAsync(id);

        if (permission == null)
        {
            return NotFound(new { message = $"Permission with ID '{id}' not found." });
        }

        var response = new PermissionResponse
        {
            Id = permission.Id,
            ResourceType = permission.ResourceType,
            ResourceId = permission.ResourceId,
            FieldName = permission.FieldName,
            Action = permission.Action,
            SubjectType = permission.SubjectType,
            SubjectId = permission.SubjectId,
            CreatedAt = permission.CreatedAt,
            Description = permission.Description,
            SubjectDisplayName = await GetSubjectDisplayNameAsync(permission.SubjectType, permission.SubjectId)
        };

        return Ok(response);
    }

    // POST: api/Permissions
    [HttpPost]
    public async Task<ActionResult<PermissionResponse>> CreatePermission([FromBody] CreatePermissionRequest request)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        // Validate SubjectType
        if (!SubjectTypes.PriorityOrder.Contains(request.SubjectType))
        {
            return BadRequest(new { message = $"Invalid SubjectType '{request.SubjectType}'. Allowed: {string.Join(", ", SubjectTypes.PriorityOrder)}" });
        }

        // Validate SubjectId based on SubjectType
        var validationResult = await ValidateSubjectAsync(request.SubjectType, request.SubjectId);
        if (!validationResult.IsValid)
        {
            return BadRequest(new { message = validationResult.ErrorMessage });
        }

        // Check for duplicate
        var exists = await _context.Permissions.AnyAsync(p =>
            p.ResourceType == request.ResourceType &&
            p.ResourceId == request.ResourceId &&
            p.FieldName == request.FieldName &&
            p.Action == request.Action &&
            p.SubjectType == request.SubjectType &&
            p.SubjectId == request.SubjectId);

        if (exists)
        {
            return Conflict(new { message = "A permission with this combination already exists." });
        }

        var userEmail = User.FindFirstValue(ClaimTypes.Email) ?? "system";

        var permission = new Permission
        {
            Id = Guid.NewGuid(),
            ResourceType = request.ResourceType,
            ResourceId = request.ResourceId,
            FieldName = request.FieldName,
            Action = request.Action,
            SubjectType = request.SubjectType,
            SubjectId = request.SubjectId,
            Description = request.Description,
            CreatedAt = DateTime.UtcNow
        };

        _context.Permissions.Add(permission);
        await _context.SaveChangesAsync();

        _logger.LogInformation(
            "Permission {PermissionId} created by {User}: {ResourceType}:{FieldName}:{Action} -> {SubjectType}:{SubjectId}",
            permission.Id, userEmail, permission.ResourceType, permission.FieldName ?? "*",
            permission.Action, permission.SubjectType, permission.SubjectId);

        var response = new PermissionResponse
        {
            Id = permission.Id,
            ResourceType = permission.ResourceType,
            ResourceId = permission.ResourceId,
            FieldName = permission.FieldName,
            Action = permission.Action,
            SubjectType = permission.SubjectType,
            SubjectId = permission.SubjectId,
            CreatedAt = permission.CreatedAt,
            Description = permission.Description,
            SubjectDisplayName = await GetSubjectDisplayNameAsync(permission.SubjectType, permission.SubjectId)
        };

        return CreatedAtAction(nameof(GetPermission), new { id = permission.Id }, response);
    }

    // PUT: api/Permissions/{id}
    [HttpPut("{id}")]
    public async Task<IActionResult> UpdatePermission(Guid id, [FromBody] UpdatePermissionRequest request)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var permission = await _context.Permissions.FindAsync(id);
        if (permission == null)
        {
            return NotFound(new { message = $"Permission with ID '{id}' not found." });
        }

        // Validate SubjectType if changed
        if (!string.IsNullOrEmpty(request.SubjectType) && !SubjectTypes.PriorityOrder.Contains(request.SubjectType))
        {
            return BadRequest(new { message = $"Invalid SubjectType '{request.SubjectType}'. Allowed: {string.Join(", ", SubjectTypes.PriorityOrder)}" });
        }

        // Validate SubjectId if SubjectType or SubjectId changed
        var newSubjectType = request.SubjectType ?? permission.SubjectType;
        var newSubjectId = request.SubjectId ?? permission.SubjectId;
        
        if (request.SubjectType != null || request.SubjectId != null)
        {
            var validationResult = await ValidateSubjectAsync(newSubjectType, newSubjectId);
            if (!validationResult.IsValid)
            {
                return BadRequest(new { message = validationResult.ErrorMessage });
            }
        }

        var userEmail = User.FindFirstValue(ClaimTypes.Email) ?? "system";

        // Update fields
        if (request.ResourceType != null) permission.ResourceType = request.ResourceType;
        if (request.ResourceId != null) permission.ResourceId = request.ResourceId == "" ? null : request.ResourceId;
        if (request.FieldName != null) permission.FieldName = request.FieldName == "" ? null : request.FieldName;
        if (request.Action != null) permission.Action = request.Action;
        if (request.SubjectType != null) permission.SubjectType = request.SubjectType;
        if (request.SubjectId != null) permission.SubjectId = request.SubjectId;
        if (request.Description != null) permission.Description = request.Description == "" ? null : request.Description;

        try
        {
            await _context.SaveChangesAsync();
            _logger.LogInformation("Permission {PermissionId} updated by {User}", permission.Id, userEmail);
        }
        catch (DbUpdateConcurrencyException)
        {
            if (!await PermissionExistsAsync(id))
            {
                return NotFound(new { message = $"Permission with ID '{id}' not found." });
            }
            throw;
        }

        return NoContent();
    }

    // DELETE: api/Permissions/{id}
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeletePermission(Guid id)
    {
        var permission = await _context.Permissions.FindAsync(id);
        if (permission == null)
        {
            return NotFound(new { message = $"Permission with ID '{id}' not found." });
        }

        var userEmail = User.FindFirstValue(ClaimTypes.Email) ?? "system";

        _context.Permissions.Remove(permission);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Permission {PermissionId} deleted by {User}", id, userEmail);

        return NoContent();
    }

    // GET: api/Permissions/subjects
    // Get available subjects (users, roles, policies) for selection
    [HttpGet("subjects")]
    public async Task<IActionResult> GetAvailableSubjects()
    {
        var users = await _userManager.Users
            .Select(u => new { id = u.Id, name = u.Email ?? u.UserName, type = "User" })
            .ToListAsync();

        var roles = await _roleManager.Roles
            .Select(r => new { id = r.Name!, name = r.Name!, type = "Role" })
            .ToListAsync();

        var policies = await _context.Policies
            .Select(p => new { id = p.Id.ToString(), name = p.Description ?? p.Id.ToString(), type = "Policy" })
            .ToListAsync();

        return Ok(new
        {
            users,
            roles,
            policies
        });
    }

    // GET: api/Permissions/resources
    // Get available resource types
    [HttpGet("resources")]
    public async Task<IActionResult> GetAvailableResources()
    {
        var resources = await _context.Resources
            .Include(r => r.Fields)
            .Select(r => new 
            { 
                name = r.ResourceName,
                fields = r.Fields.Select(f => f.FieldName).ToList()
            })
            .ToListAsync();

        return Ok(resources);
    }

    #region Private Methods

    private async Task<bool> PermissionExistsAsync(Guid id)
    {
        return await _context.Permissions.AnyAsync(e => e.Id == id);
    }

    private async Task<string?> GetSubjectDisplayNameAsync(string subjectType, string subjectId)
    {
        return subjectType switch
        {
            SubjectTypes.User => (await _userManager.FindByIdAsync(subjectId))?.Email,
            SubjectTypes.Role => subjectId, // Role name is the ID
            SubjectTypes.Policy => (await _context.Policies.FindAsync(Guid.TryParse(subjectId, out var id) ? id : Guid.Empty))?.Description ?? subjectId,
            _ => subjectId
        };
    }

    private async Task<(bool IsValid, string? ErrorMessage)> ValidateSubjectAsync(string subjectType, string subjectId)
    {
        switch (subjectType)
        {
            case SubjectTypes.User:
                var user = await _userManager.FindByIdAsync(subjectId);
                if (user == null)
                    return (false, $"User with ID '{subjectId}' not found.");
                break;
                
            case SubjectTypes.Role:
                var roleExists = await _roleManager.RoleExistsAsync(subjectId);
                if (!roleExists)
                    return (false, $"Role '{subjectId}' not found.");
                break;
                
            case SubjectTypes.Policy:
                if (!Guid.TryParse(subjectId, out var policyId))
                    return (false, $"Invalid Policy ID format: '{subjectId}'");
                var policyExists = await _context.Policies.AnyAsync(p => p.Id == policyId);
                if (!policyExists)
                    return (false, $"Policy with ID '{subjectId}' not found.");
                break;
                
            default:
                return (false, $"Unknown SubjectType: '{subjectType}'");
        }
        
        return (true, null);
    }

    #endregion
}

