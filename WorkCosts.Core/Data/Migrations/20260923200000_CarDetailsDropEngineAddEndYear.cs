using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WorkCosts.Data.Migrations
{
    /// <inheritdoc />
    public partial class CarDetailsDropEngineAddEndYear : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CarDetails_TypeKey",
                table: "CarDetails");

            migrationBuilder.AddColumn<int>(
                name: "EndYear",
                table: "CarDetails",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE CarDetails
                SET TypeKey = UPPER(TRIM(Make)) || '|' || UPPER(TRIM(Model)) || '|' || UPPER(TRIM(ModelNumber)) || '|' || CAST(Year AS TEXT);
                """);

            migrationBuilder.Sql(
                """
                DELETE FROM CarDetails
                WHERE Id IN (
                    SELECT c.Id
                    FROM CarDetails c
                    INNER JOIN CarDetails keep
                        ON keep.TypeKey = c.TypeKey AND keep.Id < c.Id
                    WHERE NOT EXISTS (SELECT 1 FROM Cars WHERE CarDetailsId = c.Id)
                      AND NOT EXISTS (SELECT 1 FROM JobCarDetails WHERE CarDetailsId = c.Id)
                      AND NOT EXISTS (SELECT 1 FROM GarageJobs WHERE CarDetailsId = c.Id)
                      AND NOT EXISTS (SELECT 1 FROM ItemsOfWork WHERE CarDetailsId = c.Id)
                );
                """);

            migrationBuilder.DropColumn(
                name: "EngineType",
                table: "CarDetails");

            migrationBuilder.CreateIndex(
                name: "IX_CarDetails_TypeKey",
                table: "CarDetails",
                column: "TypeKey",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CarDetails_TypeKey",
                table: "CarDetails");

            migrationBuilder.AddColumn<string>(
                name: "EngineType",
                table: "CarDetails",
                type: "TEXT",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.DropColumn(
                name: "EndYear",
                table: "CarDetails");

            migrationBuilder.Sql(
                """
                UPDATE CarDetails
                SET TypeKey = UPPER(TRIM(Make)) || '|' || UPPER(TRIM(ModelNumber)) || '|' || CAST(Year AS TEXT) || '|' || UPPER(TRIM(EngineType));
                """);

            migrationBuilder.CreateIndex(
                name: "IX_CarDetails_TypeKey",
                table: "CarDetails",
                column: "TypeKey",
                unique: true);
        }
    }
}
