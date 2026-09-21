using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BloodLink.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class EnforceCanonicalDatabaseIntegrity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                IF EXISTS (SELECT 1 FROM [BloodInventory] WHERE [TotalUnits] < 0 OR [ReservedUnits] < 0 OR [ReservedUnits] > [TotalUnits] OR [LowStockThreshold] < 0)
                    THROW 51000, 'Phase 2 migration blocked: BloodInventory contains invalid unit counts.', 1;
                IF EXISTS (SELECT 1 FROM [InventoryTransactions] WHERE ([TotalUnitsChange] = 0 AND [ReservedUnitsChange] = 0) OR [TotalAfter] < 0 OR [ReservedAfter] < 0 OR [ReservedAfter] > [TotalAfter])
                    THROW 51000, 'Phase 2 migration blocked: InventoryTransactions contains invalid unit counts.', 1;
                IF EXISTS (SELECT 1 FROM [BloodNeeds] WHERE [UnitsNeeded] <= 0)
                    THROW 51000, 'Phase 2 migration blocked: BloodNeeds contains non-positive units.', 1;
                IF EXISTS (SELECT 1 FROM [BloodRequests] WHERE [UnitsRequested] <= 0 OR ([UnitsAccepted] IS NOT NULL AND ([UnitsAccepted] <= 0 OR [UnitsAccepted] > [UnitsRequested])) OR [RequestingFacilityId] = [SourceFacilityId])
                    THROW 51000, 'Phase 2 migration blocked: BloodRequests contains invalid units or matching facilities.', 1;
                IF EXISTS (SELECT [Name] FROM [Facilities] GROUP BY [Name] HAVING COUNT(*) > 1)
                    THROW 51000, 'Phase 2 migration blocked: duplicate facility names exist.', 1;
                IF EXISTS (SELECT [RegistrationNumber] FROM [Facilities] GROUP BY [RegistrationNumber] HAVING COUNT(*) > 1)
                    THROW 51000, 'Phase 2 migration blocked: duplicate facility registration numbers exist.', 1;
                IF EXISTS (SELECT [UserId] FROM [FacilityStaff] GROUP BY [UserId] HAVING COUNT(*) > 1)
                    THROW 51000, 'Phase 2 migration blocked: a user has multiple FacilityStaff records.', 1;
                """);

            migrationBuilder.DropIndex(
                name: "IX_Facilities_Name_RegistrationNumber",
                table: "Facilities");

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

            migrationBuilder.AlterColumn<string>(
                name: "LastName",
                table: "AspNetUsers",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "FirstName",
                table: "AspNetUsers",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_RecipientUserId_IsRead_CreatedAtUtc",
                table: "Notifications",
                columns: new[] { "RecipientUserId", "IsRead", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_InventoryTransactions_BloodInventoryId_CreatedAtUtc",
                table: "InventoryTransactions",
                columns: new[] { "BloodInventoryId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_InventoryTransactions_PerformedByUserId",
                table: "InventoryTransactions",
                column: "PerformedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryTransactions_ReferenceType_ReferenceId",
                table: "InventoryTransactions",
                columns: new[] { "ReferenceType", "ReferenceId" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_InventoryTransactions_HasChange",
                table: "InventoryTransactions",
                sql: "[TotalUnitsChange] <> 0 OR [ReservedUnitsChange] <> 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_InventoryTransactions_ReservedAfter_NonNegative",
                table: "InventoryTransactions",
                sql: "[ReservedAfter] >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_InventoryTransactions_ReservedAfterWithinTotal",
                table: "InventoryTransactions",
                sql: "[ReservedAfter] <= [TotalAfter]");

            migrationBuilder.AddCheckConstraint(
                name: "CK_InventoryTransactions_TotalAfter_NonNegative",
                table: "InventoryTransactions",
                sql: "[TotalAfter] >= 0");

            migrationBuilder.CreateIndex(
                name: "IX_FacilityStaff_CreatedByAdminId",
                table: "FacilityStaff",
                column: "CreatedByAdminId");

            migrationBuilder.CreateIndex(
                name: "IX_FacilityStaff_FacilityId_Status",
                table: "FacilityStaff",
                columns: new[] { "FacilityId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_FacilityStaff_UserId",
                table: "FacilityStaff",
                column: "UserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Facilities_Name",
                table: "Facilities",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Facilities_Region_City",
                table: "Facilities",
                columns: new[] { "Region", "City" });

            migrationBuilder.CreateIndex(
                name: "IX_Facilities_RegistrationNumber",
                table: "Facilities",
                column: "RegistrationNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Facilities_Status",
                table: "Facilities",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_BloodRequestStatusHistory_BloodRequestId_ChangedAtUtc",
                table: "BloodRequestStatusHistory",
                columns: new[] { "BloodRequestId", "ChangedAtUtc" });

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
                name: "IX_BloodRequests_RequestingFacilityId_Status",
                table: "BloodRequests",
                columns: new[] { "RequestingFacilityId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_BloodRequests_RespondedByAdminId",
                table: "BloodRequests",
                column: "RespondedByAdminId");

            migrationBuilder.CreateIndex(
                name: "IX_BloodRequests_SourceFacilityId_Status",
                table: "BloodRequests",
                columns: new[] { "SourceFacilityId", "Status" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_BloodRequests_DistinctFacilities",
                table: "BloodRequests",
                sql: "[RequestingFacilityId] <> [SourceFacilityId]");

            migrationBuilder.AddCheckConstraint(
                name: "CK_BloodRequests_UnitsAccepted_Positive",
                table: "BloodRequests",
                sql: "[UnitsAccepted] IS NULL OR [UnitsAccepted] > 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_BloodRequests_UnitsAcceptedWithinRequested",
                table: "BloodRequests",
                sql: "[UnitsAccepted] IS NULL OR [UnitsAccepted] <= [UnitsRequested]");

            migrationBuilder.AddCheckConstraint(
                name: "CK_BloodRequests_UnitsRequested_Positive",
                table: "BloodRequests",
                sql: "[UnitsRequested] > 0");

            migrationBuilder.CreateIndex(
                name: "IX_BloodNeeds_FacilityId_Status",
                table: "BloodNeeds",
                columns: new[] { "FacilityId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_BloodNeeds_RequestedByUserId",
                table: "BloodNeeds",
                column: "RequestedByUserId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_BloodNeeds_UnitsNeeded_Positive",
                table: "BloodNeeds",
                sql: "[UnitsNeeded] > 0");

            migrationBuilder.CreateIndex(
                name: "IX_BloodInventory_BloodType_TotalUnits_ReservedUnits",
                table: "BloodInventory",
                columns: new[] { "BloodType", "TotalUnits", "ReservedUnits" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_BloodInventory_LowStockThreshold_NonNegative",
                table: "BloodInventory",
                sql: "[LowStockThreshold] >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_BloodInventory_ReservedUnits_NonNegative",
                table: "BloodInventory",
                sql: "[ReservedUnits] >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_BloodInventory_ReservedWithinTotal",
                table: "BloodInventory",
                sql: "[ReservedUnits] <= [TotalUnits]");

            migrationBuilder.AddCheckConstraint(
                name: "CK_BloodInventory_TotalUnits_NonNegative",
                table: "BloodInventory",
                sql: "[TotalUnits] >= 0");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_ActorUserId",
                table: "AuditLogs",
                column: "ActorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_EntityType_EntityId",
                table: "AuditLogs",
                columns: new[] { "EntityType", "EntityId" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_FacilityId_CreatedAtUtc",
                table: "AuditLogs",
                columns: new[] { "FacilityId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUsers_FacilityId",
                table: "AspNetUsers",
                column: "FacilityId");

            migrationBuilder.AddForeignKey(
                name: "FK_AspNetUsers_Facilities_FacilityId",
                table: "AspNetUsers",
                column: "FacilityId",
                principalTable: "Facilities",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_AuditLogs_AspNetUsers_ActorUserId",
                table: "AuditLogs",
                column: "ActorUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_AuditLogs_Facilities_FacilityId",
                table: "AuditLogs",
                column: "FacilityId",
                principalTable: "Facilities",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_BloodInventory_Facilities_FacilityId",
                table: "BloodInventory",
                column: "FacilityId",
                principalTable: "Facilities",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_BloodNeeds_AspNetUsers_RequestedByUserId",
                table: "BloodNeeds",
                column: "RequestedByUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_BloodNeeds_Facilities_FacilityId",
                table: "BloodNeeds",
                column: "FacilityId",
                principalTable: "Facilities",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_BloodRequests_AspNetUsers_FulfilledByAdminId",
                table: "BloodRequests",
                column: "FulfilledByAdminId",
                principalTable: "AspNetUsers",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_BloodRequests_AspNetUsers_RequestedByAdminId",
                table: "BloodRequests",
                column: "RequestedByAdminId",
                principalTable: "AspNetUsers",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_BloodRequests_AspNetUsers_RespondedByAdminId",
                table: "BloodRequests",
                column: "RespondedByAdminId",
                principalTable: "AspNetUsers",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_BloodRequests_BloodNeeds_BloodNeedId",
                table: "BloodRequests",
                column: "BloodNeedId",
                principalTable: "BloodNeeds",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_BloodRequests_Facilities_RequestingFacilityId",
                table: "BloodRequests",
                column: "RequestingFacilityId",
                principalTable: "Facilities",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_BloodRequests_Facilities_SourceFacilityId",
                table: "BloodRequests",
                column: "SourceFacilityId",
                principalTable: "Facilities",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_BloodRequestStatusHistory_AspNetUsers_ChangedByUserId",
                table: "BloodRequestStatusHistory",
                column: "ChangedByUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_BloodRequestStatusHistory_BloodRequests_BloodRequestId",
                table: "BloodRequestStatusHistory",
                column: "BloodRequestId",
                principalTable: "BloodRequests",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_FacilityStaff_AspNetUsers_CreatedByAdminId",
                table: "FacilityStaff",
                column: "CreatedByAdminId",
                principalTable: "AspNetUsers",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_FacilityStaff_AspNetUsers_UserId",
                table: "FacilityStaff",
                column: "UserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_FacilityStaff_Facilities_FacilityId",
                table: "FacilityStaff",
                column: "FacilityId",
                principalTable: "Facilities",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_InventoryTransactions_AspNetUsers_PerformedByUserId",
                table: "InventoryTransactions",
                column: "PerformedByUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_InventoryTransactions_BloodInventory_BloodInventoryId",
                table: "InventoryTransactions",
                column: "BloodInventoryId",
                principalTable: "BloodInventory",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Notifications_AspNetUsers_RecipientUserId",
                table: "Notifications",
                column: "RecipientUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AspNetUsers_Facilities_FacilityId",
                table: "AspNetUsers");

            migrationBuilder.DropForeignKey(
                name: "FK_AuditLogs_AspNetUsers_ActorUserId",
                table: "AuditLogs");

            migrationBuilder.DropForeignKey(
                name: "FK_AuditLogs_Facilities_FacilityId",
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
                name: "FK_FacilityStaff_AspNetUsers_CreatedByAdminId",
                table: "FacilityStaff");

            migrationBuilder.DropForeignKey(
                name: "FK_FacilityStaff_AspNetUsers_UserId",
                table: "FacilityStaff");

            migrationBuilder.DropForeignKey(
                name: "FK_FacilityStaff_Facilities_FacilityId",
                table: "FacilityStaff");

            migrationBuilder.DropForeignKey(
                name: "FK_InventoryTransactions_AspNetUsers_PerformedByUserId",
                table: "InventoryTransactions");

            migrationBuilder.DropForeignKey(
                name: "FK_InventoryTransactions_BloodInventory_BloodInventoryId",
                table: "InventoryTransactions");

            migrationBuilder.DropForeignKey(
                name: "FK_Notifications_AspNetUsers_RecipientUserId",
                table: "Notifications");

            migrationBuilder.DropIndex(
                name: "IX_Notifications_RecipientUserId_IsRead_CreatedAtUtc",
                table: "Notifications");

            migrationBuilder.DropIndex(
                name: "IX_InventoryTransactions_BloodInventoryId_CreatedAtUtc",
                table: "InventoryTransactions");

            migrationBuilder.DropIndex(
                name: "IX_InventoryTransactions_PerformedByUserId",
                table: "InventoryTransactions");

            migrationBuilder.DropIndex(
                name: "IX_InventoryTransactions_ReferenceType_ReferenceId",
                table: "InventoryTransactions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_InventoryTransactions_HasChange",
                table: "InventoryTransactions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_InventoryTransactions_ReservedAfter_NonNegative",
                table: "InventoryTransactions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_InventoryTransactions_ReservedAfterWithinTotal",
                table: "InventoryTransactions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_InventoryTransactions_TotalAfter_NonNegative",
                table: "InventoryTransactions");

            migrationBuilder.DropIndex(
                name: "IX_FacilityStaff_CreatedByAdminId",
                table: "FacilityStaff");

            migrationBuilder.DropIndex(
                name: "IX_FacilityStaff_FacilityId_Status",
                table: "FacilityStaff");

            migrationBuilder.DropIndex(
                name: "IX_FacilityStaff_UserId",
                table: "FacilityStaff");

            migrationBuilder.DropIndex(
                name: "IX_Facilities_Name",
                table: "Facilities");

            migrationBuilder.DropIndex(
                name: "IX_Facilities_Region_City",
                table: "Facilities");

            migrationBuilder.DropIndex(
                name: "IX_Facilities_RegistrationNumber",
                table: "Facilities");

            migrationBuilder.DropIndex(
                name: "IX_Facilities_Status",
                table: "Facilities");

            migrationBuilder.DropIndex(
                name: "IX_BloodRequestStatusHistory_BloodRequestId_ChangedAtUtc",
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
                name: "IX_BloodRequests_RequestingFacilityId_Status",
                table: "BloodRequests");

            migrationBuilder.DropIndex(
                name: "IX_BloodRequests_RespondedByAdminId",
                table: "BloodRequests");

            migrationBuilder.DropIndex(
                name: "IX_BloodRequests_SourceFacilityId_Status",
                table: "BloodRequests");

            migrationBuilder.DropCheckConstraint(
                name: "CK_BloodRequests_DistinctFacilities",
                table: "BloodRequests");

            migrationBuilder.DropCheckConstraint(
                name: "CK_BloodRequests_UnitsAccepted_Positive",
                table: "BloodRequests");

            migrationBuilder.DropCheckConstraint(
                name: "CK_BloodRequests_UnitsAcceptedWithinRequested",
                table: "BloodRequests");

            migrationBuilder.DropCheckConstraint(
                name: "CK_BloodRequests_UnitsRequested_Positive",
                table: "BloodRequests");

            migrationBuilder.DropIndex(
                name: "IX_BloodNeeds_FacilityId_Status",
                table: "BloodNeeds");

            migrationBuilder.DropIndex(
                name: "IX_BloodNeeds_RequestedByUserId",
                table: "BloodNeeds");

            migrationBuilder.DropCheckConstraint(
                name: "CK_BloodNeeds_UnitsNeeded_Positive",
                table: "BloodNeeds");

            migrationBuilder.DropIndex(
                name: "IX_BloodInventory_BloodType_TotalUnits_ReservedUnits",
                table: "BloodInventory");

            migrationBuilder.DropCheckConstraint(
                name: "CK_BloodInventory_LowStockThreshold_NonNegative",
                table: "BloodInventory");

            migrationBuilder.DropCheckConstraint(
                name: "CK_BloodInventory_ReservedUnits_NonNegative",
                table: "BloodInventory");

            migrationBuilder.DropCheckConstraint(
                name: "CK_BloodInventory_ReservedWithinTotal",
                table: "BloodInventory");

            migrationBuilder.DropCheckConstraint(
                name: "CK_BloodInventory_TotalUnits_NonNegative",
                table: "BloodInventory");

            migrationBuilder.DropIndex(
                name: "IX_AuditLogs_ActorUserId",
                table: "AuditLogs");

            migrationBuilder.DropIndex(
                name: "IX_AuditLogs_EntityType_EntityId",
                table: "AuditLogs");

            migrationBuilder.DropIndex(
                name: "IX_AuditLogs_FacilityId_CreatedAtUtc",
                table: "AuditLogs");

            migrationBuilder.DropIndex(
                name: "IX_AspNetUsers_FacilityId",
                table: "AspNetUsers");

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

            migrationBuilder.AlterColumn<string>(
                name: "LastName",
                table: "AspNetUsers",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<string>(
                name: "FirstName",
                table: "AspNetUsers",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100);

            migrationBuilder.CreateIndex(
                name: "IX_Facilities_Name_RegistrationNumber",
                table: "Facilities",
                columns: new[] { "Name", "RegistrationNumber" },
                unique: true);
        }
    }
}
