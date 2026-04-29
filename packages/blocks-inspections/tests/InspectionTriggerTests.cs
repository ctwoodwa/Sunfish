using System.Text.Json;
using Sunfish.Blocks.Inspections.Models;
using Xunit;

namespace Sunfish.Blocks.Inspections.Tests;

public class InspectionTriggerTests
{
    [Fact]
    public void All_five_values_present()
    {
        var values = Enum.GetValues<InspectionTrigger>();
        Assert.Equal(5, values.Length);
        Assert.Contains(InspectionTrigger.Annual, values);
        Assert.Contains(InspectionTrigger.MoveIn, values);
        Assert.Contains(InspectionTrigger.MoveOut, values);
        Assert.Contains(InspectionTrigger.PostRepair, values);
        Assert.Contains(InspectionTrigger.OnDemand, values);
    }

    [Fact]
    public void Json_round_trips_as_string_default()
    {
        // Default JSON-serializer of an enum is integer; verify that's the
        // shape we're committing to (matches existing enum patterns in this
        // package — DeficiencySeverity, DeficiencyStatus, InspectionPhase).
        var json = JsonSerializer.Serialize(InspectionTrigger.MoveIn);
        Assert.Equal(((int)InspectionTrigger.MoveIn).ToString(), json);

        var roundTripped = JsonSerializer.Deserialize<InspectionTrigger>(json);
        Assert.Equal(InspectionTrigger.MoveIn, roundTripped);
    }
}
