USE BloodLink_Development;
GO

-- ============================================================================
-- SCRIPT: 02_inventory_integrity_checks.sql
-- PURPOSE: Integrity verification, discrepancy audits, and availability checks
-- ============================================================================

-- ----------------------------------------------------------------------------
-- 1. DATA INTEGRITY AUDIT: Check for Illegal Stock States
-- (Rule: TotalUnits >= 0, ReservedUnits >= 0, ReservedUnits <= TotalUnits)
-- Expected Result: 0 rows returned (no violations)
-- ----------------------------------------------------------------------------
SELECT 
    Id,
    FacilityId,
    BloodType,
    TotalUnits,
    ReservedUnits,
    (TotalUnits - ReservedUnits) AS AvailableUnits,
    CASE 
        WHEN TotalUnits < 0 THEN 'Negative Total Units'
        WHEN ReservedUnits < 0 THEN 'Negative Reserved Units'
        WHEN ReservedUnits > TotalUnits THEN 'Reserved Exceeds Total'
        ELSE 'Valid'
    END AS ViolationReason
FROM BloodInventory
WHERE TotalUnits < 0 
   OR ReservedUnits < 0 
   OR ReservedUnits > TotalUnits;

-- ----------------------------------------------------------------------------
-- 2. BLOOD REQUEST CONSISTENCY AUDIT
-- (Rule: UnitsRequested > 0, UnitsAccepted <= UnitsRequested)
-- ----------------------------------------------------------------------------
SELECT 
    Id,
    BloodNeedId,
    RequestingFacilityId,
    SourceFacilityId,
    BloodType,
    UnitsRequested,
    UnitsAccepted,
    Status,
    CASE 
        WHEN UnitsRequested <= 0 THEN 'UnitsRequested must be positive'
        WHEN UnitsAccepted < 0 THEN 'UnitsAccepted cannot be negative'
        WHEN UnitsAccepted > UnitsRequested THEN 'UnitsAccepted exceeds UnitsRequested'
        ELSE 'Valid'
    END AS RequestViolationReason
FROM BloodRequests
WHERE UnitsRequested <= 0
   OR UnitsAccepted < 0
   OR UnitsAccepted > UnitsRequested;

-- ----------------------------------------------------------------------------
-- 3. INVENTORY TRANSACTION MATHEMATICAL RECONCILIATION
-- (Rule: TotalAfter = PreviousTotal + TotalUnitsChange)
-- ----------------------------------------------------------------------------
SELECT 
    t.Id,
    t.BloodInventoryId,
    t.TransactionType,
    t.TotalUnitsChange,
    t.ReservedUnitsChange,
    t.TotalAfter,
    t.ReservedAfter,
    t.Reason,
    t.CreatedAtUtc
FROM InventoryTransactions t
ORDER BY t.CreatedAtUtc DESC;

-- ----------------------------------------------------------------------------
-- 4. BLOOD AVAILABILITY REPORT (Business Query)
-- Joins Facilities with BloodInventory to show real-time available stock
-- ----------------------------------------------------------------------------
SELECT 
    f.Name AS FacilityName,
    f.City,
    f.Region,
    f.ContactPhone,
    bi.BloodType,
    bi.TotalUnits,
    bi.ReservedUnits,
    (bi.TotalUnits - bi.ReservedUnits) AS AvailableUnits,
    bi.LowStockThreshold,
    CASE 
        WHEN (bi.TotalUnits - bi.ReservedUnits) = 0 THEN 'OUT OF STOCK'
        WHEN (bi.TotalUnits - bi.ReservedUnits) <= bi.LowStockThreshold THEN 'LOW STOCK'
        ELSE 'ADEQUATE'
    END AS StockStatus
FROM BloodInventory bi
INNER JOIN Facilities f ON bi.FacilityId = f.Id
WHERE f.Status = 1 -- Active facilities only
ORDER BY f.Name, bi.BloodType;