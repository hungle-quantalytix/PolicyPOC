namespace PolicyPOC.Models;
public class Loan
{
    // Primary Key
    public Guid LoanId { get; set; }
    
    // Loan Identity
    public required string LoanNumber { get; set; }
    
    // Status & Workflow (CRITICAL FOR AUTHORIZATION)
    public required string LoanStatus { get; set; }
    public string? LoanStage { get; set; }
    
    // Borrower (Denormalized)
    public required string BorrowerId { get; set; }
    public string? BorrowerName { get; set; }
    public string? BorrowerEmail { get; set; }
    
    // Assignment (Denormalized)
    public string? AssignedLoanOfficerId { get; set; }
    public string? AssignedLoanOfficerName { get; set; }
    public string? AssignedUnderwriterId { get; set; }
    public string? AssignedUnderwriterName { get; set; }
    
    // Organization Context (Denormalized)
    public required string LenderId { get; set; }
    public string? LenderName { get; set; }
    public string? Department { get; set; }
    public string? BranchId { get; set; }
    public string? Region { get; set; }
    
    // Loan Details
    public string? LoanType { get; set; }
    public string? LoanPurpose { get; set; }
    public decimal? TotalLoanAmount { get; set; }
    public decimal? LTV { get; set; }
    public decimal? InterestRate { get; set; }
    
    // Property Info
    public string? PropertyState { get; set; }
    public string? PropertyType { get; set; }
    
    // Authorization Helpers
    public string? RiskRating { get; set; } // Low, Medium, High
    public bool RequiresComplianceReview { get; set; }
    
    // Audit Fields
    public required string CreatedBy { get; set; }
    public DateTime CreatedDate { get; set; }
    public string? LastModifiedBy { get; set; }
    public DateTime? LastModifiedDate { get; set; }
}

public static class LoanStatuses
{
    public const string Draft = "Draft";
    public const string Submitted = "Submitted";
    public const string InitialReview = "Initial Review";
    public const string Underwriting = "Underwriting";
    public const string Approved = "Approved";
    public const string Closing = "Closing";
    public const string Funded = "Funded";
    public const string Closed = "Closed";
    public const string Denied = "Denied";
}

public static class RiskRatings
{
    public const string Low = "Low";
    public const string Medium = "Medium";
    public const string High = "High";
}

public static class LoanTypes
{
    public const string Conventional = "Conventional";
    public const string FHA = "FHA";
    public const string VA = "VA";
    public const string USDA = "USDA";
    public const string Jumbo = "Jumbo";
}