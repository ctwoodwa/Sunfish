using Sunfish.Blocks.Inspections.Models;
using Sunfish.Blocks.Inspections.Services;
using Sunfish.Blocks.PropertyEquipment.Models;
using Sunfish.Foundation.Assets.Common;
using Xunit;

namespace Sunfish.Blocks.Inspections.Tests;

public class EquipmentConditionAssessmentServiceTests
{
    private static readonly EntityId TestUnitId = new("unit", "test", "unit-1");

    private static async Task<(InMemoryInspectionsService svc, Inspection inProgressInspection)> MakeServiceWithStartedInspection()
    {
        var svc = new InMemoryInspectionsService();
        var template = await svc.CreateTemplateAsync(new CreateTemplateRequest
        {
            Name = "Standard Annual",
            Description = "Annual checklist",
            Items = [new InspectionChecklistItem(InspectionChecklistItemId.NewId(), "Test item", InspectionItemKind.PassFail, true)],
        });
        var scheduled = await svc.ScheduleAsync(new ScheduleInspectionRequest
        {
            TemplateId = template.Id,
            UnitId = TestUnitId,
            InspectorName = "Jane Doe",
            ScheduledDate = new DateOnly(2026, 5, 1),
            Trigger = InspectionTrigger.Annual,
        });
        var started = await svc.StartAsync(scheduled.Id);
        return (svc, started);
    }

    private static async Task<List<T>> CollectAsync<T>(IAsyncEnumerable<T> source)
    {
        var list = new List<T>();
        await foreach (var item in source)
            list.Add(item);
        return list;
    }

    [Fact]
    public async Task RecordEquipmentConditionAsync_persists_assessment()
    {
        var (svc, inspection) = await MakeServiceWithStartedInspection();
        var equipmentId = EquipmentId.NewId();

        var result = await svc.RecordEquipmentConditionAsync(new RecordEquipmentConditionRequest
        {
            InspectionId = inspection.Id,
            EquipmentId = equipmentId,
            Condition = ConditionRating.Good,
            ExpectedRemainingLifeYears = 8,
            Observations = "All good",
        });

        Assert.Equal(inspection.Id, result.InspectionId);
        Assert.Equal(equipmentId, result.EquipmentId);
        Assert.Equal(ConditionRating.Good, result.Condition);
        Assert.Equal(8, result.ExpectedRemainingLifeYears);
        Assert.Equal("All good", result.Observations);
        Assert.False(string.IsNullOrWhiteSpace(result.Id.Value));
    }

