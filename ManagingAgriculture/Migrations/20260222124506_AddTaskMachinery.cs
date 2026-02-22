using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ManagingAgriculture.Migrations
{
    /// <inheritdoc />
    public partial class AddTaskMachinery : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AssignedMachineryId",
                table: "TaskAssignments",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_TaskAssignments_AssignedMachineryId",
                table: "TaskAssignments",
                column: "AssignedMachineryId");

            migrationBuilder.AddForeignKey(
                name: "FK_TaskAssignments_Machinery_AssignedMachineryId",
                table: "TaskAssignments",
                column: "AssignedMachineryId",
                principalTable: "Machinery",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TaskAssignments_Machinery_AssignedMachineryId",
                table: "TaskAssignments");

            migrationBuilder.DropIndex(
                name: "IX_TaskAssignments_AssignedMachineryId",
                table: "TaskAssignments");

            migrationBuilder.DropColumn(
                name: "AssignedMachineryId",
                table: "TaskAssignments");
        }
    }
}
