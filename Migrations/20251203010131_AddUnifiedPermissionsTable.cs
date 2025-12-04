using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PolicyPOC.Migrations
{
    /// <inheritdoc />
    public partial class AddUnifiedPermissionsTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Permissions_ExpiresAt",
                table: "Permissions");

            migrationBuilder.DropIndex(
                name: "IX_Permissions_ResourceType_Action",
                table: "Permissions");

            migrationBuilder.DropIndex(
                name: "IX_Permissions_ResourceType_ResourceId_Action_SubjectType_SubjectId",
                table: "Permissions");

            migrationBuilder.DropColumn(
                name: "Conditions",
                table: "Permissions");

            migrationBuilder.RenameColumn(
                name: "CreatedBy",
                table: "Permissions",
                newName: "FieldName");

            migrationBuilder.CreateIndex(
                name: "IX_Permissions_ResourceType_FieldName_Action",
                table: "Permissions",
                columns: new[] { "ResourceType", "FieldName", "Action" },
                filter: "\"FieldName\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Permissions_ResourceType_ResourceId_Action",
                table: "Permissions",
                columns: new[] { "ResourceType", "ResourceId", "Action" });

            migrationBuilder.CreateIndex(
                name: "IX_Permissions_ResourceType_ResourceId_FieldName_Action_SubjectType_SubjectId",
                table: "Permissions",
                columns: new[] { "ResourceType", "ResourceId", "FieldName", "Action", "SubjectType", "SubjectId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Permissions_ResourceType_FieldName_Action",
                table: "Permissions");

            migrationBuilder.DropIndex(
                name: "IX_Permissions_ResourceType_ResourceId_Action",
                table: "Permissions");

            migrationBuilder.DropIndex(
                name: "IX_Permissions_ResourceType_ResourceId_FieldName_Action_SubjectType_SubjectId",
                table: "Permissions");

            migrationBuilder.RenameColumn(
                name: "FieldName",
                table: "Permissions",
                newName: "CreatedBy");

            migrationBuilder.AddColumn<string>(
                name: "Conditions",
                table: "Permissions",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Permissions_ExpiresAt",
                table: "Permissions",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_Permissions_ResourceType_Action",
                table: "Permissions",
                columns: new[] { "ResourceType", "Action" });

            migrationBuilder.CreateIndex(
                name: "IX_Permissions_ResourceType_ResourceId_Action_SubjectType_SubjectId",
                table: "Permissions",
                columns: new[] { "ResourceType", "ResourceId", "Action", "SubjectType", "SubjectId" },
                unique: true);
        }
    }
}
