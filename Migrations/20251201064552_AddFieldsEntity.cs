using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PolicyPOC.Migrations
{
    /// <inheritdoc />
    public partial class AddFieldsEntity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FieldsJson",
                table: "Resources");

            migrationBuilder.CreateTable(
                name: "Fields",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    FieldName = table.Column<string>(type: "TEXT", nullable: false),
                    IsPublic = table.Column<bool>(type: "INTEGER", nullable: false),
                    ResourceId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Fields", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Fields_Resources_ResourceId",
                        column: x => x.ResourceId,
                        principalTable: "Resources",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "FieldReadPolicies",
                columns: table => new
                {
                    ReadFieldsId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ReadPoliciesId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FieldReadPolicies", x => new { x.ReadFieldsId, x.ReadPoliciesId });
                    table.ForeignKey(
                        name: "FK_FieldReadPolicies_Fields_ReadFieldsId",
                        column: x => x.ReadFieldsId,
                        principalTable: "Fields",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_FieldReadPolicies_Policies_ReadPoliciesId",
                        column: x => x.ReadPoliciesId,
                        principalTable: "Policies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "FieldWritePolicies",
                columns: table => new
                {
                    WriteFieldsId = table.Column<Guid>(type: "TEXT", nullable: false),
                    WritePoliciesId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FieldWritePolicies", x => new { x.WriteFieldsId, x.WritePoliciesId });
                    table.ForeignKey(
                        name: "FK_FieldWritePolicies_Fields_WriteFieldsId",
                        column: x => x.WriteFieldsId,
                        principalTable: "Fields",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_FieldWritePolicies_Policies_WritePoliciesId",
                        column: x => x.WritePoliciesId,
                        principalTable: "Policies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FieldReadPolicies_ReadPoliciesId",
                table: "FieldReadPolicies",
                column: "ReadPoliciesId");

            migrationBuilder.CreateIndex(
                name: "IX_Fields_ResourceId",
                table: "Fields",
                column: "ResourceId");

            migrationBuilder.CreateIndex(
                name: "IX_FieldWritePolicies_WritePoliciesId",
                table: "FieldWritePolicies",
                column: "WritePoliciesId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FieldReadPolicies");

            migrationBuilder.DropTable(
                name: "FieldWritePolicies");

            migrationBuilder.DropTable(
                name: "Fields");

            migrationBuilder.AddColumn<string>(
                name: "FieldsJson",
                table: "Resources",
                type: "TEXT",
                nullable: true);
        }
    }
}
