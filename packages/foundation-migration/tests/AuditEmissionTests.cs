using System;
using System.Threading;
using System.Threading.Tasks;
using NSubstitute;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Crypto;
using Sunfish.Foundation.Migration.Audit;
using Sunfish.Foundation.Versioning;
using Sunfish.Kernel.Audit;
using Xunit;

namespace Sunfish.Foundation.Migration.Tests;

public sealed class AuditEmissionTests
{
    private static readonly TenantId TenantA = new("tenant-a");
    private const string NodeId = "node-A";
    private static readonly DateTimeOffset Now = new(2026, 5, 1, 12, 0, 0, TimeSpan.Zero);

    private static FormFactorProfile Phone() => new()
    {
        FormFactor = FormFactorKind.Phone,
        InputModalities = new() { InputModalityKind.Touch },
        DisplayClass = DisplayClassKind.Small,
        NetworkPosture = NetworkPostureKind.IntermittentConnected,
        StorageBudgetMb = 64_000,
        PowerProfile = PowerProfileKind.Battery,
        SensorSurface = new() { SensorKind.Camera, SensorKind.Mic, SensorKind.Gps, SensorKind.Accelerometer },
        InstanceClass = InstanceClassKind.SelfHost,
    };

    private static FormFactorProfile Watch() => new()
    {
        FormFactor = FormFactorKind.Watch,
        InputModalities = new() { InputModalityKind.Touch, InputModalityKind.Voice },
        DisplayClass = DisplayClassKind.MicroDisplay,
        NetworkPosture = NetworkPostureKind.IntermittentConnected,
        StorageBudgetMb = 4_096,
        PowerProfile = PowerProfileKind.LowPower,
        SensorSurface = new() { SensorKind.Mic, SensorKind.Accelerometer },
        InstanceClass = InstanceClassKind.SelfHost,
    };

    private static HardwareTierChangeEvent Change(FormFactorProfile prev, FormFactorProfile curr,
        TriggeringEventKind trigger = TriggeringEventKind.ManualReprofile) => new()
    {
        NodeId = NodeId,
        PreviousProfile = prev,
        CurrentProfile = curr,
        TriggeringEvent = trigger,
        DetectedAt = Now,
    };

    private static SequesteredRecord NewRecord(string recordId, string capability,
        bool isEncrypted = false, bool isCp = false) => new()
    {
        NodeId = NodeId,
        RecordId = recordId,
        RequiredCapability = capability,
        IsEncrypted = isEncrypted,
        IsPrimaryKeyEncrypted = false,
        IsCpClass = isCp,
    };

    private static (InMemoryFormFactorMigrationService svc, ISequestrationStore store, IAuditTrail trail, FakeTimeProvider time) NewAuditEnabled()
    {
        var store = new InMemorySequestrationStore();
        var trail = Substitute.For<IAuditTrail>();
        var signer = new Ed25519Signer(KeyPair.Generate());
        var time = new FakeTimeProvider(Now);
        var svc = new InMemoryFormFactorMigrationService(store, trail, signer, TenantA, time);
        return (svc, store, trail, time);
    }

