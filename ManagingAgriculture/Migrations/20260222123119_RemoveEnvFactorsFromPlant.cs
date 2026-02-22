using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ManagingAgriculture.Migrations
{
    /// <inheritdoc />
    public partial class RemoveEnvFactorsFromPlant : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AvgTemperatureCelsius",
                table: "Plants");

            migrationBuilder.DropColumn(
                name: "SoilType",
                table: "Plants");

            migrationBuilder.DropColumn(
                name: "SunlightExposure",
                table: "Plants");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "AvgTemperatureCelsius",
                table: "Plants",
                type: "decimal(5,2)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SoilType",
                table: "Plants",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SunlightExposure",
                table: "Plants",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);
        }
    }
}
