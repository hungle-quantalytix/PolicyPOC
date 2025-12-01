using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PolicyPOC.Migrations
{
    /// <inheritdoc />
    public partial class DirectPolicyReferences : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PolicyResources");

            migrationBuilder.AddColumn<string>(
                name: "FieldsJson",
                table: "Resources",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ResourceReadPolicies",
                columns: table => new
                {
                    ReadPoliciesId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ReadResourcesId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ResourceReadPolicies", x => new { x.ReadPoliciesId, x.ReadResourcesId });
                    table.ForeignKey(
                        name: "FK_ResourceReadPolicies_Policies_ReadPoliciesId",
                        column: x => x.ReadPoliciesId,
                        principalTable: "Policies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ResourceReadPolicies_Resources_ReadResourcesId",
                        column: x => x.ReadResourcesId,
                        principalTable: "Resources",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ResourceWritePolicies",
                columns: table => new
                {
                    WritePoliciesId = table.Column<Guid>(type: "TEXT", nullable: false),
                    WriteResourcesId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ResourceWritePolicies", x => new { x.WritePoliciesId, x.WriteResourcesId });
                    table.ForeignKey(
                        name: "FK_ResourceWritePolicies_Policies_WritePoliciesId",
                        column: x => x.WritePoliciesId,
                        principalTable: "Policies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ResourceWritePolicies_Resources_WriteResourcesId",
                        column: x => x.WriteResourcesId,
                        principalTable: "Resources",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ResourceReadPolicies_ReadResourcesId",
                table: "ResourceReadPolicies",
                column: "ReadResourcesId");

            migrationBuilder.CreateIndex(
                name: "IX_ResourceWritePolicies_WriteResourcesId",
                table: "ResourceWritePolicies",
                column: "WriteResourcesId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ResourceReadPolicies");

            migrationBuilder.DropTable(
                name: "ResourceWritePolicies");

            migrationBuilder.DropColumn(
                name: "FieldsJson",
                table: "Resources");

            migrationBuilder.CreateTable(
                name: "PolicyResources",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    PolicyId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Action = table.Column<string>(type: "TEXT", nullable: true),
                    Effect = table.Column<string>(type: "TEXT", nullable: true),
                    ResourceColumns = table.Column<string>(type: "TEXT", nullable: true),
                    ResourceName = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PolicyResources", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PolicyResources_Policies_PolicyId",
                        column: x => x.PolicyId,
                        principalTable: "Policies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PolicyResources_PolicyId",
                table: "PolicyResources",
                column: "PolicyId");
        }
    }
}