    [Fact]
    public async Task ApplyMigrationAsync_AuditEnabled_EmitsHardwareTierChanged()
    {
        var (svc, _, trail, _) = NewAuditEnabled();

        await svc.ApplyMigrationAsync(Change(Phone(), Watch()));

        await trail.Received(1).AppendAsync(
            Arg.Is<AuditRecord>(r => r != null && r.EventType.Equals(AuditEventType.HardwareTierChanged) && r.TenantId.Equals(TenantA)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ApplyMigrationAsync_PlaintextSequestered_Emits()
    {
        var (svc, store, trail, _) = NewAuditEnabled();
        await store.RegisterAsync(NewRecord("r1", "sensor.Camera", isEncrypted: false));

        await svc.ApplyMigrationAsync(Change(Phone(), Watch()));

        await trail.Received(1).AppendAsync(
            Arg.Is<AuditRecord>(r => r != null && r.EventType.Equals(AuditEventType.PlaintextSequestered)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ApplyMigrationAsync_CiphertextSequestered_Emits()
    {
        var (svc, store, trail, _) = NewAuditEnabled();
        await store.RegisterAsync(NewRecord("r1", "sensor.Camera", isEncrypted: true));

        await svc.ApplyMigrationAsync(Change(Phone(), Watch()));

        await trail.Received(1).AppendAsync(
            Arg.Is<AuditRecord>(r => r != null && r.EventType.Equals(AuditEventType.CiphertextSequestered)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ApplyMigrationAsync_FormFactorQuorumIneligible_EmitsForCpRecord()
    {
        var (svc, store, trail, _) = NewAuditEnabled();
        await store.RegisterAsync(NewRecord("cp1", "sensor.Camera", isCp: true));

        await svc.ApplyMigrationAsync(Change(Phone(), Watch()));

        await trail.Received(1).AppendAsync(
            Arg.Is<AuditRecord>(r => r != null && r.EventType.Equals(AuditEventType.FormFactorQuorumIneligible)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ApplyMigrationAsync_DataReleased_EmitsOnSurfaceExpansion()
    {
        var (svc, store, trail, _) = NewAuditEnabled();
        await store.RegisterAsync(NewRecord("r1", "sensor.Camera"));
        await svc.ApplyMigrationAsync(Change(Phone(), Watch())); // sequester
        trail.ClearReceivedCalls();

        await svc.ApplyMigrationAsync(Change(Watch(), Phone())); // release

        await trail.Received(1).AppendAsync(
            Arg.Is<AuditRecord>(r => r != null && r.EventType.Equals(AuditEventType.DataReleased)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ApplyMigrationAsync_AdapterDowngrade_EmitsAdapterRollbackDetected()
    {
        var (svc, _, trail, _) = NewAuditEnabled();

        await svc.ApplyMigrationAsync(Change(Phone(), Watch(), TriggeringEventKind.AdapterDowngrade));

        await trail.Received(1).AppendAsync(
            Arg.Is<AuditRecord>(r => r != null && r.EventType.Equals(AuditEventType.AdapterRollbackDetected)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ApplyMigrationAsync_AdapterRollback_DedupsWithin6HourWindow()
    {
        var (svc, _, trail, time) = NewAuditEnabled();

        // First emission at T=0.
        await svc.ApplyMigrationAsync(Change(Phone(), Watch(), TriggeringEventKind.AdapterDowngrade));
        // Storm: 5 retries within 30 minutes — all deduped.
        for (var i = 0; i < 5; i++)
        {
            time.Advance(TimeSpan.FromMinutes(5));
            await svc.ApplyMigrationAsync(Change(Phone(), Watch(), TriggeringEventKind.AdapterDowngrade));
        }

        await trail.Received(1).AppendAsync(
            Arg.Is<AuditRecord>(r => r != null && r.EventType.Equals(AuditEventType.AdapterRollbackDetected)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ApplyMigrationAsync_AdapterRollback_RefiresAfter6HourWindow()
    {
        var (svc, _, trail, time) = NewAuditEnabled();

        await svc.ApplyMigrationAsync(Change(Phone(), Watch(), TriggeringEventKind.AdapterDowngrade));
        time.Advance(TimeSpan.FromHours(6).Add(TimeSpan.FromSeconds(1)));
        await svc.ApplyMigrationAsync(Change(Phone(), Watch(), TriggeringEventKind.AdapterDowngrade));

        await trail.Received(2).AppendAsync(
            Arg.Is<AuditRecord>(r => r != null && r.EventType.Equals(AuditEventType.AdapterRollbackDetected)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ApplyMigrationAsync_NonRollbackTrigger_DoesNotEmitAdapterRollback()
    {
        var (svc, _, trail, _) = NewAuditEnabled();

        await svc.ApplyMigrationAsync(Change(Phone(), Watch(), TriggeringEventKind.ManualReprofile));

        await trail.DidNotReceive().AppendAsync(
            Arg.Is<AuditRecord>(r => r != null && r.EventType.Equals(AuditEventType.AdapterRollbackDetected)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CanWriteFieldAsync_AuditEnabled_EmitsFieldWriteSequesteredOnRejection()
    {
        var (svc, store, trail, _) = NewAuditEnabled();
        await store.RegisterAsync(NewRecord("r1#tenant_demographics", "sensor.BiometricAuth"));
        await svc.ApplyMigrationAsync(Change(Watch(), Phone())); // Phone has no BiometricAuth

        var canWrite = await svc.CanWriteFieldAsync(NodeId, "r1#tenant_demographics");

        Assert.False(canWrite);
        await trail.Received(1).AppendAsync(
            Arg.Is<AuditRecord>(r => r != null && r.EventType.Equals(AuditEventType.FieldWriteSequestered)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CanWriteFieldAsync_AuditEnabled_DoesNotEmitOnAuthorized()
    {
        var (svc, store, trail, _) = NewAuditEnabled();
        await store.RegisterAsync(NewRecord("r1#tenant_demographics", "sensor.BiometricAuth"));
        // Construct a profile that has BiometricAuth.
        var watch = Watch() with { SensorSurface = new() { SensorKind.Mic, SensorKind.Accelerometer, SensorKind.BiometricAuth } };
        await svc.ApplyMigrationAsync(Change(Phone(), watch));
        trail.ClearReceivedCalls();

        var canWrite = await svc.CanWriteFieldAsync(NodeId, "r1#tenant_demographics");

        Assert.True(canWrite);
        await trail.DidNotReceive().AppendAsync(
            Arg.Is<AuditRecord>(r => r != null && r.EventType.Equals(AuditEventType.FieldWriteSequestered)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ApplyMigrationAsync_AuditDisabled_DoesNotEmit()
    {
        var store = new InMemorySequestrationStore();
        var svc = new InMemoryFormFactorMigrationService(store);
        await store.RegisterAsync(NewRecord("r1", "sensor.Camera"));

        // Should complete without throwing — and no audit emitter wired.
        await svc.ApplyMigrationAsync(Change(Phone(), Watch()));
    }

    [Fact]
    public void Constructor_AuditEnabled_RequiresAllArgs()
    {
        var store = new InMemorySequestrationStore();
        var trail = Substitute.For<IAuditTrail>();
        var signer = new Ed25519Signer(KeyPair.Generate());

        Assert.Throws<ArgumentNullException>(() =>
            new InMemoryFormFactorMigrationService(store, null!, signer, TenantA));
        Assert.Throws<ArgumentNullException>(() =>
            new InMemoryFormFactorMigrationService(store, trail, null!, TenantA));
        Assert.Throws<ArgumentException>(() =>
            new InMemoryFormFactorMigrationService(store, trail, signer, default));
    }

    [Fact]
    public void AuditPayloads_HardwareTierChanged_ShapeIsAlphabetized()
    {
        var p = MigrationAuditPayloads.HardwareTierChanged(NodeId, FormFactorKind.Phone, FormFactorKind.Watch, TriggeringEventKind.ManualReprofile);
        Assert.Equal("Watch", p.Body["current_form_factor"]);
        Assert.Equal(NodeId, p.Body["node_id"]);
        Assert.Equal("Phone", p.Body["previous_form_factor"]);
        Assert.Equal("ManualReprofile", p.Body["triggering_event"]);
        Assert.Equal(4, p.Body.Count);
    }

    [Fact]
    public void AuditPayloads_Sequestered_ShapeIsAlphabetized()
    {
        var p = MigrationAuditPayloads.Sequestered(NodeId, "r1", "sensor.Camera", SequestrationFlagKind.PlaintextSequestered);
        Assert.Equal("PlaintextSequestered", p.Body["flag"]);
        Assert.Equal(NodeId, p.Body["node_id"]);
        Assert.Equal("r1", p.Body["record_id"]);
        Assert.Equal("sensor.Camera", p.Body["required_capability"]);
        Assert.Equal(4, p.Body.Count);
    }

    [Fact]
    public void AuditPayloads_DataReleased_ShapeIsAlphabetized()
    {
        var p = MigrationAuditPayloads.DataReleased(NodeId, "r1", "sensor.Camera");
        Assert.Equal(NodeId, p.Body["node_id"]);
        Assert.Equal("r1", p.Body["record_id"]);
        Assert.Equal("sensor.Camera", p.Body["required_capability"]);
        Assert.Equal(3, p.Body.Count);
    }

    [Fact]
    public void AuditPayloads_AdapterRollbackDetected_ShapeIsAlphabetized()
    {
        var p = MigrationAuditPayloads.AdapterRollbackDetected(NodeId, "formFactor.Watch", "Phone", "Watch");
        Assert.Equal("formFactor.Watch", p.Body["adapter_id"]);
        Assert.Equal("Watch", p.Body["current_version"]);
        Assert.Equal(NodeId, p.Body["node_id"]);
        Assert.Equal("Phone", p.Body["previous_version"]);
        Assert.Equal(4, p.Body.Count);
    }

    [Fact]
    public void AuditPayloads_FieldWriteSequestered_ShapeIsAlphabetized()
    {
        var p = MigrationAuditPayloads.FieldWriteSequestered(NodeId, "r1#field", "sensor.BiometricAuth");
        Assert.Equal("r1#field", p.Body["field_entry_id"]);
        Assert.Equal(NodeId, p.Body["node_id"]);
        Assert.Equal("sensor.BiometricAuth", p.Body["required_capability"]);
        Assert.Equal(3, p.Body.Count);
    }

    private sealed class FakeTimeProvider : TimeProvider
    {
        private DateTimeOffset _now;
        public FakeTimeProvider(DateTimeOffset now) => _now = now;
        public override DateTimeOffset GetUtcNow() => _now;
        public void Advance(TimeSpan delta) => _now = _now.Add(delta);
    }
}
