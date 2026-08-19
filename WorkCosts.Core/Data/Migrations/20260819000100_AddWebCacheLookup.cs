using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WorkCosts.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddWebCacheLookup : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CachedWebPages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    PageUrl = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: false),
                    Domain = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    RelativePath = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    ByteSize = table.Column<long>(type: "INTEGER", nullable: false),
                    CachedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CachedWebPages", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CachedWebPages_PageUrl",
                table: "CachedWebPages",
                column: "PageUrl",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CachedWebPages_Domain",
                table: "CachedWebPages",
                column: "Domain");

            migrationBuilder.CreateTable(
                name: "CachedWebImages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    PageUrl = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: false),
                    ImageUrl = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: false),
                    Domain = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    RelativePath = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    ContentType = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    ByteSize = table.Column<long>(type: "INTEGER", nullable: false),
                    CachedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CachedWebImages", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CachedWebImages_PageUrl_ImageUrl",
                table: "CachedWebImages",
                columns: new[] { "PageUrl", "ImageUrl" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CachedWebImages_Domain",
                table: "CachedWebImages",
                column: "Domain");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "CachedWebImages");
            migrationBuilder.DropTable(name: "CachedWebPages");
        }
    }
}
