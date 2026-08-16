using BloodLink.Application.Contracts;
using BloodLink.Application.DTOs;
using BloodLink.Domain.Enums;
using BloodLink.Infrastructure.Services.Needs;
using BloodLink.Infrastructure.Services.Requests;
using BloodLink.Infrastructure.Tests.Services;


namespace	BloodLink.Acceptance.Tests;
public sealed class	EndToEndRequestFlowAcceptanceTests
{
	[Fact]
	public async Task Need_MovesThroughSearchRequestAcceptAndFulfil()
	{
		await using	var	dbContext =	WorkflowTestSupport.CreateDbContext();
		WorkflowTestSupport.AddUser(dbContext, "staff-a", RoleNames.FacilityStaff, WorkflowTestSupport.FacilityAId);
		WorkflowTestSupport.AddUser(dbContext, "admin-a", RoleNames.FacilityAdmin, WorkflowTestSupport.FacilityAId);
		WorkflowTestSupport.AddUser(dbContext, "admin-b", RoleNames.FacilityAdmin, WorkflowTestSupport.FacilityBId);
		
        var	staffUser =	new	FakeCurrentUserService
        {
			UserId = "staff-a",	FacilityId = WorkflowTestSupport.FacilityAId
	    };
		staffUser.RoleList.Add(RoleNames.FacilityStaff);
		var	adminAUser = new FakeCurrentUserService
		{
			UserId = "admin-a",	FacilityId = WorkflowTestSupport.FacilityAId
		};
		adminAUser.RoleList.Add(RoleNames.FacilityAdmin);
		var	adminBUser = new FakeCurrentUserService
		{
			UserId	= "admin-b", FacilityId	= WorkflowTestSupport.FacilityBId
		};
		adminBUser.RoleList.Add(RoleNames.FacilityAdmin);
		var	inventory =	new	FakeInventoryService();
		// Step 1:	staff raises a need
		var	needService	= new BloodNeedService(dbContext, staffUser);
		var	need = await needService.CreateAsync(new CreateBloodNeedRequest(
			BloodType.ONegative, 2,	UrgencyLevel.Urgent, DateTime.UtcNow.AddHours(6), "Ward	stock low"));
			Assert.Equal(BloodNeedStatus.PendingReview,	need.Status);
			// Step	2: facility	A admin	moves the need to Searching
			var	needServiceAsAdmin	= new BloodNeedService(dbContext, adminAUser);
			await needServiceAsAdmin.StartSearchAsync(new	NeedDecisionRequest(need.Id,	null));
			// Step	3: facility	A admin	sends a	request	to facility B
			var	requestService	= new BloodRequestService(dbContext, adminAUser, inventory);
			var request = await requestService.CreateFromNeedAsync(new CreateBloodRequestRequest(
            need.Id, WorkflowTestSupport.FacilityBId, 2, null));
			Assert.Equal(BloodRequestStatus.Sent,	request.Status);
			// Step	4: facility	B admin	accepts, which	should	reserve	stock
			var	requestServiceAsSource = new BloodRequestService(dbContext,	adminBUser,	inventory);
			await requestServiceAsSource.AcceptAsync(new RequestResponseRequest(request.Id,	2, null));
			Assert.Equal(1,	inventory.ReserveCalls);
			// Step 5: facility B admin fulfils, which should transfer stock
			await requestServiceAsSource.FulfilAsync(new FulfilRequestRequest(request.Id, null));
			Assert.Equal(1,	inventory.FulfilCalls);
			var	storedRequest =	await requestServiceAsSource.GetAsync(request.Id);
			Assert.Equal(BloodRequestStatus.Fulfilled, storedRequest!.Status);
				}
}


public sealed class	OnboardingAcceptanceTests
{
//	TODO:	waiting	on	Backend	Developer	1	(Poku	Nancy)	to	deliver	FacilityService.
//	When	ready,	test:
//	1.	A	public	registration	call	creates	a	Pending	facility	and	its	first	FacilityAdmin.
//	2.	SystemAdmin	ApproveAsync	moves	the	facility	to	Approved	and	activates	the	admin.
//	3.	SystemAdmin	RejectAsync	stores	a	reason	and	blocks	operational	access.
//	4.	A	non-SystemAdmin	calling	ApproveAsync	throws	UnauthorizedAccessException.
}
public	sealed	class	StaffAcceptanceTests
{
//	TODO:	waiting	on	Backend	Developer	1	(Poku	Nancy)	to	deliver	StaffService.
//	When	ready,	test:
//	1.	FacilityAdmin	can	create	staff	only	for	their	own	facility.
//	2.	A	pending	or	suspended	facility	cannot	create	staff.
//	3.	DeactivateStaffAsync	blocks	sign-in	for	that	user	afterward.
}
public	sealed	class	InventoryAcceptanceTests
{
//	TODO:	waiting	on	Backend	Developer	2	(Jephthah	Peprah)	to	deliver	InventoryService.
//	When	ready,	test:
//	1.	AdjustInventoryAsync	never	allows	TotalUnits	to	drop	below	ReservedUnits.
//	2.	Every	adjustment	creates	one	immutable	InventoryTransaction.
//	3.	SearchAvailabilityAsync	excludes	the	requesting	facility	and	any	pending	or	suspended	facility.
}