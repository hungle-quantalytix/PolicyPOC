using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PolicyPOC.Contracts.Fields;
using PolicyPOC.Contracts.Resources;
using PolicyPOC.Data;
using PolicyPOC.Models;
using System.Security.Claims;

namespace PolicyPOC.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ResourcesController(
    ApplicationDbContext context,
    ILogger<ResourcesController> logger) : ControllerBase
{
    private readonly ApplicationDbContext _context = context;
    private readonly ILogger<ResourcesController> _logger = logger;

    // GET: api/Resources
    [HttpGet]
    public async Task<ActionResult<IEnumerable<ResourceResponse>>> GetResources()
    {
        var resources = await _context.Resources
            .Include(r => r.ReadPolicies)
            .Include(r => r.WritePolicies)
            .Include(r => r.Fields)
                .ThenInclude(f => f.ReadPolicies)
            .Include(r => r.Fields)
                .ThenInclude(f => f.WritePolicies)
            .Select(r => new ResourceResponse
            {
                Id = r.Id,
                ResourceName = r.ResourceName,
                ReadPolicyIds = r.ReadPolicies.Select(p => p.Id).ToList(),
                WritePolicyIds = r.WritePolicies.Select(p => p.Id).ToList(),
                Fields = r.Fields.Select(f => new FieldResponse
                {
                    Id = f.Id,
                    FieldName = f.FieldName,
                    IsPublic = f.IsPublic,
                    ResourceId = f.ResourceId,
                    ReadPolicyIds = f.ReadPolicies.Select(p => p.Id).ToList(),
                    WritePolicyIds = f.WritePolicies.Select(p => p.Id).ToList()
                }).ToList()
            })
            .ToListAsync();

        return Ok(resources);
    }

    // GET: api/Resources/{id}
    [HttpGet("{id}")]
    public async Task<ActionResult<ResourceResponse>> GetResource(Guid id)
    {
        var resource = await _context.Resources
            .Include(r => r.ReadPolicies)
            .Include(r => r.WritePolicies)
            .Include(r => r.Fields)
                .ThenInclude(f => f.ReadPolicies)
            .Include(r => r.Fields)
                .ThenInclude(f => f.WritePolicies)
            .FirstOrDefaultAsync(r => r.Id == id);

        if (resource == null)
        {
            return NotFound(new { message = $"Resource with ID '{id}' not found." });
        }

        var response = new ResourceResponse
        {
            Id = resource.Id,
            ResourceName = resource.ResourceName,
            ReadPolicyIds = resource.ReadPolicies.Select(p => p.Id).ToList(),
            WritePolicyIds = resource.WritePolicies.Select(p => p.Id).ToList(),
            Fields = resource.Fields.Select(f => new FieldResponse
            {
                Id = f.Id,
                FieldName = f.FieldName,
                IsPublic = f.IsPublic,
                ResourceId = f.ResourceId,
                ReadPolicyIds = f.ReadPolicies.Select(p => p.Id).ToList(),
                WritePolicyIds = f.WritePolicies.Select(p => p.Id).ToList()
            }).ToList()
        };

        return Ok(response);
    }

    // POST: api/Resources
    [HttpPost]
    public async Task<ActionResult<ResourceResponse>> CreateResource([FromBody] CreateResourceRequest request)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var userEmail = User.FindFirstValue(ClaimTypes.Email) ?? "system";

        var resource = new Resource
        {
            Id = Guid.NewGuid(),
            ResourceName = request.ResourceName
        };

        _context.Resources.Add(resource);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Resource {ResourceName} created successfully by {User}", resource.ResourceName, userEmail);

        var response = new ResourceResponse
        {
            Id = resource.Id,
            ResourceName = resource.ResourceName,
            Fields = new List<FieldResponse>()
        };

        return CreatedAtAction(nameof(GetResource), new { id = resource.Id }, response);
    }

    // PUT: api/Resources/{id}
    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateResource(Guid id, [FromBody] UpdateResourceRequest request)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var resource = await _context.Resources.FindAsync(id);
        if (resource == null)
        {
            return NotFound(new { message = $"Resource with ID '{id}' not found." });
        }

        var userEmail = User.FindFirstValue(ClaimTypes.Email) ?? "system";

        // Update resource properties
        resource.ResourceName = request.ResourceName;

        try
        {
            await _context.SaveChangesAsync();
            _logger.LogInformation("Resource {ResourceName} updated successfully by {User}", resource.ResourceName, userEmail);
        }
        catch (DbUpdateConcurrencyException)
        {
            if (!await ResourceExists(id))
            {
                return NotFound(new { message = $"Resource with ID '{id}' not found." });
            }
            throw;
        }

        return NoContent();
    }

    // DELETE: api/Resources/{id}
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteResource(Guid id)
    {
        var resource = await _context.Resources.FindAsync(id);
        if (resource == null)
        {
            return NotFound(new { message = $"Resource with ID '{id}' not found." });
        }

        var userEmail = User.FindFirstValue(ClaimTypes.Email) ?? "system";

        _context.Resources.Remove(resource);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Resource {ResourceName} deleted successfully by {User}", resource.ResourceName, userEmail);

        return NoContent();
    }

    // ==================== Field Endpoints ====================

    // GET: api/Resources/{resourceId}/fields
    [HttpGet("{resourceId}/fields")]
    public async Task<ActionResult<IEnumerable<FieldResponse>>> GetFields(Guid resourceId)
    {
        var resource = await _context.Resources
            .Include(r => r.Fields)
                .ThenInclude(f => f.ReadPolicies)
            .Include(r => r.Fields)
                .ThenInclude(f => f.WritePolicies)
            .FirstOrDefaultAsync(r => r.Id == resourceId);

        if (resource == null)
        {
            return NotFound(new { message = $"Resource with ID '{resourceId}' not found." });
        }

        var fields = resource.Fields.Select(f => new FieldResponse
        {
            Id = f.Id,
            FieldName = f.FieldName,
            IsPublic = f.IsPublic,
            ResourceId = f.ResourceId,
            ReadPolicyIds = f.ReadPolicies.Select(p => p.Id).ToList(),
            WritePolicyIds = f.WritePolicies.Select(p => p.Id).ToList()
        }).ToList();

        return Ok(fields);
    }

    // GET: api/Resources/{resourceId}/fields/{fieldId}
    [HttpGet("{resourceId}/fields/{fieldId}")]
    public async Task<ActionResult<FieldResponse>> GetField(Guid resourceId, Guid fieldId)
    {
        var field = await _context.Fields
            .Include(f => f.ReadPolicies)
            .Include(f => f.WritePolicies)
            .FirstOrDefaultAsync(f => f.Id == fieldId && f.ResourceId == resourceId);

        if (field == null)
        {
            return NotFound(new { message = $"Field with ID '{fieldId}' not found in resource '{resourceId}'." });
        }

        var response = new FieldResponse
        {
            Id = field.Id,
            FieldName = field.FieldName,
            IsPublic = field.IsPublic,
            ResourceId = field.ResourceId,
            ReadPolicyIds = field.ReadPolicies.Select(p => p.Id).ToList(),
            WritePolicyIds = field.WritePolicies.Select(p => p.Id).ToList()
        };

        return Ok(response);
    }

    // POST: api/Resources/{resourceId}/fields
    [HttpPost("{resourceId}/fields")]
    public async Task<ActionResult<FieldResponse>> CreateField(Guid resourceId, [FromBody] CreateFieldRequest request)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var resource = await _context.Resources.FindAsync(resourceId);
        if (resource == null)
        {
            return NotFound(new { message = $"Resource with ID '{resourceId}' not found." });
        }

        var userEmail = User.FindFirstValue(ClaimTypes.Email) ?? "system";

        var field = new Field
        {
            Id = Guid.NewGuid(),
            FieldName = request.FieldName,
            IsPublic = request.IsPublic,
            ResourceId = resourceId
        };

        _context.Fields.Add(field);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Field {FieldName} created for resource {ResourceId} by {User}", field.FieldName, resourceId, userEmail);

        var response = new FieldResponse
        {
            Id = field.Id,
            FieldName = field.FieldName,
            IsPublic = field.IsPublic,
            ResourceId = field.ResourceId,
            ReadPolicyIds = new List<Guid>(),
            WritePolicyIds = new List<Guid>()
        };

        return CreatedAtAction(nameof(GetField), new { resourceId = resourceId, fieldId = field.Id }, response);
    }

    // PUT: api/Resources/{resourceId}/fields/{fieldId}
    [HttpPut("{resourceId}/fields/{fieldId}")]
    public async Task<IActionResult> UpdateField(Guid resourceId, Guid fieldId, [FromBody] UpdateFieldRequest request)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var field = await _context.Fields.FirstOrDefaultAsync(f => f.Id == fieldId && f.ResourceId == resourceId);
        if (field == null)
        {
            return NotFound(new { message = $"Field with ID '{fieldId}' not found in resource '{resourceId}'." });
        }

        var userEmail = User.FindFirstValue(ClaimTypes.Email) ?? "system";

        field.FieldName = request.FieldName;
        field.IsPublic = request.IsPublic;

        try
        {
            await _context.SaveChangesAsync();
            _logger.LogInformation("Field {FieldName} updated in resource {ResourceId} by {User}", field.FieldName, resourceId, userEmail);
        }
        catch (DbUpdateConcurrencyException)
        {
            if (!await FieldExists(fieldId))
            {
                return NotFound(new { message = $"Field with ID '{fieldId}' not found." });
            }
            throw;
        }

        return NoContent();
    }

    // DELETE: api/Resources/{resourceId}/fields/{fieldId}
    [HttpDelete("{resourceId}/fields/{fieldId}")]
    public async Task<IActionResult> DeleteField(Guid resourceId, Guid fieldId)
    {
        var field = await _context.Fields.FirstOrDefaultAsync(f => f.Id == fieldId && f.ResourceId == resourceId);
        if (field == null)
        {
            return NotFound(new { message = $"Field with ID '{fieldId}' not found in resource '{resourceId}'." });
        }

        var userEmail = User.FindFirstValue(ClaimTypes.Email) ?? "system";

        _context.Fields.Remove(field);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Field {FieldName} deleted from resource {ResourceId} by {User}", field.FieldName, resourceId, userEmail);

        return NoContent();
    }

    // ==================== Policy Assignment for Resources ====================

    // POST: api/Resources/{resourceId}/assign-policy
    [HttpPost("{resourceId}/assign-policy")]
    public async Task<IActionResult> AssignPolicyToResource(Guid resourceId, [FromBody] AssignResourcePolicyRequest request)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var resource = await _context.Resources
            .Include(r => r.ReadPolicies)
            .Include(r => r.WritePolicies)
            .FirstOrDefaultAsync(r => r.Id == resourceId);

        if (resource == null)
        {
            return NotFound(new { message = $"Resource with ID '{resourceId}' not found." });
        }

        var policy = await _context.Policies.FindAsync(request.PolicyId);
        if (policy == null)
        {
            return NotFound(new { message = $"Policy with ID '{request.PolicyId}' not found." });
        }

        var userEmail = User.FindFirstValue(ClaimTypes.Email) ?? "system";
        var action = request.Action?.ToLower() ?? "read";

        if (action == "read")
        {
            if (!resource.ReadPolicies.Any(p => p.Id == policy.Id))
            {
                resource.ReadPolicies.Add(policy);
            }
        }
        else if (action == "write")
        {
            if (!resource.WritePolicies.Any(p => p.Id == policy.Id))
            {
                resource.WritePolicies.Add(policy);
            }
        }
        else
        {
            return BadRequest(new { message = $"Invalid action '{request.Action}'. Supported actions: read, write" });
        }

        await _context.SaveChangesAsync();

        _logger.LogInformation("Policy {PolicyId} assigned to resource {ResourceId} for action {Action} by {User}",
            request.PolicyId, resourceId, action, userEmail);

        return Ok(new
        {
            message = $"Policy assigned to resource for action '{action}'",
            policyId = policy.Id,
            resourceId = resource.Id,
            action = action
        });
    }

    // DELETE: api/Resources/{resourceId}/unassign-policy
    [HttpDelete("{resourceId}/unassign-policy")]
    public async Task<IActionResult> UnassignPolicyFromResource(Guid resourceId, [FromBody] AssignResourcePolicyRequest request)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var resource = await _context.Resources
            .Include(r => r.ReadPolicies)
            .Include(r => r.WritePolicies)
            .FirstOrDefaultAsync(r => r.Id == resourceId);

        if (resource == null)
        {
            return NotFound(new { message = $"Resource with ID '{resourceId}' not found." });
        }

        var userEmail = User.FindFirstValue(ClaimTypes.Email) ?? "system";
        var action = request.Action?.ToLower() ?? "read";

        Policy? policyToRemove = null;

        if (action == "read")
        {
            policyToRemove = resource.ReadPolicies.FirstOrDefault(p => p.Id == request.PolicyId);
            if (policyToRemove != null)
            {
                resource.ReadPolicies.Remove(policyToRemove);
            }
        }
        else if (action == "write")
        {
            policyToRemove = resource.WritePolicies.FirstOrDefault(p => p.Id == request.PolicyId);
            if (policyToRemove != null)
            {
                resource.WritePolicies.Remove(policyToRemove);
            }
        }
        else
        {
            return BadRequest(new { message = $"Invalid action '{request.Action}'. Supported actions: read, write" });
        }

        if (policyToRemove == null)
        {
            return NotFound(new { message = $"Policy with ID '{request.PolicyId}' is not assigned to this resource for action '{action}'." });
        }

        await _context.SaveChangesAsync();

        _logger.LogInformation("Policy {PolicyId} unassigned from resource {ResourceId} for action {Action} by {User}",
            request.PolicyId, resourceId, action, userEmail);

        return NoContent();
    }

    // ==================== Policy Assignment for Fields ====================

    // POST: api/Resources/{resourceId}/fields/{fieldId}/assign-policy
    [HttpPost("{resourceId}/fields/{fieldId}/assign-policy")]
    public async Task<IActionResult> AssignPolicyToField(Guid resourceId, Guid fieldId, [FromBody] AssignResourcePolicyRequest request)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var field = await _context.Fields
            .Include(f => f.ReadPolicies)
            .Include(f => f.WritePolicies)
            .FirstOrDefaultAsync(f => f.Id == fieldId && f.ResourceId == resourceId);

        if (field == null)
        {
            return NotFound(new { message = $"Field with ID '{fieldId}' not found in resource '{resourceId}'." });
        }

        var policy = await _context.Policies.FindAsync(request.PolicyId);
        if (policy == null)
        {
            return NotFound(new { message = $"Policy with ID '{request.PolicyId}' not found." });
        }

        var userEmail = User.FindFirstValue(ClaimTypes.Email) ?? "system";
        var action = request.Action?.ToLower() ?? "read";

        if (action == "read")
        {
            if (!field.ReadPolicies.Any(p => p.Id == policy.Id))
            {
                field.ReadPolicies.Add(policy);
            }
        }
        else if (action == "write")
        {
            if (!field.WritePolicies.Any(p => p.Id == policy.Id))
            {
                field.WritePolicies.Add(policy);
            }
        }
        else
        {
            return BadRequest(new { message = $"Invalid action '{request.Action}'. Supported actions: read, write" });
        }

        await _context.SaveChangesAsync();

        _logger.LogInformation("Policy {PolicyId} assigned to field {FieldId} in resource {ResourceId} for action {Action} by {User}",
            request.PolicyId, fieldId, resourceId, action, userEmail);

        return Ok(new
        {
            message = $"Policy assigned to field for action '{action}'",
            policyId = policy.Id,
            fieldId = field.Id,
            resourceId = resourceId,
            action = action
        });
    }

    // DELETE: api/Resources/{resourceId}/fields/{fieldId}/unassign-policy
    [HttpDelete("{resourceId}/fields/{fieldId}/unassign-policy")]
    public async Task<IActionResult> UnassignPolicyFromField(Guid resourceId, Guid fieldId, [FromBody] AssignResourcePolicyRequest request)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var field = await _context.Fields
            .Include(f => f.ReadPolicies)
            .Include(f => f.WritePolicies)
            .FirstOrDefaultAsync(f => f.Id == fieldId && f.ResourceId == resourceId);

        if (field == null)
        {
            return NotFound(new { message = $"Field with ID '{fieldId}' not found in resource '{resourceId}'." });
        }

        var userEmail = User.FindFirstValue(ClaimTypes.Email) ?? "system";
        var action = request.Action?.ToLower() ?? "read";

        Policy? policyToRemove = null;

        if (action == "read")
        {
            policyToRemove = field.ReadPolicies.FirstOrDefault(p => p.Id == request.PolicyId);
            if (policyToRemove != null)
            {
                field.ReadPolicies.Remove(policyToRemove);
            }
        }
        else if (action == "write")
        {
            policyToRemove = field.WritePolicies.FirstOrDefault(p => p.Id == request.PolicyId);
            if (policyToRemove != null)
            {
                field.WritePolicies.Remove(policyToRemove);
            }
        }
        else
        {
            return BadRequest(new { message = $"Invalid action '{request.Action}'. Supported actions: read, write" });
        }

        if (policyToRemove == null)
        {
            return NotFound(new { message = $"Policy with ID '{request.PolicyId}' is not assigned to this field for action '{action}'." });
        }

        await _context.SaveChangesAsync();

        _logger.LogInformation("Policy {PolicyId} unassigned from field {FieldId} in resource {ResourceId} for action {Action} by {User}",
            request.PolicyId, fieldId, resourceId, action, userEmail);

        return NoContent();
    }

    private async Task<bool> ResourceExists(Guid id)
    {
        return await _context.Resources.AnyAsync(e => e.Id == id);
    }

    private async Task<bool> FieldExists(Guid id)
    {
        return await _context.Fields.AnyAsync(e => e.Id == id);
    }
}

// Request model for policy assignment
public class AssignResourcePolicyRequest
{
    public required Guid PolicyId { get; set; }
    
    /// <summary>
    /// Action type: "read" or "write"
    /// </summary>
    public string? Action { get; set; }
}
