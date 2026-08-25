using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BloodLink.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CanonicalDatabaseModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Title",
                table: "Notifications",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "RelatedEntityType",
                table: "Notifications",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "RecipientUserId",
                table: "Notifications",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "Message",
                table: "Notifications",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "ReferenceType",
                table: "InventoryTransactions",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Reason",
                table: "InventoryTransactions",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "PerformedByUserId",
                table: "InventoryTransactions",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "UserId",
                table: "FacilityStaff",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "StatusReason",
                table: "FacilityStaff",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "CreatedByAdminId",
                table: "FacilityStaff",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "RejectionReason",
                table: "Facilities",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "RegistrationNumber",
                table: "Facilities",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)");

            migrationBuilder.AlterColumn<string>(
                name: "Region",
                table: "Facilities",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "Facilities",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)");

            migrationBuilder.AlterColumn<string>(
                name: "CreatedByUserId",
                table: "Facilities",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "ContactPhone",
                table: "Facilities",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "ContactEmail",
                table: "Facilities",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "City",
                table: "Facilities",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "ApprovedByUserId",
                table: "Facilities",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Address",
                table: "Facilities",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "Note",
                table: "BloodRequestStatusHistory",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "ChangedByUserId",
                table: "BloodRequestStatusHistory",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "ResponseNote",
                table: "BloodRequests",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "RespondedByAdminId",
                table: "BloodRequests",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "RequestedByAdminId",
                table: "BloodRequests",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "RequestNote",
                table: "BloodRequests",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "FulfilledByAdminId",
                table: "BloodRequests",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "RequestedByUserId",
                table: "BloodNeeds",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "Note",
                table: "BloodNeeds",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "DecisionReason",
                table: "BloodNeeds",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Summary",
                table: "AuditLogs",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "IpAddress",
                table: "AuditLogs",
                type: "nvarchar(45)",
                maxLength: 45,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "EntityType",
                table: "AuditLogs",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "ActorUserId",
                table: "AuditLogs",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Action",
                table: "AuditLogs",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_RecipientUserId",
                table: "Notifications",
                column: "RecipientUserId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryTransactions_BloodInventoryId",
                table: "InventoryTransactions",
                column: "BloodInventoryId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_InventoryTransactions_ReservedAfter",
                table: "InventoryTransactions",
                sql: "[ReservedAfter] >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_InventoryTransactions_TotalAfter",
                table: "InventoryTransactions",
                sql: "[TotalAfter] >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_InventoryTransactions_TotalUnitsChange",
                table: "InventoryTransactions",
                sql: "[TotalUnitsChange] <> 0 OR [ReservedUnitsChange] <> 0");

            migrationBuilder.CreateIndex(
                name: "IX_FacilityStaff_FacilityId_UserId",
                table: "FacilityStaff",
                columns: new[] { "FacilityId", "UserId" },
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Facilities_Name_NotEmpty",
                table: "Facilities",
                sql: "[Name] <> N''");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Facilities_RegistrationNumber_NotEmpty",
                table: "Facilities",
                sql: "[RegistrationNumber] <> N''");

            migrationBuilder.CreateIndex(
                name: "IX_BloodRequestStatusHistory_BloodRequestId",
                table: "BloodRequestStatusHistory",
                column: "BloodRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_BloodRequestStatusHistory_ChangedByUserId",
                table: "BloodRequestStatusHistory",
                column: "ChangedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_BloodRequests_BloodNeedId",
                table: "BloodRequests",
                column: "BloodNeedId");

            migrationBuilder.CreateIndex(
                name: "IX_BloodRequests_FulfilledByAdminId",
                table: "BloodRequests",
                column: "FulfilledByAdminId");

            migrationBuilder.CreateIndex(
                name: "IX_BloodRequests_RequestedByAdminId",
                table: "BloodRequests",
                column: "RequestedByAdminId");

            migrationBuilder.CreateIndex(
                name: "IX_BloodRequests_RequestingFacilityId",
                table: "BloodRequests",
                column: "RequestingFacilityId");

            migrationBuilder.CreateIndex(
                name: "IX_BloodRequests_RespondedByAdminId",
                table: "BloodRequests",
                column: "RespondedByAdminId");

            migrationBuilder.CreateIndex(
                name: "IX_BloodRequests_SourceFacilityId",
                table: "BloodRequests",
                column: "SourceFacilityId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_BloodRequests_UnitsAccepted",
                table: "BloodRequests",
                sql: "[UnitsAccepted] IS NULL OR [UnitsAccepted] >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_BloodRequests_UnitsRequested",
                table: "BloodRequests",
                sql: "[UnitsRequested] > 0");

            migrationBuilder.CreateIndex(
                name: "IX_BloodNeeds_FacilityId",
                table: "BloodNeeds",
                column: "FacilityId");

            migrationBuilder.CreateIndex(
                name: "IX_BloodNeeds_RequestedByUserId",
                table: "BloodNeeds",
                column: "RequestedByUserId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_BloodNeeds_NeededByUtc",
                table: "BloodNeeds",
                sql: "[NeededByUtc] > GETDATE()");

            migrationBuilder.AddCheckConstraint(
                name: "CK_BloodNeeds_UnitsNeeded",
                table: "BloodNeeds",
                sql: "[UnitsNeeded] > 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_BloodInventory_LowStockThreshold",
                table: "BloodInventory",
                sql: "[LowStockThreshold] >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_BloodInventory_ReservedUnits",
                table: "BloodInventory",
                sql: "[ReservedUnits] >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_BloodInventory_TotalUnits",
                table: "BloodInventory",
                sql: "[TotalUnits] >= 0");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_ActorUserId",
                table: "AuditLogs",
                column: "ActorUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_AuditLogs_AspNetUsers_ActorUserId",
                table: "AuditLogs",
                column: "ActorUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_BloodInventory_Facilities_FacilityId",
                table: "BloodInventory",
                column: "FacilityId",
                principalTable: "Facilities",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_BloodNeeds_AspNetUsers_RequestedByUserId",
                table: "BloodNeeds",
                column: "RequestedByUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_BloodNeeds_Facilities_FacilityId",
                table: "BloodNeeds",
                column: "FacilityId",
                principalTable: "Facilities",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_BloodRequests_AspNetUsers_FulfilledByAdminId",
                table: "BloodRequests",
                column: "FulfilledByAdminId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_BloodRequests_AspNetUsers_RequestedByAdminId",
                table: "BloodRequests",
                column: "RequestedByAdminId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_BloodRequests_AspNetUsers_RespondedByAdminId",
                table: "BloodRequests",
                column: "RespondedByAdminId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_BloodRequests_BloodNeeds_BloodNeedId",
                table: "BloodRequests",
                column: "BloodNeedId",
                principalTable: "BloodNeeds",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_BloodRequests_Facilities_RequestingFacilityId",
                table: "BloodRequests",
                column: "RequestingFacilityId",
                principalTable: "Facilities",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_BloodRequests_Facilities_SourceFacilityId",
                table: "BloodRequests",
                column: "SourceFacilityId",
                principalTable: "Facilities",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_BloodRequestStatusHistory_AspNetUsers_ChangedByUserId",
                table: "BloodRequestStatusHistory",
                column: "ChangedByUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_BloodRequestStatusHistory_BloodRequests_BloodRequestId",
                table: "BloodRequestStatusHistory",
                column: "BloodRequestId",
                principalTable: "BloodRequests",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_FacilityStaff_Facilities_FacilityId",
                table: "FacilityStaff",
                column: "FacilityId",
                principalTable: "Facilities",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_InventoryTransactions_BloodInventory_BloodInventoryId",
                table: "InventoryTransactions",
                column: "BloodInventoryId",
                principalTable: "BloodInventory",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Notifications_AspNetUsers_RecipientUserId",
                table: "Notifications",
                column: "RecipientUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AuditLogs_AspNetUsers_ActorUserId",
                table: "AuditLogs");

            migrationBuilder.DropForeignKey(
                name: "FK_BloodInventory_Facilities_FacilityId",
                table: "BloodInventory");

            migrationBuilder.DropForeignKey(
                name: "FK_BloodNeeds_AspNetUsers_RequestedByUserId",
                table: "BloodNeeds");

            migrationBuilder.DropForeignKey(
                name: "FK_BloodNeeds_Facilities_FacilityId",
                table: "BloodNeeds");

            migrationBuilder.DropForeignKey(
                name: "FK_BloodRequests_AspNetUsers_FulfilledByAdminId",
                table: "BloodRequests");

            migrationBuilder.DropForeignKey(
                name: "FK_BloodRequests_AspNetUsers_RequestedByAdminId",
                table: "BloodRequests");

            migrationBuilder.DropForeignKey(
                name: "FK_BloodRequests_AspNetUsers_RespondedByAdminId",
                table: "BloodRequests");

            migrationBuilder.DropForeignKey(
                name: "FK_BloodRequests_BloodNeeds_BloodNeedId",
                table: "BloodRequests");

            migrationBuilder.DropForeignKey(
                name: "FK_BloodRequests_Facilities_RequestingFacilityId",
                table: "BloodRequests");

            migrationBuilder.DropForeignKey(
                name: "FK_BloodRequests_Facilities_SourceFacilityId",
                table: "BloodRequests");

            migrationBuilder.DropForeignKey(
                name: "FK_BloodRequestStatusHistory_AspNetUsers_ChangedByUserId",
                table: "BloodRequestStatusHistory");

            migrationBuilder.DropForeignKey(
                name: "FK_BloodRequestStatusHistory_BloodRequests_BloodRequestId",
                table: "BloodRequestStatusHistory");

            migrationBuilder.DropForeignKey(
                name: "FK_FacilityStaff_Facilities_FacilityId",
                table: "FacilityStaff");

            migrationBuilder.DropForeignKey(
                name: "FK_InventoryTransactions_BloodInventory_BloodInventoryId",
                table: "InventoryTransactions");

            migrationBuilder.DropForeignKey(
                name: "FK_Notifications_AspNetUsers_RecipientUserId",
                table: "Notifications");

            migrationBuilder.DropIndex(
                name: "IX_Notifications_RecipientUserId",
                table: "Notifications");

            migrationBuilder.DropIndex(
                name: "IX_InventoryTransactions_BloodInventoryId",
                table: "InventoryTransactions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_InventoryTransactions_ReservedAfter",
                table: "InventoryTransactions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_InventoryTransactions_TotalAfter",
                table: "InventoryTransactions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_InventoryTransactions_TotalUnitsChange",
                table: "InventoryTransactions");

            migrationBuilder.DropIndex(
                name: "IX_FacilityStaff_FacilityId_UserId",
                table: "FacilityStaff");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Facilities_Name_NotEmpty",
                table: "Facilities");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Facilities_RegistrationNumber_NotEmpty",
                table: "Facilities");

            migrationBuilder.DropIndex(
                name: "IX_BloodRequestStatusHistory_BloodRequestId",
                table: "BloodRequestStatusHistory");

            migrationBuilder.DropIndex(
                name: "IX_BloodRequestStatusHistory_ChangedByUserId",
                table: "BloodRequestStatusHistory");

            migrationBuilder.DropIndex(
                name: "IX_BloodRequests_BloodNeedId",
                table: "BloodRequests");

            migrationBuilder.DropIndex(
                name: "IX_BloodRequests_FulfilledByAdminId",
                table: "BloodRequests");

            migrationBuilder.DropIndex(
                name: "IX_BloodRequests_RequestedByAdminId",
                table: "BloodRequests");

            migrationBuilder.DropIndex(
                name: "IX_BloodRequests_RequestingFacilityId",
                table: "BloodRequests");

            migrationBuilder.DropIndex(
                name: "IX_BloodRequests_RespondedByAdminId",
                table: "BloodRequests");

            migrationBuilder.DropIndex(
                name: "IX_BloodRequests_SourceFacilityId",
                table: "BloodRequests");

            migrationBuilder.DropCheckConstraint(
                name: "CK_BloodRequests_UnitsAccepted",
                table: "BloodRequests");

            migrationBuilder.DropCheckConstraint(
                name: "CK_BloodRequests_UnitsRequested",
                table: "BloodRequests");

            migrationBuilder.DropIndex(
                name: "IX_BloodNeeds_FacilityId",
                table: "BloodNeeds");

            migrationBuilder.DropIndex(
                name: "IX_BloodNeeds_RequestedByUserId",
                table: "BloodNeeds");

            migrationBuilder.DropCheckConstraint(
                name: "CK_BloodNeeds_NeededByUtc",
                table: "BloodNeeds");

            migrationBuilder.DropCheckConstraint(
                name: "CK_BloodNeeds_UnitsNeeded",
                table: "BloodNeeds");

            migrationBuilder.DropCheckConstraint(
                name: "CK_BloodInventory_LowStockThreshold",
                table: "BloodInventory");

            migrationBuilder.DropCheckConstraint(
                name: "CK_BloodInventory_ReservedUnits",
                table: "BloodInventory");

            migrationBuilder.DropCheckConstraint(
                name: "CK_BloodInventory_TotalUnits",
                table: "BloodInventory");

            migrationBuilder.DropIndex(
                name: "IX_AuditLogs_ActorUserId",
                table: "AuditLogs");

            migrationBuilder.AlterColumn<string>(
                name: "Title",
                table: "Notifications",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(200)",
                oldMaxLength: 200);

            migrationBuilder.AlterColumn<string>(
                name: "RelatedEntityType",
                table: "Notifications",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "RecipientUserId",
                table: "Notifications",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)",
                oldMaxLength: 450);

            migrationBuilder.AlterColumn<string>(
                name: "Message",
                table: "Notifications",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(1000)",
                oldMaxLength: 1000);

            migrationBuilder.AlterColumn<string>(
                name: "ReferenceType",
                table: "InventoryTransactions",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Reason",
                table: "InventoryTransactions",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(500)",
                oldMaxLength: 500);

            migrationBuilder.AlterColumn<string>(
                name: "PerformedByUserId",
                table: "InventoryTransactions",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)",
                oldMaxLength: 450);

            migrationBuilder.AlterColumn<string>(
                name: "UserId",
                table: "FacilityStaff",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)",
                oldMaxLength: 450);

            migrationBuilder.AlterColumn<string>(
                name: "StatusReason",
                table: "FacilityStaff",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(500)",
                oldMaxLength: 500,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "CreatedByAdminId",
                table: "FacilityStaff",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)",
                oldMaxLength: 450);

            migrationBuilder.AlterColumn<string>(
                name: "RejectionReason",
                table: "Facilities",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(500)",
                oldMaxLength: 500,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "RegistrationNumber",
                table: "Facilities",
                type: "nvarchar(450)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<string>(
                name: "Region",
                table: "Facilities",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "Facilities",
                type: "nvarchar(450)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(200)",
                oldMaxLength: 200);

            migrationBuilder.AlterColumn<string>(
                name: "CreatedByUserId",
                table: "Facilities",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)",
                oldMaxLength: 450);

            migrationBuilder.AlterColumn<string>(
                name: "ContactPhone",
                table: "Facilities",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(30)",
                oldMaxLength: 30);

            migrationBuilder.AlterColumn<string>(
                name: "ContactEmail",
                table: "Facilities",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(256)",
                oldMaxLength: 256);

            migrationBuilder.AlterColumn<string>(
                name: "City",
                table: "Facilities",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<string>(
                name: "ApprovedByUserId",
                table: "Facilities",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)",
                oldMaxLength: 450,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Address",
                table: "Facilities",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(500)",
                oldMaxLength: 500);

            migrationBuilder.AlterColumn<string>(
                name: "Note",
                table: "BloodRequestStatusHistory",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(1000)",
                oldMaxLength: 1000,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "ChangedByUserId",
                table: "BloodRequestStatusHistory",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)",
                oldMaxLength: 450);

            migrationBuilder.AlterColumn<string>(
                name: "ResponseNote",
                table: "BloodRequests",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(1000)",
                oldMaxLength: 1000,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "RespondedByAdminId",
                table: "BloodRequests",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)",
                oldMaxLength: 450,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "RequestedByAdminId",
                table: "BloodRequests",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)",
                oldMaxLength: 450);

            migrationBuilder.AlterColumn<string>(
                name: "RequestNote",
                table: "BloodRequests",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(1000)",
                oldMaxLength: 1000,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "FulfilledByAdminId",
                table: "BloodRequests",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)",
                oldMaxLength: 450,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "RequestedByUserId",
                table: "BloodNeeds",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)",
                oldMaxLength: 450);

            migrationBuilder.AlterColumn<string>(
                name: "Note",
                table: "BloodNeeds",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(1000)",
                oldMaxLength: 1000,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "DecisionReason",
                table: "BloodNeeds",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(1000)",
                oldMaxLength: 1000,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Summary",
                table: "AuditLogs",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(1000)",
                oldMaxLength: 1000);

            migrationBuilder.AlterColumn<string>(
                name: "IpAddress",
                table: "AuditLogs",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(45)",
                oldMaxLength: 45,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "EntityType",
                table: "AuditLogs",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(200)",
                oldMaxLength: 200);

            migrationBuilder.AlterColumn<string>(
                name: "ActorUserId",
                table: "AuditLogs",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)",
                oldMaxLength: 450,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Action",
                table: "AuditLogs",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(200)",
                oldMaxLength: 200);
        }
    }
}
