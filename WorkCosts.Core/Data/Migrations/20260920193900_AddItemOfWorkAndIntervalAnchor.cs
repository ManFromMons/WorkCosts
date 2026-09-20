using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WorkCosts.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddItemOfWorkAndIntervalAnchor : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "IntervalAnchorDate",
                table: "GarageJobs",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ItemsOfWork",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    GarageJobId = table.Column<Guid>(type: "TEXT", nullable: false),
                    OccurredAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    OdometerMiles = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ItemsOfWork", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ItemsOfWork_GarageJobs_GarageJobId",
                        column: x => x.GarageJobId,
                        principalTable: "GarageJobs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ItemsOfWork_GarageJobId_OccurredAt",
                table: "ItemsOfWork",
                columns: new[] { "GarageJobId", "OccurredAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ItemsOfWork");

            migrationBuilder.DropColumn(
                name: "IntervalAnchorDate",
                table: "GarageJobs");
        }
    }
}
