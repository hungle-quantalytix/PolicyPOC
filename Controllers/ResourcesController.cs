using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
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
            .Select(r => new ResourceResponse
            {
                Id = r.Id,
                ResourceName = r.ResourceName
            })
            .ToListAsync();

        return Ok(resources);
    }

    // GET: api/Resources/{id}
    [HttpGet("{id}")]
    public async Task<ActionResult<ResourceResponse>> GetResource(Guid id)
    {
        var resource = await _context.Resources.FindAsync(id);

        if (resource == null)
        {
            return NotFound(new { message = $"Resource with ID '{id}' not found." });
        }

        var response = new ResourceResponse
        {
            Id = resource.Id,
            ResourceName = resource.ResourceName
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
            ResourceName = resource.ResourceName
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

    private async Task<bool> ResourceExists(Guid id)
    {
        return await _context.Resources.AnyAsync(e => e.Id == id);
    }
}

