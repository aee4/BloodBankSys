USE BloodLink_Development;
GO

-- ============================================================================
-- SCRIPT: 03_enforce_constraints.sql
-- PURPOSE: Enforce database-level CHECK constraints for business invariants
-- ============================================================================

-- 1. Ensure BloodInventory cannot have negative stock or illegal reservations
IF NOT EXISTS (SELECT * FROM sys.check_constraints WHERE name = 'CK_BloodInventory_NonNegativeUnits')
BEGIN
    ALTER TABLE BloodInventory
    ADD CONSTRAINT CK_BloodInventory_NonNegativeUnits 
    CHECK (TotalUnits >= 0 AND ReservedUnits >= 0);
END
GO

IF NOT EXISTS (SELECT * FROM sys.check_constraints WHERE name = 'CK_BloodInventory_ReservedNotExceedTotal')
BEGIN
    ALTER TABLE BloodInventory
    ADD CONSTRAINT CK_BloodInventory_ReservedNotExceedTotal 
    CHECK (ReservedUnits <= TotalUnits);
END
GO

-- 2. Ensure BloodRequests cannot have non-positive requests or excess acceptance
IF NOT EXISTS (SELECT * FROM sys.check_constraints WHERE name = 'CK_BloodRequests_ValidUnits')
BEGIN
    ALTER TABLE BloodRequests
    ADD CONSTRAINT CK_BloodRequests_ValidUnits 
    CHECK (UnitsRequested > 0 AND UnitsAccepted >= 0 AND UnitsAccepted <= UnitsRequested);
END
GO

PRINT 'All database integrity constraints applied successfully.';