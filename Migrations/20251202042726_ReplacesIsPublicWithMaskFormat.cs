using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PolicyPOC.Migrations
{
    /// <inheritdoc />
    public partial class ReplacesIsPublicWithMaskFormat : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsPublic",
                table: "Fields");

            migrationBuilder.AddColumn<string>(
                name: "MaskFormat",
                table: "Fields",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MaskFormat",
                table: "Fields");

            migrationBuilder.AddColumn<bool>(
                name: "IsPublic",
                table: "Fields",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
        }
    }
}
