using System.Text.Json;
using Sunfish.Blocks.Inspections.Models;
using Sunfish.Blocks.PropertyEquipment.Models;
using Sunfish.Foundation.Assets.Common;
using Xunit;

namespace Sunfish.Blocks.Inspections.Tests;

public class EquipmentConditionAssessmentTests
{
    private static EquipmentConditionAssessment NewSample() => new()
    {
        Id = EquipmentConditionAssessmentId.NewId(),
        InspectionId = InspectionId.NewId(),
        EquipmentId = EquipmentId.NewId(),
        Condition = ConditionRating.Fair,
        ExpectedRemainingLifeYears = 5,
        Observations = "Minor scaling on heating element",
        Recommendations = "Schedule descale within 12 months",
        ObservedAtUtc = Instant.Now,
    };

    [Fact]
    public void Json_round_trip_preserves_all_fields()
    {
        var original = NewSample() with
        {
            // Use List<string> so the deserialized record's PhotoBlobRefs (which deserializes
            // to List<string>) compares equal under record-equality.
            PhotoBlobRefs = new List<string> { "blob://photos/abc", "blob://photos/def" },
        };
        var json = JsonSerializer.Serialize(original);
        var roundTripped = JsonSerializer.Deserialize<EquipmentConditionAssessment>(json);
        Assert.NotNull(roundTripped);
        Assert.Equal(original.Id, roundTripped!.Id);
        Assert.Equal(original.InspectionId, roundTripped.InspectionId);
        Assert.Equal(original.EquipmentId, roundTripped.EquipmentId);
        Assert.Equal(original.Condition, roundTripped.Condition);
        Assert.Equal(original.ExpectedRemainingLifeYears, roundTripped.ExpectedRemainingLifeYears);
        Assert.Equal(original.Observations, roundTripped.Observations);
        Assert.Equal(original.Recommendations, roundTripped.Recommendations);
        Assert.Equal(original.PhotoBlobRefs, roundTripped.PhotoBlobRefs);
        Assert.Equal(original.ObservedAtUtc, roundTripped.ObservedAtUtc);
    }

    [Fact]
    public void Records_with_same_fields_are_equal()
    {
        var a = NewSample();
        var b = a with { };
        Assert.Equal(a, b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }

    [Fact]
    public void With_changes_field_in_place()
    {
        var a = NewSample();
        var b = a with { Condition = ConditionRating.Failed };
        Assert.NotEqual(a, b);
        Assert.Equal(ConditionRating.Fair, a.Condition);
        Assert.Equal(ConditionRating.Failed, b.Condition);
    }

    [Fact]
    public void EquipmentConditionAssessmentId_implicit_string_conversion()
    {
        var raw = "ec-123";
        EquipmentConditionAssessmentId id = raw;
        string back = id;
        Assert.Equal(raw, back);
    }

    [Fact]
    public void EquipmentConditionAssessmentId_NewId_is_unique()
    {
        Assert.NotEqual(EquipmentConditionAssessmentId.NewId(), EquipmentConditionAssessmentId.NewId());
    }

    [Fact]
    public void ConditionRating_ordering_matches_Good_to_Failed_progression()
    {
        // Used by the move-in/out delta to compute Degraded; verify the
        // numeric ordering matches the semantic ordering.
        Assert.True(ConditionRating.Good < ConditionRating.Fair);
        Assert.True(ConditionRating.Fair < ConditionRating.Poor);
        Assert.True(ConditionRating.Poor < ConditionRating.Failed);
    }
}
