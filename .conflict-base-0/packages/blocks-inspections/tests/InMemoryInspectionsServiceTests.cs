using Sunfish.Blocks.Inspections.Models;
using Sunfish.Blocks.Inspections.Services;
using Sunfish.Foundation.Assets.Common;
using Xunit;

namespace Sunfish.Blocks.Inspections.Tests;

public class InMemoryInspectionsServiceTests
{
    // ── Helpers ──────────────────────────────────────────────────────────────

    private static readonly EntityId TestUnitId = new("unit", "test", "unit-1");
    private static readonly EntityId TestUnitId2 = new("unit", "test", "unit-2");

    private static InspectionChecklistItem Item(string prompt, InspectionItemKind kind = InspectionItemKind.YesNo, bool required = true)
        => new(InspectionChecklistItemId.NewId(), prompt, kind, required);

    private static CreateTemplateRequest MakeTemplateRequest(string name = "Standard Move-In") =>
        new()
        {
            Name = name,
            Description = "Standard move-in checklist",
            Items =
            [
                Item("Smoke detector operational?"),
                Item("Water heater functional?", InspectionItemKind.PassFail),
                Item("Overall cleanliness rating", InspectionItemKind.Rating1to5, required: false),
            ]
        };

    private static async Task<(InMemoryInspectionsService svc, InspectionTemplate template)> MakeServiceWithTemplate(string templateName = "Standard Move-In")
    {
        var svc = new InMemoryInspectionsService();
        var template = await svc.CreateTemplateAsync(MakeTemplateRequest(templateName));
        return (svc, template);
    }

    private static ScheduleInspectionRequest MakeScheduleRequest(InspectionTemplateId templateId, EntityId? unitId = null) =>
        new()
        {
            TemplateId = templateId,
            UnitId = unitId ?? TestUnitId,
            InspectorName = "Jane Smith",
            ScheduledDate = new DateOnly(2026, 5, 1),
        };

    private static async Task<List<T>> CollectAsync<T>(IAsyncEnumerable<T> source)
    {
        var list = new List<T>();
        await foreach (var item in source)
            list.Add(item);
        return list;
    }

    // ── Template tests ────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateTemplateAsync_RoundTrip_WithThreeChecklistItems()
    {
        var svc = new InMemoryInspectionsService();
        var request = MakeTemplateRequest();

        var template = await svc.CreateTemplateAsync(request);

        Assert.False(string.IsNullOrWhiteSpace(template.Id.Value));
        Assert.Equal("Standard Move-In", template.Name);
        Assert.Equal(3, template.Items.Count);

        var retrieved = await svc.GetTemplateAsync(template.Id);
        Assert.NotNull(retrieved);
        Assert.Equal(template.Id, retrieved.Id);
        Assert.Equal(3, retrieved.Items.Count);
    }

    // ── Inspection lifecycle tests ─────────────────────────────────────────

    [Fact]
    public async Task ScheduleAsync_CreatesInspection_InScheduledPhase()
    {
        var (svc, template) = await MakeServiceWithTemplate();

        var inspection = await svc.ScheduleAsync(MakeScheduleRequest(template.Id));

        Assert.False(string.IsNullOrWhiteSpace(inspection.Id.Value));
        Assert.Equal(InspectionPhase.Scheduled, inspection.Phase);
        Assert.Null(inspection.StartedAtUtc);
        Assert.Null(inspection.CompletedAtUtc);
        Assert.Empty(inspection.Responses);
    }

    [Fact]
    public async Task StartAsync_Scheduled_TransitionsToInProgress_AndSetsStartedAtUtc()
    {
        var (svc, template) = await MakeServiceWithTemplate();
        var inspection = await svc.ScheduleAsync(MakeScheduleRequest(template.Id));

        var started = await svc.StartAsync(inspection.Id);

        Assert.Equal(InspectionPhase.InProgress, started.Phase);
        Assert.NotNull(started.StartedAtUtc);
    }

