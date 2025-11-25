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
            .Include(p => p.PolicyResources)
            .Select(p => new PolicyResponse
            {
                Id = p.Id,
                Description = p.Description,
                PolicyData = p.PolicyData,
                PolicyResources = p.PolicyResources.Select(pr => new PolicyResourceResponse
                {
                    Id = pr.Id,
                    PolicyId = pr.PolicyId,
                    ResourceName = pr.ResourceName,
                    ResourceColumns = pr.ResourceColumns,
                    Action = pr.Action,
                    Effect = pr.Effect
                }).ToList()
            })
            .ToListAsync();

        return Ok(policies);
    }

    // GET: api/Policies/{id}
    [HttpGet("{id}")]
    public async Task<ActionResult<PolicyResponse>> GetPolicy(Guid id)
    {
        var policy = await _context.Policies
            .Include(p => p.PolicyResources)
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
            PolicyResources = policy.PolicyResources.Select(pr => new PolicyResourceResponse
            {
                Id = pr.Id,
                PolicyId = pr.PolicyId,
                ResourceName = pr.ResourceName,
                ResourceColumns = pr.ResourceColumns,
                Action = pr.Action,
                Effect = pr.Effect
            }).ToList()
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
            PolicyData = request.PolicyData,
            PolicyResources = new List<PolicyResource>()
        };

        _context.Policies.Add(policy);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Policy {PolicyId} created successfully by {User}", policy.Id, userEmail);

        var response = new PolicyResponse
        {
            Id = policy.Id,
            Description = policy.Description,
            PolicyData = policy.PolicyData,
            PolicyResources = new List<PolicyResourceResponse>()
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
            .Include(p => p.PolicyResources)
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
    [HttpPost("assign")]
    public async Task<ActionResult<PolicyResourceResponse>> AssignPolicy([FromBody] AssignPolicyRequest request)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var policy = await _context.Policies.FindAsync(request.PolicyId);
        if (policy == null)
        {
            return NotFound(new { message = $"Policy with ID '{request.PolicyId}' not found." });
        }

        var userEmail = User.FindFirstValue(ClaimTypes.Email) ?? "system";

        var policyResource = new PolicyResource
        {
            Id = Guid.NewGuid(),
            PolicyId = request.PolicyId,
            ResourceName = request.ResourceName,
            ResourceColumns = request.ResourceColumns,
            Action = request.Action,
            Effect = request.Effect,
            Policy = policy
        };

        _context.PolicyResources.Add(policyResource);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Policy {PolicyId} assigned to resource {ResourceName} by {User}", 
            request.PolicyId, request.ResourceName, userEmail);

        var response = new PolicyResourceResponse
        {
            Id = policyResource.Id,
            PolicyId = policyResource.PolicyId,
            ResourceName = policyResource.ResourceName,
            ResourceColumns = policyResource.ResourceColumns,
            Action = policyResource.Action,
            Effect = policyResource.Effect
        };

        return Ok(response);
    }

    // PUT: api/Policies/assign/{id}
    [HttpPut("assign/{id}")]
    public async Task<IActionResult> UpdatePolicyAssignment(Guid id, [FromBody] UpdatePolicyAssignmentRequest request)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        if (id != request.AssignmentId)
        {
            return BadRequest(new { message = "Assignment ID in URL does not match the request body." });
        }

        var policyResource = await _context.PolicyResources.FindAsync(id);
        if (policyResource == null)
        {
            return NotFound(new { message = $"Policy assignment with ID '{id}' not found." });
        }

        var userEmail = User.FindFirstValue(ClaimTypes.Email) ?? "system";

        // Update only the fields that are provided
        if (request.ResourceName != null)
        {
            policyResource.ResourceName = request.ResourceName;
        }
        
        if (request.ResourceColumns != null)
        {
            policyResource.ResourceColumns = request.ResourceColumns;
        }
        
        if (request.Action != null)
        {
            policyResource.Action = request.Action;
        }
        
        if (request.Effect != null)
        {
            policyResource.Effect = request.Effect;
        }

        try
        {
            await _context.SaveChangesAsync();
            _logger.LogInformation("Policy assignment {Id} updated successfully by {User}", id, userEmail);
        }
        catch (DbUpdateConcurrencyException)
        {
            if (!await _context.PolicyResources.AnyAsync(pr => pr.Id == id))
            {
                return NotFound(new { message = $"Policy assignment with ID '{id}' not found." });
            }
            throw;
        }

        return NoContent();
    }

    // DELETE: api/Policies/assign/{id}
    [HttpDelete("assign/{id}")]
    public async Task<IActionResult> UnassignPolicy(Guid id)
    {
        var policyResource = await _context.PolicyResources.FindAsync(id);
        if (policyResource == null)
        {
            return NotFound(new { message = $"Policy assignment with ID '{id}' not found." });
        }

        var userEmail = User.FindFirstValue(ClaimTypes.Email) ?? "system";

        _context.PolicyResources.Remove(policyResource);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Policy assignment {Id} removed by {User}", id, userEmail);

        return NoContent();
    }

    // GET: api/Policies/{id}/assignments
    [HttpGet("{id}/assignments")]
    public async Task<ActionResult<IEnumerable<PolicyResourceResponse>>> GetPolicyAssignments(Guid id)
    {
        var policy = await _context.Policies.FindAsync(id);
        if (policy == null)
        {
            return NotFound(new { message = $"Policy with ID '{id}' not found." });
        }

        var assignments = await _context.PolicyResources
            .Where(pr => pr.PolicyId == id)
            .Select(pr => new PolicyResourceResponse
            {
                Id = pr.Id,
                PolicyId = pr.PolicyId,
                ResourceName = pr.ResourceName,
                ResourceColumns = pr.ResourceColumns,
                Action = pr.Action,
                Effect = pr.Effect
            })
            .ToListAsync();

        return Ok(assignments);
    }

    private async Task<bool> PolicyExists(Guid id)
    {
        return await _context.Policies.AnyAsync(e => e.Id == id);
    }
}

