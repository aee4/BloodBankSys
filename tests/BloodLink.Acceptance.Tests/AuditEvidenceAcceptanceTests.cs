using BloodLink.Acceptance.Tests.Support;
using BloodLink.Application.Contracts;
using BloodLink.Application.DTOs;
using BloodLink.Domain.Enums;
using BloodLink.Infrastructure.Services.Needs;

namespace BloodLink.Acceptance.Tests;

public sealed class AuditEvidenceAcceptanceTests
{
    [Fact]
    public async Task CreatingABloodNeed_ShouldWriteAnAuditLogEntry()
    {
        // This test is expected to fail right now. AuditLogs exists as a table in
        // BloodLinkDbContext, but nothing in BloodNeedService writes to it yet.
        // Leaving this test in, failing, is intentional, it documents the gap
        // until Backend Developer 3 wires in real audit logging.
        await using var dbContext = WorkflowTestSupport.CreateDbContext();
        WorkflowTestSupport.AddUser(dbContext, "staff-a", RoleNames.FacilityStaff, WorkflowTestSupport.FacilityAId);
        var staffUser = new FakeCurrentUserService { UserId = "staff-a", FacilityId = WorkflowTestSupport.FacilityAId };
        staffUser.RoleList.Add(RoleNames.FacilityStaff);

        var needService = new BloodNeedService(dbContext, staffUser);
        var need = await needService.CreateAsync(new CreateBloodNeedRequest(
            BloodType.ONegative, 1, UrgencyLevel.Urgent, DateTime.UtcNow.AddHours(4), null));

        var auditEntry = dbContext.AuditLogs.SingleOrDefault(entry => entry.EntityId == need.Id);

        Assert.NotNull(auditEntry);
    }
}