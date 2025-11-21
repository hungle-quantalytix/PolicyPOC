using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PolicyPOC.Migrations
{
    /// <inheritdoc />
    public partial class AddLoansTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Loans",
                columns: table => new
                {
                    LoanId = table.Column<Guid>(type: "TEXT", nullable: false),
                    LoanNumber = table.Column<string>(type: "TEXT", nullable: false),
                    LoanStatus = table.Column<string>(type: "TEXT", nullable: false),
                    LoanStage = table.Column<string>(type: "TEXT", nullable: true),
                    BorrowerId = table.Column<string>(type: "TEXT", nullable: false),
                    BorrowerName = table.Column<string>(type: "TEXT", nullable: true),
                    BorrowerEmail = table.Column<string>(type: "TEXT", nullable: true),
                    AssignedLoanOfficerId = table.Column<string>(type: "TEXT", nullable: true),
                    AssignedLoanOfficerName = table.Column<string>(type: "TEXT", nullable: true),
                    AssignedUnderwriterId = table.Column<string>(type: "TEXT", nullable: true),
                    AssignedUnderwriterName = table.Column<string>(type: "TEXT", nullable: true),
                    LenderId = table.Column<string>(type: "TEXT", nullable: false),
                    LenderName = table.Column<string>(type: "TEXT", nullable: true),
                    Department = table.Column<string>(type: "TEXT", nullable: true),
                    BranchId = table.Column<string>(type: "TEXT", nullable: true),
                    Region = table.Column<string>(type: "TEXT", nullable: true),
                    LoanType = table.Column<string>(type: "TEXT", nullable: true),
                    LoanPurpose = table.Column<string>(type: "TEXT", nullable: true),
                    TotalLoanAmount = table.Column<decimal>(type: "TEXT", nullable: true),
                    LTV = table.Column<decimal>(type: "TEXT", nullable: true),
                    InterestRate = table.Column<decimal>(type: "TEXT", nullable: true),
                    PropertyState = table.Column<string>(type: "TEXT", nullable: true),
                    PropertyType = table.Column<string>(type: "TEXT", nullable: true),
                    RiskRating = table.Column<string>(type: "TEXT", nullable: true),
                    RequiresComplianceReview = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedBy = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "TEXT", nullable: true),
                    LastModifiedDate = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Loans", x => x.LoanId);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Loans");
        }
    }
}