    [Fact]
    public async Task StartAsync_WhenNotScheduled_ThrowsInvalidOperationException()
    {
        var (svc, template) = await MakeServiceWithTemplate();
        var inspection = await svc.ScheduleAsync(MakeScheduleRequest(template.Id));
        await svc.StartAsync(inspection.Id);

        // Inspection is now InProgress — starting again must throw.
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => svc.StartAsync(inspection.Id).AsTask());
    }

    [Fact]
    public async Task RecordResponseAsync_AppendsToResponses()
    {
        var (svc, template) = await MakeServiceWithTemplate();
        var inspection = await svc.ScheduleAsync(MakeScheduleRequest(template.Id));
        await svc.StartAsync(inspection.Id);

        var response = new InspectionResponse(template.Items[0].Id, "yes", null);
        var updated = await svc.RecordResponseAsync(inspection.Id, response);

        Assert.Single(updated.Responses);
        Assert.Equal("yes", updated.Responses[0].ResponseValue);
    }

    [Fact]
    public async Task CompleteAsync_InProgress_TransitionsToCompleted_AndSetsCompletedAtUtc()
    {
        var (svc, template) = await MakeServiceWithTemplate();
        var inspection = await svc.ScheduleAsync(MakeScheduleRequest(template.Id));
        await svc.StartAsync(inspection.Id);

        var completed = await svc.CompleteAsync(inspection.Id);

        Assert.Equal(InspectionPhase.Completed, completed.Phase);
        Assert.NotNull(completed.CompletedAtUtc);
    }

    [Fact]
    public async Task CompleteAsync_WhenNotInProgress_ThrowsInvalidOperationException()
    {
        var (svc, template) = await MakeServiceWithTemplate();
        var inspection = await svc.ScheduleAsync(MakeScheduleRequest(template.Id));

        // Inspection is Scheduled, not InProgress.
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => svc.CompleteAsync(inspection.Id).AsTask());
    }

    [Fact]
    public async Task GetInspectionAsync_UnknownId_ReturnsNull()
    {
        var svc = new InMemoryInspectionsService();

        var result = await svc.GetInspectionAsync(new InspectionId("no-such-id"));

        Assert.Null(result);
    }

    [Fact]
    public async Task ListInspectionsAsync_FiltersByUnitId()
    {
        var (svc, template) = await MakeServiceWithTemplate();

        await svc.ScheduleAsync(MakeScheduleRequest(template.Id, TestUnitId));
        await svc.ScheduleAsync(MakeScheduleRequest(template.Id, TestUnitId));
        await svc.ScheduleAsync(MakeScheduleRequest(template.Id, TestUnitId2));

        var forUnit1 = await CollectAsync(svc.ListInspectionsAsync(new ListInspectionsQuery { UnitId = TestUnitId }));
        var forUnit2 = await CollectAsync(svc.ListInspectionsAsync(new ListInspectionsQuery { UnitId = TestUnitId2 }));

        Assert.Equal(2, forUnit1.Count);
        Assert.Single(forUnit2);
    }

    // ── Deficiency tests ──────────────────────────────────────────────────

    [Fact]
    public async Task RecordDeficiencyAsync_LinksToInspection()
    {
        var (svc, template) = await MakeServiceWithTemplate();
        var inspection = await svc.ScheduleAsync(MakeScheduleRequest(template.Id));
        await svc.StartAsync(inspection.Id);

        var deficiency = await svc.RecordDeficiencyAsync(new RecordDeficiencyRequest
        {
            InspectionId = inspection.Id,
            ItemId = template.Items[0].Id,
            Severity = DeficiencySeverity.High,
            Description = "Smoke detector battery missing",
        });

        Assert.Equal(inspection.Id, deficiency.InspectionId);
        Assert.Equal(DeficiencyStatus.Open, deficiency.Status);

        var list = await CollectAsync(svc.ListDeficienciesAsync(inspection.Id));
        Assert.Single(list);
        Assert.Equal(deficiency.Id, list[0].Id);
    }

    // ── Report tests ──────────────────────────────────────────────────────

    [Fact]
    public async Task GenerateReportAsync_CountsItemsAndDeficienciesCorrectly()
    {
        var (svc, template) = await MakeServiceWithTemplate();
        var inspection = await svc.ScheduleAsync(MakeScheduleRequest(template.Id));
        await svc.StartAsync(inspection.Id);

        // Record 2 of 3 items (items[0] = YesNo/yes = pass, items[1] = PassFail/fail = fail).
        await svc.RecordResponseAsync(inspection.Id, new InspectionResponse(template.Items[0].Id, "yes", null));
        await svc.RecordResponseAsync(inspection.Id, new InspectionResponse(template.Items[1].Id, "fail", null));
        await svc.CompleteAsync(inspection.Id);

        // Record one deficiency.
        await svc.RecordDeficiencyAsync(new RecordDeficiencyRequest
        {
            InspectionId = inspection.Id,
            ItemId = template.Items[1].Id,
            Severity = DeficiencySeverity.Medium,
            Description = "Water heater not functional",
        });

        var report = await svc.GenerateReportAsync(inspection.Id);

        Assert.Equal(inspection.Id, report.InspectionId);
        Assert.Equal(3, report.TotalItems);   // 3 items in template
        Assert.Equal(1, report.PassedItems);  // only items[0] passed
        Assert.Equal(1, report.DeficiencyCount);
    }

    // ── Concurrency test ──────────────────────────────────────────────────

    [Fact]
    public async Task ConcurrentRecordResponseAsync_OnSameInspection_AreSerializedNoLostResponses()
    {
        var (svc, template) = await MakeServiceWithTemplate();
        var inspection = await svc.ScheduleAsync(MakeScheduleRequest(template.Id));
        await svc.StartAsync(inspection.Id);

        // Fire 10 concurrent RecordResponseAsync calls on the same inspection.
        // Each records a distinct "yes" answer against the first checklist item.
        // After all complete, the responses list must have exactly 10 entries.
        const int concurrency = 10;
        var tasks = Enumerable.Range(0, concurrency)
            .Select(_ => svc.RecordResponseAsync(
                inspection.Id,
                new InspectionResponse(template.Items[0].Id, "yes", null)).AsTask())
            .ToArray();

        await Task.WhenAll(tasks);

        var final = await svc.GetInspectionAsync(inspection.Id);
        Assert.NotNull(final);
        Assert.Equal(concurrency, final.Responses.Count);
    }
}
