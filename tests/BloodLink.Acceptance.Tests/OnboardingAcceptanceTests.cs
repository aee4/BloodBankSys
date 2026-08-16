namespace BloodLink.Acceptance.Tests;

public sealed class OnboardingAcceptanceTests
{
    // TODO: waiting on Backend Developer 1 (Poku Nancy) to deliver FacilityService.
    // When ready, test:
    // 1. A public registration call creates a Pending facility and its first FacilityAdmin.
    // 2. SystemAdmin ApproveAsync moves the facility to Approved and activates the admin.
    // 3. SystemAdmin RejectAsync stores a reason and blocks operational access.
    // 4. A non-SystemAdmin calling ApproveAsync throws UnauthorizedAccessException.
}
