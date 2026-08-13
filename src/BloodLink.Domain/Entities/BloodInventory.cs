using BloodLink.Domain.Common;
using BloodLink.Domain.Enums;

namespace BloodLink.Domain.Entities;

public sealed class BloodInventory : Entity
{
    public Guid FacilityId { get; set; }
    public BloodType BloodType { get; set; }
    public int TotalUnits { get; set; }
    public int ReservedUnits { get; set; }
    public int LowStockThreshold { get; set; }
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public byte[] RowVersion { get; set; } = [];

    public int AvailableUnits => TotalUnits - ReservedUnits;
}
