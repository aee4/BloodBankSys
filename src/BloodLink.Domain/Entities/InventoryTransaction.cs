using BloodLink.Domain.Common;
using BloodLink.Domain.Enums;

namespace BloodLink.Domain.Entities;

public sealed class InventoryTransaction : Entity
{
    public Guid BloodInventoryId { get; set; }
    public InventoryTransactionType TransactionType { get; set; }
    public int TotalUnitsChange { get; set; }
    public int ReservedUnitsChange { get; set; }
    public int TotalAfter { get; set; }
    public int ReservedAfter { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string? ReferenceType { get; set; }
    public Guid? ReferenceId { get; set; }
    public string PerformedByUserId { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
