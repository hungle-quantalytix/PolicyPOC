using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PolicyPOC.Contracts.Policies;
using PolicyPOC.Data;
using PolicyPOC.Models;
using System.Security.Claims;

namespace PolicyPOC.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class PoliciesController(
    ApplicationDbContext context,
    ILogger<PoliciesController> logger) : ControllerBase
{
    private readonly ApplicationDbContext _context = context;
    private readonly ILogger<PoliciesController> _logger = logger;

    // GET: api/Policies
    [HttpGet]
    public async Task<ActionResult<IEnumerable<PolicyResponse>>> GetPolicies()
    {
        var policies = await _context.Policies
            .Include(p => p.ReadResources)
            .Include(p => p.WriteResources)
            .Select(p => new PolicyResponse
            {
                Id = p.Id,
                Description = p.Description,
                PolicyData = p.PolicyData,
                ReadResourceNames = p.ReadResources.Select(r => r.ResourceName).ToList(),
                WriteResourceNames = p.WriteResources.Select(r => r.ResourceName).ToList()
            })
            .ToListAsync();

        return Ok(policies);
    }

    // GET: api/Policies/{id}
    [HttpGet("{id}")]
    public async Task<ActionResult<PolicyResponse>> GetPolicy(Guid id)
    {
        var policy = await _context.Policies
            .Include(p => p.ReadResources)
            .Include(p => p.WriteResources)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (policy == null)
        {
            return NotFound(new { message = $"Policy with ID '{id}' not found." });
        }

        var response = new PolicyResponse
        {
            Id = policy.Id,
            Description = policy.Description,
            PolicyData = policy.PolicyData,
            ReadResourceNames = policy.ReadResources.Select(r => r.ResourceName).ToList(),
            WriteResourceNames = policy.WriteResources.Select(r => r.ResourceName).ToList()
        };

        return Ok(response);
    }

    // POST: api/Policies
    [HttpPost]
    public async Task<ActionResult<PolicyResponse>> CreatePolicy([FromBody] CreatePolicyRequest request)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var userEmail = User.FindFirstValue(ClaimTypes.Email) ?? "system";

        var policy = new Policy
        {
            Id = Guid.NewGuid(),
            Description = request.Description,
            PolicyData = request.PolicyData
        };

        _context.Policies.Add(policy);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Policy {PolicyId} created successfully by {User}", policy.Id, userEmail);

        var response = new PolicyResponse
        {
            Id = policy.Id,
            Description = policy.Description,
            PolicyData = policy.PolicyData,
            ReadResourceNames = new List<string>(),
            WriteResourceNames = new List<string>()
        };

        return CreatedAtAction(nameof(GetPolicy), new { id = policy.Id }, response);
    }

    // PUT: api/Policies/{id}
    [HttpPut("{id}")]
    public async Task<IActionResult> UpdatePolicy(Guid id, [FromBody] UpdatePolicyRequest request)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var policy = await _context.Policies.FindAsync(id);
        if (policy == null)
        {
            return NotFound(new { message = $"Policy with ID '{id}' not found." });
        }

        var userEmail = User.FindFirstValue(ClaimTypes.Email) ?? "system";

        // Update policy properties
        policy.Description = request.Description;
        policy.PolicyData = request.PolicyData;

        try
        {
            await _context.SaveChangesAsync();
            _logger.LogInformation("Policy {PolicyId} updated successfully by {User}", policy.Id, userEmail);
        }
        catch (DbUpdateConcurrencyException)
        {
            if (!await PolicyExists(id))
            {
                return NotFound(new { message = $"Policy with ID '{id}' not found." });
            }
            throw;
        }

        return NoContent();
    }

    // DELETE: api/Policies/{id}
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeletePolicy(Guid id)
    {
        var policy = await _context.Policies
            .Include(p => p.ReadResources)
            .Include(p => p.WriteResources)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (policy == null)
        {
            return NotFound(new { message = $"Policy with ID '{id}' not found." });
        }

        var userEmail = User.FindFirstValue(ClaimTypes.Email) ?? "system";

        _context.Policies.Remove(policy);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Policy {PolicyId} deleted successfully by {User}", policy.Id, userEmail);

        return NoContent();
    }

    // POST: api/Policies/assign
    // Assigns a policy to a resource for a specific action (read/write)
    [HttpPost("assign")]
    public async Task<IActionResult> AssignPolicy([FromBody] AssignPolicyRequest request)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var policy = await _context.Policies
            .Include(p => p.ReadResources)
            .Include(p => p.WriteResources)
            .FirstOrDefaultAsync(p => p.Id == request.PolicyId);
            
        if (policy == null)
        {
            return NotFound(new { message = $"Policy with ID '{request.PolicyId}' not found." });
        }

        var resource = await _context.Resources
            .Include(r => r.ReadPolicies)
            .Include(r => r.WritePolicies)
            .FirstOrDefaultAsync(r => r.ResourceName == request.ResourceName);
            
        if (resource == null)
        {
            return NotFound(new { message = $"Resource with name '{request.ResourceName}' not found." });
        }

        var userEmail = User.FindFirstValue(ClaimTypes.Email) ?? "system";

        // Assign based on action type
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

        _logger.LogInformation("Policy {PolicyId} assigned to resource {ResourceName} for action {Action} by {User}", 
            request.PolicyId, request.ResourceName, action, userEmail);

        return Ok(new 
        { 
            message = $"Policy assigned to resource '{request.ResourceName}' for action '{action}'",
            policyId = policy.Id,
            resourceName = resource.ResourceName,
            action = action
        });
    }

    // DELETE: api/Policies/unassign
    // Removes a policy assignment from a resource
    [HttpDelete("unassign")]
    public async Task<IActionResult> UnassignPolicy([FromBody] AssignPolicyRequest request)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var resource = await _context.Resources
            .Include(r => r.ReadPolicies)
            .Include(r => r.WritePolicies)
            .FirstOrDefaultAsync(r => r.ResourceName == request.ResourceName);
            
        if (resource == null)
        {
            return NotFound(new { message = $"Resource with name '{request.ResourceName}' not found." });
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
            return NotFound(new { message = $"Policy with ID '{request.PolicyId}' is not assigned to resource '{request.ResourceName}' for action '{action}'." });
        }

        await _context.SaveChangesAsync();

        _logger.LogInformation("Policy {PolicyId} unassigned from resource {ResourceName} for action {Action} by {User}", 
            request.PolicyId, request.ResourceName, action, userEmail);

        return NoContent();
    }

    // GET: api/Policies/{id}/resources
    // Gets all resources that have this policy assigned
    [HttpGet("{id}/resources")]
    public async Task<IActionResult> GetPolicyResources(Guid id)
    {
        var policy = await _context.Policies
            .Include(p => p.ReadResources)
            .Include(p => p.WriteResources)
            .FirstOrDefaultAsync(p => p.Id == id);
            
        if (policy == null)
        {
            return NotFound(new { message = $"Policy with ID '{id}' not found." });
        }

        return Ok(new
        {
            policyId = policy.Id,
            description = policy.Description,
            readResources = policy.ReadResources.Select(r => new { r.Id, r.ResourceName }).ToList(),
            writeResources = policy.WriteResources.Select(r => new { r.Id, r.ResourceName }).ToList()
        });
    }

    private async Task<bool> PolicyExists(Guid id)
    {
        return await _context.Policies.AnyAsync(e => e.Id == id);
    }
}

