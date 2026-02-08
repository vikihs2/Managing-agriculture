using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ManagingAgriculture.Migrations
{
    /// <inheritdoc />
    public partial class AddCurrentTrackingDate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "CurrentTrackingDate",
                table: "Plants",
                type: "date",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CurrentTrackingDate",
                table: "Plants");
        }
    }
}
