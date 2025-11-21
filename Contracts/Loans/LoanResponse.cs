namespace PolicyPOC.Contracts.Loans;

public class LoanResponse
{
    public Guid LoanId { get; set; }
    public required string LoanNumber { get; set; }
    public required string LoanStatus { get; set; }
    public string? LoanStage { get; set; }
    
    public required string BorrowerId { get; set; }
    public string? BorrowerName { get; set; }
    public string? BorrowerEmail { get; set; }
    
    public string? AssignedLoanOfficerId { get; set; }
    public string? AssignedLoanOfficerName { get; set; }
    public string? AssignedUnderwriterId { get; set; }
    public string? AssignedUnderwriterName { get; set; }
    
    public required string LenderId { get; set; }
    public string? LenderName { get; set; }
    public string? Department { get; set; }
    public string? BranchId { get; set; }
    public string? Region { get; set; }
    
    public string? LoanType { get; set; }
    public string? LoanPurpose { get; set; }
    public decimal? TotalLoanAmount { get; set; }
    public decimal? LTV { get; set; }
    public decimal? InterestRate { get; set; }
    
    public string? PropertyState { get; set; }
    public string? PropertyType { get; set; }
    
    public string? RiskRating { get; set; }
    public bool RequiresComplianceReview { get; set; }
    
    public required string CreatedBy { get; set; }
    public DateTime CreatedDate { get; set; }
    public string? LastModifiedBy { get; set; }
    public DateTime? LastModifiedDate { get; set; }
}

