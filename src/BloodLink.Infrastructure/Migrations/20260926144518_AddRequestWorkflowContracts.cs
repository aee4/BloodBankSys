using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BloodLink.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddRequestWorkflowContracts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF EXISTS (
                    SELECT BloodNeedId
                    FROM BloodRequests
                    WHERE Status IN (0, 1)
                    GROUP BY BloodNeedId
                    HAVING COUNT(*) > 1)
                BEGIN
                    THROW 51001, 'Cannot enforce one active blood request per need: duplicate active requests exist.', 1;
                END;
                """);

            migrationBuilder.DropIndex(
                name: "IX_BloodRequests_BloodNeedId",
                table: "BloodRequests");

            migrationBuilder.AddColumn<int>(
                name: "ReservedBefore",
                table: "InventoryTransactions",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TotalBefore",
                table: "InventoryTransactions",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.Sql("""
                UPDATE InventoryTransactions
                SET TotalBefore = TotalAfter - TotalUnitsChange,
                    ReservedBefore = ReservedAfter - ReservedUnitsChange;
                """);

            migrationBuilder.Sql("""
                IF EXISTS (
                    SELECT 1 FROM InventoryTransactions
                    WHERE TotalBefore < 0 OR ReservedBefore < 0 OR ReservedBefore > TotalBefore
                       OR TotalAfter < 0 OR ReservedAfter < 0 OR ReservedAfter > TotalAfter
                       OR TotalAfter - TotalBefore <> TotalUnitsChange
                       OR ReservedAfter - ReservedBefore <> ReservedUnitsChange)
                BEGIN
                    THROW 51002, 'Cannot add inventory transaction before-value constraints: existing transaction evidence violates the inventory invariant.', 1;
                END;
                """);

            migrationBuilder.CreateTable(
                name: "BloodNeedStatusHistory",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BloodNeedId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FromStatus = table.Column<int>(type: "int", nullable: true),
                    ToStatus = table.Column<int>(type: "int", nullable: false),
                    Note = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    ChangedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    ChangedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BloodNeedStatusHistory", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BloodNeedStatusHistory_AspNetUsers_ChangedByUserId",
                        column: x => x.ChangedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_BloodNeedStatusHistory_BloodNeeds_BloodNeedId",
                        column: x => x.BloodNeedId,
                        principalTable: "BloodNeeds",
                        principalColumn: "Id");
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_InventoryTransactions_ReservedBefore_NonNegative",
                table: "InventoryTransactions",
                sql: "[ReservedBefore] >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_InventoryTransactions_ReservedBeforeWithinTotal",
                table: "InventoryTransactions",
                sql: "[ReservedBefore] <= [TotalBefore]");

            migrationBuilder.AddCheckConstraint(
                name: "CK_InventoryTransactions_ReservedDeltaMatches",
                table: "InventoryTransactions",
                sql: "[ReservedAfter] - [ReservedBefore] = [ReservedUnitsChange]");

            migrationBuilder.AddCheckConstraint(
                name: "CK_InventoryTransactions_TotalBefore_NonNegative",
                table: "InventoryTransactions",
                sql: "[TotalBefore] >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_InventoryTransactions_TotalDeltaMatches",
                table: "InventoryTransactions",
                sql: "[TotalAfter] - [TotalBefore] = [TotalUnitsChange]");

            migrationBuilder.CreateIndex(
                name: "IX_BloodRequests_BloodNeedId",
                table: "BloodRequests",
                column: "BloodNeedId",
                unique: true,
                filter: "[Status] IN (0, 1)");

            migrationBuilder.CreateIndex(
                name: "IX_BloodNeedStatusHistory_BloodNeedId_ChangedAtUtc_Id",
                table: "BloodNeedStatusHistory",
                columns: new[] { "BloodNeedId", "ChangedAtUtc", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_BloodNeedStatusHistory_ChangedByUserId",
                table: "BloodNeedStatusHistory",
                column: "ChangedByUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BloodNeedStatusHistory");

            migrationBuilder.DropCheckConstraint(
                name: "CK_InventoryTransactions_ReservedBefore_NonNegative",
                table: "InventoryTransactions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_InventoryTransactions_ReservedBeforeWithinTotal",
                table: "InventoryTransactions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_InventoryTransactions_ReservedDeltaMatches",
                table: "InventoryTransactions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_InventoryTransactions_TotalBefore_NonNegative",
                table: "InventoryTransactions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_InventoryTransactions_TotalDeltaMatches",
                table: "InventoryTransactions");

            migrationBuilder.DropIndex(
                name: "IX_BloodRequests_BloodNeedId",
                table: "BloodRequests");

            migrationBuilder.DropColumn(
                name: "ReservedBefore",
                table: "InventoryTransactions");

            migrationBuilder.DropColumn(
                name: "TotalBefore",
                table: "InventoryTransactions");

            migrationBuilder.CreateIndex(
                name: "IX_BloodRequests_BloodNeedId",
                table: "BloodRequests",
                column: "BloodNeedId");
        }
    }
}
