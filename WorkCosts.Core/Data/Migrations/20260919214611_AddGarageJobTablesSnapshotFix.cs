using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WorkCosts.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddGarageJobTablesSnapshotFix : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "GarageJobs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    TargetKind = table.Column<int>(type: "INTEGER", nullable: false),
                    TargetLabel = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 8000, nullable: false),
                    DurationMinutes = table.Column<int>(type: "INTEGER", nullable: false),
                    IconRelativePath = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    IconContentType = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    RepeatCombine = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GarageJobs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "GarageJobReferencedJobs",
                columns: table => new
                {
                    GarageJobId = table.Column<Guid>(type: "TEXT", nullable: false),
                    JobId = table.Column<Guid>(type: "TEXT", nullable: false),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GarageJobReferencedJobs", x => new { x.GarageJobId, x.JobId });
                    table.ForeignKey(
                        name: "FK_GarageJobReferencedJobs_GarageJobs_GarageJobId",
                        column: x => x.GarageJobId,
                        principalTable: "GarageJobs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_GarageJobReferencedJobs_Jobs_JobId",
                        column: x => x.JobId,
                        principalTable: "Jobs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "GarageJobRepeatConditions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    GarageJobId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Kind = table.Column<int>(type: "INTEGER", nullable: false),
                    Amount = table.Column<int>(type: "INTEGER", nullable: false),
                    Unit = table.Column<int>(type: "INTEGER", nullable: false),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GarageJobRepeatConditions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GarageJobRepeatConditions_GarageJobs_GarageJobId",
                        column: x => x.GarageJobId,
                        principalTable: "GarageJobs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "GarageJobRequiredProducts",
                columns: table => new
                {
                    GarageJobId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProductId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Quantity = table.Column<short>(type: "INTEGER", nullable: false),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GarageJobRequiredProducts", x => new { x.GarageJobId, x.ProductId });
                    table.ForeignKey(
                        name: "FK_GarageJobRequiredProducts_GarageJobs_GarageJobId",
                        column: x => x.GarageJobId,
                        principalTable: "GarageJobs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_GarageJobRequiredProducts_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GarageJobReferencedJobs_JobId",
                table: "GarageJobReferencedJobs",
                column: "JobId");

            migrationBuilder.CreateIndex(
                name: "IX_GarageJobRepeatConditions_GarageJobId",
                table: "GarageJobRepeatConditions",
                column: "GarageJobId");

            migrationBuilder.CreateIndex(
                name: "IX_GarageJobRequiredProducts_ProductId",
                table: "GarageJobRequiredProducts",
                column: "ProductId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GarageJobReferencedJobs");

            migrationBuilder.DropTable(
                name: "GarageJobRepeatConditions");

            migrationBuilder.DropTable(
                name: "GarageJobRequiredProducts");

            migrationBuilder.DropTable(
                name: "GarageJobs");
        }
    }
}
