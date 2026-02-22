using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ManagingAgriculture.Migrations
{
    /// <inheritdoc />
    public partial class AddFieldEntity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "FieldId",
                table: "Plants",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Fields",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompanyId = table.Column<int>(type: "int", nullable: true),
                    OwnerUserId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SizeInDecars = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    City = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SoilType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    SunlightExposure = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    AverageTemperatureCelsius = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    IsOccupied = table.Column<bool>(type: "bit", nullable: false),
                    CurrentPlantId = table.Column<int>(type: "int", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedDate = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Fields", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Fields_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Fields_Plants_CurrentPlantId",
                        column: x => x.CurrentPlantId,
                        principalTable: "Plants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Plants_FieldId",
                table: "Plants",
                column: "FieldId");

            migrationBuilder.CreateIndex(
                name: "IX_Fields_CompanyId",
                table: "Fields",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_Fields_CurrentPlantId",
                table: "Fields",
                column: "CurrentPlantId",
                unique: true,
                filter: "[CurrentPlantId] IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_Plants_Fields_FieldId",
                table: "Plants",
                column: "FieldId",
                principalTable: "Fields",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Plants_Fields_FieldId",
                table: "Plants");

            migrationBuilder.DropTable(
                name: "Fields");

            migrationBuilder.DropIndex(
                name: "IX_Plants_FieldId",
                table: "Plants");

            migrationBuilder.DropColumn(
                name: "FieldId",
                table: "Plants");
        }
    }
}
