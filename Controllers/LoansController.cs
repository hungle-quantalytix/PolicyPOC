using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PolicyPOC.Attributes;
using PolicyPOC.Contracts.Loans;
using PolicyPOC.Data;
using PolicyPOC.Models;
using PolicyPOC.Extensions;
using PolicyPOC.Services;
using System.Security.Claims;

namespace PolicyPOC.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class LoansController(
    ApplicationDbContext context,
    ILogger<LoansController> logger,
    ISecurityContextService securityContextService) : ControllerBase
{
    private readonly ApplicationDbContext _context = context;
    private readonly ILogger<LoansController> _logger = logger;
    private readonly ISecurityContextService _securityContextService = securityContextService;

    // GET: api/Loans
    [HttpGet]
    [Policy("Loan", "Read")]
    public async Task<ActionResult<IEnumerable<LoanResponse>>> GetLoans()
    {
        // Start with base query
        IQueryable<Loan> query = _context.Loans;

        // Apply row-level security rules from security context
        // This will filter the results based on policies like "resource.Department = user.Department"
        query = query.ApplyRowLevelSecurity(_securityContextService);

        // Check if we have field-level restrictions
        var hasFieldRestrictions = _securityContextService.HasFieldRestrictions();

        _logger.LogInformation("Fetching loans with {RLSRules} row-level security rules, field restrictions: {HasFieldRestrictions}",
            _securityContextService.SecurityContext.RowLevelSecurityRules.Count,
            hasFieldRestrictions);

        var loans = await query
            .Select(l => new LoanResponse
            {
                LoanId = l.LoanId,
                LoanNumber = l.LoanNumber,
                LoanStatus = l.LoanStatus,
                LoanStage = l.LoanStage,
                BorrowerId = l.BorrowerId,
                BorrowerName = l.BorrowerName,
                BorrowerEmail = l.BorrowerEmail,
                AssignedLoanOfficerId = l.AssignedLoanOfficerId,
                AssignedLoanOfficerName = l.AssignedLoanOfficerName,
                AssignedUnderwriterId = l.AssignedUnderwriterId,
                AssignedUnderwriterName = l.AssignedUnderwriterName,
                LenderId = l.LenderId,
                LenderName = l.LenderName,
                Department = l.Department,
                BranchId = l.BranchId,
                Region = l.Region,
                LoanType = l.LoanType,
                LoanPurpose = l.LoanPurpose,
                TotalLoanAmount = l.TotalLoanAmount,
                LTV = l.LTV,
                InterestRate = l.InterestRate,
                PropertyState = l.PropertyState,
                PropertyType = l.PropertyType,
                RiskRating = l.RiskRating,
                RequiresComplianceReview = l.RequiresComplianceReview,
                CreatedBy = l.CreatedBy,
                CreatedDate = l.CreatedDate,
                LastModifiedBy = l.LastModifiedBy,
                LastModifiedDate = l.LastModifiedDate
            })
            .ToListAsync();

        // Apply field-level security (masking, empty, hidden) based on security context
        if (hasFieldRestrictions)
        {
            loans = loans.Select(loan => loan.ApplyFieldSecurity(_securityContextService)).ToList();
        }

        return Ok(loans);
    }

    // GET: api/Loans/{id}
    [HttpGet("{id}")]
    [Policy("Loan", "Read")]
    public async Task<ActionResult<LoanResponse>> GetLoan(Guid id)
    {
        var loan = await _context.Loans.FindAsync(id);

        if (loan == null)
        {
            return NotFound(new { message = $"Loan with ID '{id}' not found." });
        }

        var response = new LoanResponse
        {
            LoanId = loan.LoanId,
            LoanNumber = loan.LoanNumber,
            LoanStatus = loan.LoanStatus,
            LoanStage = loan.LoanStage,
            BorrowerId = loan.BorrowerId,
            BorrowerName = loan.BorrowerName,
            BorrowerEmail = loan.BorrowerEmail,
            AssignedLoanOfficerId = loan.AssignedLoanOfficerId,
            AssignedLoanOfficerName = loan.AssignedLoanOfficerName,
            AssignedUnderwriterId = loan.AssignedUnderwriterId,
            AssignedUnderwriterName = loan.AssignedUnderwriterName,
            LenderId = loan.LenderId,
            LenderName = loan.LenderName,
            Department = loan.Department,
            BranchId = loan.BranchId,
            Region = loan.Region,
            LoanType = loan.LoanType,
            LoanPurpose = loan.LoanPurpose,
            TotalLoanAmount = loan.TotalLoanAmount,
            LTV = loan.LTV,
            InterestRate = loan.InterestRate,
            PropertyState = loan.PropertyState,
            PropertyType = loan.PropertyType,
            RiskRating = loan.RiskRating,
            RequiresComplianceReview = loan.RequiresComplianceReview,
            CreatedBy = loan.CreatedBy,
            CreatedDate = loan.CreatedDate,
            LastModifiedBy = loan.LastModifiedBy,
            LastModifiedDate = loan.LastModifiedDate
        };

        return Ok(response);
    }

    // POST: api/Loans
    [HttpPost]
    [Policy("Loan", "Write")]
    public async Task<ActionResult<LoanResponse>> CreateLoan([FromBody] CreateLoanRequest request)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "system";
        var userEmail = User.FindFirstValue(ClaimTypes.Email) ?? "system";

        var loan = new Loan
        {
            LoanId = Guid.NewGuid(),
            LoanNumber = request.LoanNumber,
            LoanStatus = request.LoanStatus,
            LoanStage = request.LoanStage,
            BorrowerId = request.BorrowerId,
            BorrowerName = request.BorrowerName,
            BorrowerEmail = request.BorrowerEmail,
            AssignedLoanOfficerId = request.AssignedLoanOfficerId,
            AssignedLoanOfficerName = request.AssignedLoanOfficerName,
            AssignedUnderwriterId = request.AssignedUnderwriterId,
            AssignedUnderwriterName = request.AssignedUnderwriterName,
            LenderId = request.LenderId,
            LenderName = request.LenderName,
            Department = request.Department,
            BranchId = request.BranchId,
            Region = request.Region,
            LoanType = request.LoanType,
            LoanPurpose = request.LoanPurpose,
            TotalLoanAmount = request.TotalLoanAmount,
            LTV = request.LTV,
            InterestRate = request.InterestRate,
            PropertyState = request.PropertyState,
            PropertyType = request.PropertyType,
            RiskRating = request.RiskRating,
            RequiresComplianceReview = request.RequiresComplianceReview,
            CreatedBy = userEmail,
            CreatedDate = DateTime.UtcNow
        };

        _context.Loans.Add(loan);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Loan {LoanNumber} created successfully by {User}", loan.LoanNumber, userEmail);

        var response = new LoanResponse
        {
            LoanId = loan.LoanId,
            LoanNumber = loan.LoanNumber,
            LoanStatus = loan.LoanStatus,
            LoanStage = loan.LoanStage,
            BorrowerId = loan.BorrowerId,
            BorrowerName = loan.BorrowerName,
            BorrowerEmail = loan.BorrowerEmail,
            AssignedLoanOfficerId = loan.AssignedLoanOfficerId,
            AssignedLoanOfficerName = loan.AssignedLoanOfficerName,
            AssignedUnderwriterId = loan.AssignedUnderwriterId,
            AssignedUnderwriterName = loan.AssignedUnderwriterName,
            LenderId = loan.LenderId,
            LenderName = loan.LenderName,
            Department = loan.Department,
            BranchId = loan.BranchId,
            Region = loan.Region,
            LoanType = loan.LoanType,
            LoanPurpose = loan.LoanPurpose,
            TotalLoanAmount = loan.TotalLoanAmount,
            LTV = loan.LTV,
            InterestRate = loan.InterestRate,
            PropertyState = loan.PropertyState,
            PropertyType = loan.PropertyType,
            RiskRating = loan.RiskRating,
            RequiresComplianceReview = loan.RequiresComplianceReview,
            CreatedBy = loan.CreatedBy,
            CreatedDate = loan.CreatedDate,
            LastModifiedBy = loan.LastModifiedBy,
            LastModifiedDate = loan.LastModifiedDate
        };

        return CreatedAtAction(nameof(GetLoan), new { id = loan.LoanId }, response);
    }

    // PUT: api/Loans/{id}
    [HttpPut("{id}")]
    [Policy("Loan", "Write")]
    public async Task<IActionResult> UpdateLoan(Guid id, [FromBody] UpdateLoanRequest request)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var loan = await _context.Loans.FindAsync(id);
        if (loan == null)
        {
            return NotFound(new { message = $"Loan with ID '{id}' not found." });
        }

        var userEmail = User.FindFirstValue(ClaimTypes.Email) ?? "system";

        // Update loan properties
        loan.LoanNumber = request.LoanNumber;
        loan.LoanStatus = request.LoanStatus;
        loan.LoanStage = request.LoanStage;
        loan.BorrowerId = request.BorrowerId;
        loan.BorrowerName = request.BorrowerName;
        loan.BorrowerEmail = request.BorrowerEmail;
        loan.AssignedLoanOfficerId = request.AssignedLoanOfficerId;
        loan.AssignedLoanOfficerName = request.AssignedLoanOfficerName;
        loan.AssignedUnderwriterId = request.AssignedUnderwriterId;
        loan.AssignedUnderwriterName = request.AssignedUnderwriterName;
        loan.LenderId = request.LenderId;
        loan.LenderName = request.LenderName;
        loan.Department = request.Department;
        loan.BranchId = request.BranchId;
        loan.Region = request.Region;
        loan.LoanType = request.LoanType;
        loan.LoanPurpose = request.LoanPurpose;
        loan.TotalLoanAmount = request.TotalLoanAmount;
        loan.LTV = request.LTV;
        loan.InterestRate = request.InterestRate;
        loan.PropertyState = request.PropertyState;
        loan.PropertyType = request.PropertyType;
        loan.RiskRating = request.RiskRating;
        loan.RequiresComplianceReview = request.RequiresComplianceReview;
        loan.LastModifiedBy = userEmail;
        loan.LastModifiedDate = DateTime.UtcNow;

        try
        {
            await _context.SaveChangesAsync();
            _logger.LogInformation("Loan {LoanNumber} updated successfully by {User}", loan.LoanNumber, userEmail);
        }
        catch (DbUpdateConcurrencyException)
        {
            if (!await LoanExists(id))
            {
                return NotFound(new { message = $"Loan with ID '{id}' not found." });
            }
            throw;
        }

        return NoContent();
    }

    // DELETE: api/Loans/{id}
    [HttpDelete("{id}")]
    [Policy("Loan", "Delete")]
    public async Task<IActionResult> DeleteLoan(Guid id)
    {
        var loan = await _context.Loans.FindAsync(id);
        if (loan == null)
        {
            return NotFound(new { message = $"Loan with ID '{id}' not found." });
        }

        var userEmail = User.FindFirstValue(ClaimTypes.Email) ?? "system";

        _context.Loans.Remove(loan);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Loan {LoanNumber} deleted successfully by {User}", loan.LoanNumber, userEmail);

        return NoContent();
    }

    private async Task<bool> LoanExists(Guid id)
    {
        return await _context.Loans.AnyAsync(e => e.LoanId == id);
    }
}

