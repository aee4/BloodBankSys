namespace BloodLink.Acceptance.Tests;

public sealed class InventoryAcceptanceTests
{
    // TODO: waiting on Backend Developer 2 (Jephthah Peprah) to deliver InventoryService.
    // When ready, test:
    // 1. AdjustInventoryAsync never allows TotalUnits to drop below ReservedUnits.
    // 2. Every adjustment creates one immutable InventoryTransaction.
    // 3. SearchAvailabilityAsync excludes the requesting facility and any pending or suspended facility.
}
