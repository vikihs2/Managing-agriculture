using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ManagingAgriculture.Migrations
{
    /// <inheritdoc />
    public partial class AddHarvestLeaveRequestMarketplacePurchase : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TaskAssignments_AspNetUsers_AssignedToUserId",
                table: "TaskAssignments");

            migrationBuilder.AddColumn<string>(
                name: "AssignedByUserId",
                table: "TaskAssignments",
                type: "nvarchar(450)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SellerCompanyId",
                table: "MarketplaceListings",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SellerUserId",
                table: "MarketplaceListings",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "HarvestRecords",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompanyId = table.Column<int>(type: "int", nullable: true),
                    OwnerUserId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PlantName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    PlantType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    FieldName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    FieldSizeDecars = table.Column<decimal>(type: "decimal(10,2)", nullable: true),
                    EstimatedYieldKg = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    HarvestedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    HarvestedByUserId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HarvestRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HarvestRecords_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "LeaveRequests",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    CompanyId = table.Column<int>(type: "int", nullable: true),
                    LeaveDate = table.Column<DateTime>(type: "date", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    RequestedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DecidedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    BossNote = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LeaveRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LeaveRequests_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_LeaveRequests_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "MarketplacePurchaseRequests",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ListingId = table.Column<int>(type: "int", nullable: false),
                    BuyerUserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    BuyerCompanyId = table.Column<int>(type: "int", nullable: true),
                    BuyerName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    RequestedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Message = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MarketplacePurchaseRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MarketplacePurchaseRequests_AspNetUsers_BuyerUserId",
                        column: x => x.BuyerUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MarketplacePurchaseRequests_MarketplaceListings_ListingId",
                        column: x => x.ListingId,
                        principalTable: "MarketplaceListings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TaskAssignments_AssignedByUserId",
                table: "TaskAssignments",
                column: "AssignedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_HarvestRecords_CompanyId",
                table: "HarvestRecords",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_LeaveRequests_CompanyId",
                table: "LeaveRequests",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_LeaveRequests_UserId",
                table: "LeaveRequests",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_MarketplacePurchaseRequests_BuyerUserId",
                table: "MarketplacePurchaseRequests",
                column: "BuyerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_MarketplacePurchaseRequests_ListingId",
                table: "MarketplacePurchaseRequests",
                column: "ListingId");

            migrationBuilder.AddForeignKey(
                name: "FK_TaskAssignments_AspNetUsers_AssignedByUserId",
                table: "TaskAssignments",
                column: "AssignedByUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_TaskAssignments_AspNetUsers_AssignedToUserId",
                table: "TaskAssignments",
                column: "AssignedToUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TaskAssignments_AspNetUsers_AssignedByUserId",
                table: "TaskAssignments");

            migrationBuilder.DropForeignKey(
                name: "FK_TaskAssignments_AspNetUsers_AssignedToUserId",
                table: "TaskAssignments");

            migrationBuilder.DropTable(
                name: "HarvestRecords");

            migrationBuilder.DropTable(
                name: "LeaveRequests");

            migrationBuilder.DropTable(
                name: "MarketplacePurchaseRequests");

            migrationBuilder.DropIndex(
                name: "IX_TaskAssignments_AssignedByUserId",
                table: "TaskAssignments");

            migrationBuilder.DropColumn(
                name: "AssignedByUserId",
                table: "TaskAssignments");

            migrationBuilder.DropColumn(
                name: "SellerCompanyId",
                table: "MarketplaceListings");

            migrationBuilder.DropColumn(
                name: "SellerUserId",
                table: "MarketplaceListings");

            migrationBuilder.AddForeignKey(
                name: "FK_TaskAssignments_AspNetUsers_AssignedToUserId",
                table: "TaskAssignments",
                column: "AssignedToUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
