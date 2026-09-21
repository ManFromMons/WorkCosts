using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WorkCosts.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCars : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CarId",
                table: "WorkJobs",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CarId",
                table: "ItemsOfWork",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CarId",
                table: "GarageJobs",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Cars",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Make = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    Model = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    ModelNumber = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    EngineType = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Vrm = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    VrmKey = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    Year = table.Column<int>(type: "INTEGER", nullable: false),
                    Vin = table.Column<string>(type: "TEXT", maxLength: 17, nullable: false),
                    ImageRelativePath = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    ImageContentType = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    VehicleOrderJson = table.Column<string>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Cars", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WorkJobs_CarId",
                table: "WorkJobs",
                column: "CarId");

            migrationBuilder.CreateIndex(
                name: "IX_ItemsOfWork_CarId",
                table: "ItemsOfWork",
                column: "CarId");

            migrationBuilder.CreateIndex(
                name: "IX_GarageJobs_CarId",
                table: "GarageJobs",
                column: "CarId");

            migrationBuilder.CreateIndex(
                name: "IX_Cars_VrmKey",
                table: "Cars",
                column: "VrmKey",
                unique: true,
                filter: "\"DeletedAt\" IS NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_GarageJobs_Cars_CarId",
                table: "GarageJobs",
                column: "CarId",
                principalTable: "Cars",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ItemsOfWork_Cars_CarId",
                table: "ItemsOfWork",
                column: "CarId",
                principalTable: "Cars",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_WorkJobs_Cars_CarId",
                table: "WorkJobs",
                column: "CarId",
                principalTable: "Cars",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_GarageJobs_Cars_CarId",
                table: "GarageJobs");

            migrationBuilder.DropForeignKey(
                name: "FK_ItemsOfWork_Cars_CarId",
                table: "ItemsOfWork");

            migrationBuilder.DropForeignKey(
                name: "FK_WorkJobs_Cars_CarId",
                table: "WorkJobs");

            migrationBuilder.DropTable(
                name: "Cars");

            migrationBuilder.DropIndex(
                name: "IX_WorkJobs_CarId",
                table: "WorkJobs");

            migrationBuilder.DropIndex(
                name: "IX_ItemsOfWork_CarId",
                table: "ItemsOfWork");

            migrationBuilder.DropIndex(
                name: "IX_GarageJobs_CarId",
                table: "GarageJobs");

            migrationBuilder.DropColumn(
                name: "CarId",
                table: "WorkJobs");

            migrationBuilder.DropColumn(
                name: "CarId",
                table: "ItemsOfWork");

            migrationBuilder.DropColumn(
                name: "CarId",
                table: "GarageJobs");
        }
    }
}
