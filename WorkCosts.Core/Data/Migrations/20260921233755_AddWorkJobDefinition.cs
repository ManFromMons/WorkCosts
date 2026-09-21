using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WorkCosts.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkJobDefinition : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsDefinition",
                table: "WorkJobs",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "SortOrder",
                table: "WorkJobs",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsDefinition",
                table: "WorkJobs");

            migrationBuilder.DropColumn(
                name: "SortOrder",
                table: "WorkJobs");
        }
    }
}
