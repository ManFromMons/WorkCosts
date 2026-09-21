using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WorkCosts.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCarDetails : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CarDetailsId",
                table: "ItemsOfWork",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CarDetailsId",
                table: "GarageJobs",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CarDetailsId",
                table: "Cars",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CarDetails",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Make = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    Model = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    ModelNumber = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    Year = table.Column<int>(type: "INTEGER", nullable: false),
                    EngineType = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    TypeKey = table.Column<string>(type: "TEXT", maxLength: 400, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CarDetails", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "JobCarDetails",
                columns: table => new
                {
                    JobId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CarDetailsId = table.Column<Guid>(type: "TEXT", nullable: false),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JobCarDetails", x => new { x.JobId, x.CarDetailsId });
                    table.ForeignKey(
                        name: "FK_JobCarDetails_CarDetails_CarDetailsId",
                        column: x => x.CarDetailsId,
                        principalTable: "CarDetails",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_JobCarDetails_Jobs_JobId",
                        column: x => x.JobId,
                        principalTable: "Jobs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ItemsOfWork_CarDetailsId",
                table: "ItemsOfWork",
                column: "CarDetailsId");

            migrationBuilder.CreateIndex(
                name: "IX_GarageJobs_CarDetailsId",
                table: "GarageJobs",
                column: "CarDetailsId");

            migrationBuilder.CreateIndex(
                name: "IX_Cars_CarDetailsId",
                table: "Cars",
                column: "CarDetailsId");

            migrationBuilder.CreateIndex(
                name: "IX_CarDetails_TypeKey",
                table: "CarDetails",
                column: "TypeKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_JobCarDetails_CarDetailsId",
                table: "JobCarDetails",
                column: "CarDetailsId");

            migrationBuilder.AddForeignKey(
                name: "FK_Cars_CarDetails_CarDetailsId",
                table: "Cars",
                column: "CarDetailsId",
                principalTable: "CarDetails",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_GarageJobs_CarDetails_CarDetailsId",
                table: "GarageJobs",
                column: "CarDetailsId",
                principalTable: "CarDetails",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ItemsOfWork_CarDetails_CarDetailsId",
                table: "ItemsOfWork",
                column: "CarDetailsId",
                principalTable: "CarDetails",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Cars_CarDetails_CarDetailsId",
                table: "Cars");

            migrationBuilder.DropForeignKey(
                name: "FK_GarageJobs_CarDetails_CarDetailsId",
                table: "GarageJobs");

            migrationBuilder.DropForeignKey(
                name: "FK_ItemsOfWork_CarDetails_CarDetailsId",
                table: "ItemsOfWork");

            migrationBuilder.DropTable(
                name: "JobCarDetails");

            migrationBuilder.DropTable(
                name: "CarDetails");

            migrationBuilder.DropIndex(
                name: "IX_ItemsOfWork_CarDetailsId",
                table: "ItemsOfWork");

            migrationBuilder.DropIndex(
                name: "IX_GarageJobs_CarDetailsId",
                table: "GarageJobs");

            migrationBuilder.DropIndex(
                name: "IX_Cars_CarDetailsId",
                table: "Cars");

            migrationBuilder.DropColumn(
                name: "CarDetailsId",
                table: "ItemsOfWork");

            migrationBuilder.DropColumn(
                name: "CarDetailsId",
                table: "GarageJobs");

            migrationBuilder.DropColumn(
                name: "CarDetailsId",
                table: "Cars");
        }
    }
}
