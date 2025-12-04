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
            .Include(r => r.Fields)
            .Select(r => new ResourceResponse
            {
                Id = r.Id,
                ResourceName = r.ResourceName,
                Fields = r.Fields.Select(f => new FieldResponse
                {
                    Id = f.Id,
                    FieldName = f.FieldName,
                    MaskFormat = f.MaskFormat,
                    ResourceId = f.ResourceId
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
            .Include(r => r.Fields)
            .FirstOrDefaultAsync(r => r.Id == id);

        if (resource == null)
        {
            return NotFound(new { message = $"Resource with ID '{id}' not found." });
        }

        var response = new ResourceResponse
        {
            Id = resource.Id,
            ResourceName = resource.ResourceName,
            Fields = resource.Fields.Select(f => new FieldResponse
            {
                Id = f.Id,
                FieldName = f.FieldName,
                MaskFormat = f.MaskFormat,
                ResourceId = f.ResourceId
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
            .FirstOrDefaultAsync(r => r.Id == resourceId);

        if (resource == null)
        {
            return NotFound(new { message = $"Resource with ID '{resourceId}' not found." });
        }

        var fields = resource.Fields.Select(f => new FieldResponse
        {
            Id = f.Id,
            FieldName = f.FieldName,
            MaskFormat = f.MaskFormat,
            ResourceId = f.ResourceId
        }).ToList();

        return Ok(fields);
    }

    // GET: api/Resources/{resourceId}/fields/{fieldId}
    [HttpGet("{resourceId}/fields/{fieldId}")]
    public async Task<ActionResult<FieldResponse>> GetField(Guid resourceId, Guid fieldId)
    {
        var field = await _context.Fields
            .FirstOrDefaultAsync(f => f.Id == fieldId && f.ResourceId == resourceId);

        if (field == null)
        {
            return NotFound(new { message = $"Field with ID '{fieldId}' not found in resource '{resourceId}'." });
        }

        var response = new FieldResponse
        {
            Id = field.Id,
            FieldName = field.FieldName,
            MaskFormat = field.MaskFormat,
            ResourceId = field.ResourceId
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
            MaskFormat = request.MaskFormat,
            ResourceId = resourceId
        };

        _context.Fields.Add(field);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Field {FieldName} created for resource {ResourceId} by {User}", field.FieldName, resourceId, userEmail);

        var response = new FieldResponse
        {
            Id = field.Id,
            FieldName = field.FieldName,
            MaskFormat = field.MaskFormat,
            ResourceId = field.ResourceId
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
        field.MaskFormat = request.MaskFormat;

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

    private async Task<bool> ResourceExists(Guid id)
    {
        return await _context.Resources.AnyAsync(e => e.Id == id);
    }

    private async Task<bool> FieldExists(Guid id)
    {
        return await _context.Fields.AnyAsync(e => e.Id == id);
    }
}
