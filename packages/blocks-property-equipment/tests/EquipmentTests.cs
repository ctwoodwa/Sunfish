using System.Text.Json;
using Sunfish.Blocks.Properties.Models;
using Sunfish.Blocks.PropertyEquipment.Models;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.MultiTenancy;
using Xunit;

namespace Sunfish.Blocks.PropertyEquipment.Tests;

public class EquipmentTests
{
    private static Equipment NewSample() => new()
    {
        Id = EquipmentId.NewId(),
        TenantId = new TenantId("tenant-a"),
        Property = PropertyId.NewId(),
        Class = EquipmentClass.WaterHeater,
        DisplayName = "Master bath water heater",
        Make = "Rheem",
        Model = "XR50T06EC36U1",
        SerialNumber = "SN12345",
        LocationInProperty = "Garage west wall",
        InstalledAt = new DateTimeOffset(2024, 6, 1, 0, 0, 0, TimeSpan.Zero),
        AcquisitionCost = 1_250m,
        ExpectedUsefulLifeYears = 12,
        Warranty = new WarrantyMetadata
        {
            StartsAt = new DateTimeOffset(2024, 6, 1, 0, 0, 0, TimeSpan.Zero),
            ExpiresAt = new DateTimeOffset(2030, 6, 1, 0, 0, 0, TimeSpan.Zero),
            Provider = "Manufacturer",
        },
        CreatedAt = new DateTimeOffset(2026, 4, 28, 0, 0, 0, TimeSpan.Zero),
    };

    [Fact]
    public void Implements_IMustHaveTenant()
    {
        Assert.IsAssignableFrom<IMustHaveTenant>(NewSample());
    }

    [Fact]
    public void Json_round_trip_preserves_all_fields()
    {
        var original = NewSample() with
        {
            AcquisitionReceiptRef = "receipt-xyz",
            Notes = "Pilot light replaced 2025",
            PrimaryPhotoBlobRef = "blob://photos/wh1",
            DisposedAt = new DateTimeOffset(2026, 4, 1, 12, 0, 0, TimeSpan.Zero),
            DisposalReason = "Replaced with tankless",
        };

        var json = JsonSerializer.Serialize(original);
        Assert.Equal(original, JsonSerializer.Deserialize<Equipment>(json));
    }

    [Fact]
    public void Records_with_same_fields_are_equal()
    {
        var a = NewSample();
        Assert.Equal(a, a with { });
    }

    [Fact]
    public void With_expression_changes_field_in_place()
    {
        var a = NewSample();
        var b = a with { DisplayName = "Garage water heater" };
        Assert.NotEqual(a, b);
        Assert.Equal("Master bath water heater", a.DisplayName);
    }
}