    [Fact]
    public async Task RecordEquipmentConditionAsync_throws_when_inspection_missing()
    {
        var svc = new InMemoryInspectionsService();
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.RecordEquipmentConditionAsync(new RecordEquipmentConditionRequest
            {
                InspectionId = InspectionId.NewId(),
                EquipmentId = EquipmentId.NewId(),
                Condition = ConditionRating.Good,
            }).AsTask());
    }

    [Fact]
    public async Task RecordEquipmentConditionAsync_throws_when_inspection_not_in_progress()
    {
        var svc = new InMemoryInspectionsService();
        var template = await svc.CreateTemplateAsync(new CreateTemplateRequest
        {
            Name = "T",
            Description = "T",
            Items = [new InspectionChecklistItem(InspectionChecklistItemId.NewId(), "I", InspectionItemKind.PassFail, true)],
        });
        var scheduled = await svc.ScheduleAsync(new ScheduleInspectionRequest
        {
            TemplateId = template.Id,
            UnitId = TestUnitId,
            InspectorName = "Inspector",
            ScheduledDate = new DateOnly(2026, 5, 1),
        });

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.RecordEquipmentConditionAsync(new RecordEquipmentConditionRequest
            {
                InspectionId = scheduled.Id,
                EquipmentId = EquipmentId.NewId(),
                Condition = ConditionRating.Good,
            }).AsTask());
    }

    [Fact]
    public async Task ListEquipmentConditionsAsync_returns_only_assessments_for_inspection()
    {
        var (svc, inspectionA) = await MakeServiceWithStartedInspection();
        var (svc2, _) = await MakeServiceWithStartedInspection();
        // Use svc2's separate state to ensure isolation by inspection id within the SAME service:
        var template2 = await svc.CreateTemplateAsync(new CreateTemplateRequest { Name = "T2", Description = "", Items = [new(InspectionChecklistItemId.NewId(), "I", InspectionItemKind.PassFail, true)] });
        var scheduledB = await svc.ScheduleAsync(new ScheduleInspectionRequest { TemplateId = template2.Id, UnitId = TestUnitId, InspectorName = "X", ScheduledDate = new DateOnly(2026, 5, 2) });
        var inspectionB = await svc.StartAsync(scheduledB.Id);

        await svc.RecordEquipmentConditionAsync(new RecordEquipmentConditionRequest { InspectionId = inspectionA.Id, EquipmentId = EquipmentId.NewId(), Condition = ConditionRating.Good });
        await svc.RecordEquipmentConditionAsync(new RecordEquipmentConditionRequest { InspectionId = inspectionA.Id, EquipmentId = EquipmentId.NewId(), Condition = ConditionRating.Fair });
        await svc.RecordEquipmentConditionAsync(new RecordEquipmentConditionRequest { InspectionId = inspectionB.Id, EquipmentId = EquipmentId.NewId(), Condition = ConditionRating.Poor });

        var aList = await CollectAsync(svc.ListEquipmentConditionsAsync(inspectionA.Id));
        var bList = await CollectAsync(svc.ListEquipmentConditionsAsync(inspectionB.Id));

        Assert.Equal(2, aList.Count);
        Assert.Single(bList);
        Assert.All(aList, a => Assert.Equal(inspectionA.Id, a.InspectionId));
    }

    [Fact]
    public async Task ListConditionHistoryForEquipmentAsync_returns_chronological_history_across_inspections()
    {
        var (svc, inspection1) = await MakeServiceWithStartedInspection();
        var equipmentId = EquipmentId.NewId();

        await svc.RecordEquipmentConditionAsync(new RecordEquipmentConditionRequest { InspectionId = inspection1.Id, EquipmentId = equipmentId, Condition = ConditionRating.Good });

        // Second inspection a year later — same unit, same equipment
        var template2 = await svc.CreateTemplateAsync(new CreateTemplateRequest { Name = "Annual 2", Description = "", Items = [new(InspectionChecklistItemId.NewId(), "I", InspectionItemKind.PassFail, true)] });
        var scheduled2 = await svc.ScheduleAsync(new ScheduleInspectionRequest { TemplateId = template2.Id, UnitId = TestUnitId, InspectorName = "X", ScheduledDate = new DateOnly(2027, 5, 1), Trigger = InspectionTrigger.Annual });
        var inspection2 = await svc.StartAsync(scheduled2.Id);
        await svc.RecordEquipmentConditionAsync(new RecordEquipmentConditionRequest { InspectionId = inspection2.Id, EquipmentId = equipmentId, Condition = ConditionRating.Fair });

        var history = await CollectAsync(svc.ListConditionHistoryForEquipmentAsync(equipmentId));

        Assert.Equal(2, history.Count);
        Assert.True(history[0].ObservedAtUtc.Value <= history[1].ObservedAtUtc.Value);
        Assert.Equal(ConditionRating.Good, history[0].Condition);
        Assert.Equal(ConditionRating.Fair, history[1].Condition);
    }

    [Fact]
    public async Task ListConditionHistoryForEquipmentAsync_returns_empty_for_unknown_equipment()
    {
        var svc = new InMemoryInspectionsService();
        var history = await CollectAsync(svc.ListConditionHistoryForEquipmentAsync(EquipmentId.NewId()));
        Assert.Empty(history);
    }
}
