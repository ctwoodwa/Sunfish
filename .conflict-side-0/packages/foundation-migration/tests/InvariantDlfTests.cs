using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Sunfish.Foundation.Versioning;
using Xunit;

namespace Sunfish.Foundation.Migration.Tests;

public sealed class InvariantDlfTests
{
    private const string NodeId = "node-A";

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
        SensorSurface = new() { SensorKind.Mic, SensorKind.Accelerometer, SensorKind.BiometricAuth },
        InstanceClass = InstanceClassKind.SelfHost,
    };

    private static HardwareTierChangeEvent Change(FormFactorProfile prev, FormFactorProfile curr,
        TriggeringEventKind trigger = TriggeringEventKind.ManualReprofile) => new()
    {
        NodeId = NodeId,
        PreviousProfile = prev,
        CurrentProfile = curr,
        TriggeringEvent = trigger,
        DetectedAt = new DateTimeOffset(2026, 5, 1, 12, 0, 0, TimeSpan.Zero),
    };

    private static SequesteredRecord NewRecord(string recordId, string capability,
        bool isEncrypted = false, bool isPkEncrypted = false, bool isCp = false) => new()
    {
        NodeId = NodeId,
        RecordId = recordId,
        RequiredCapability = capability,
        IsEncrypted = isEncrypted,
        IsPrimaryKeyEncrypted = isPkEncrypted,
        IsCpClass = isCp,
    };

    // === A5.4 Rule 1: sequestration over deletion ===

    [Fact]
    public async Task ApplyMigrationAsync_SequestersRecordWhenCapabilityLost()
    {
        var store = new InMemorySequestrationStore();
        var service = new InMemoryFormFactorMigrationService(store);
        await store.RegisterAsync(NewRecord("r1", "sensor.Camera"));

        // Phone → Watch: Camera capability disappears.
        await service.ApplyMigrationAsync(Change(Phone(), Watch()));

        var sequestered = await store.GetSequesteredAsync(NodeId);
        Assert.Single(sequestered);
        Assert.Equal("r1", sequestered[0].RecordId);
        Assert.NotNull(sequestered[0].Flag);
    }

    [Fact]
    public async Task ApplyMigrationAsync_LeavesRecordActive_WhenCapabilityRetained()
    {
        var store = new InMemorySequestrationStore();
        var service = new InMemoryFormFactorMigrationService(store);
        await store.RegisterAsync(NewRecord("r1", "sensor.Mic"));

        // Phone → Watch: Mic capability is retained on both.
        await service.ApplyMigrationAsync(Change(Phone(), Watch()));

        var sequestered = await store.GetSequesteredAsync(NodeId);
        Assert.Empty(sequestered);
    }

    // === A5.4 Rule 2: re-emergence on surface expansion ===

    [Fact]
    public async Task ApplyMigrationAsync_ReleasesRecord_WhenCapabilityRestored()
    {
        var store = new InMemorySequestrationStore();
        var service = new InMemoryFormFactorMigrationService(store);
        await store.RegisterAsync(NewRecord("r1", "sensor.Camera"));

        // Sequester first (Phone → Watch).
        await service.ApplyMigrationAsync(Change(Phone(), Watch()));
        Assert.Single(await store.GetSequesteredAsync(NodeId));

        // Then restore the surface (Watch → Phone).
        await service.ApplyMigrationAsync(Change(Watch(), Phone()));

        Assert.Empty(await store.GetSequesteredAsync(NodeId));
        var entries = await store.GetByNodeAsync(NodeId);
        Assert.Null(entries[0].Flag);
    }

    // === A8.3 Rule 5: plaintext vs ciphertext distinction ===

    [Fact]
    public async Task ApplyMigrationAsync_PlaintextRecord_GetsPlaintextSequesteredFlag()
    {
        var store = new InMemorySequestrationStore();
        var service = new InMemoryFormFactorMigrationService(store);
        await store.RegisterAsync(NewRecord("r1", "sensor.Camera", isEncrypted: false));

        await service.ApplyMigrationAsync(Change(Phone(), Watch()));

        var sequestered = await store.GetSequesteredAsync(NodeId);
        Assert.Equal(SequestrationFlagKind.PlaintextSequestered, sequestered[0].Flag);
    }

    [Fact]
    public async Task ApplyMigrationAsync_EncryptedRecord_GetsCiphertextSequesteredFlag()
    {
        var store = new InMemorySequestrationStore();
        var service = new InMemoryFormFactorMigrationService(store);
        await store.RegisterAsync(NewRecord("r1", "sensor.Camera", isEncrypted: true));

        await service.ApplyMigrationAsync(Change(Phone(), Watch()));

        var sequestered = await store.GetSequesteredAsync(NodeId);
        Assert.Equal(SequestrationFlagKind.CiphertextSequestered, sequestered[0].Flag);
    }

    // === StorageBudget trigger overrides flag classification ===

    [Fact]
    public async Task ApplyMigrationAsync_StorageBudgetTrigger_GetsStorageBudgetExceededFlag()
    {
        var store = new InMemorySequestrationStore();
        var service = new InMemoryFormFactorMigrationService(store);
        await store.RegisterAsync(NewRecord("r1", "sensor.Camera"));

        // Same profile delta but the trigger is StorageBudgetChanged
        // — flag MUST be StorageBudgetExceeded regardless of encryption.
        await service.ApplyMigrationAsync(Change(Phone(), Watch(), TriggeringEventKind.StorageBudgetChanged));

        var sequestered = await store.GetSequesteredAsync(NodeId);
        Assert.Equal(SequestrationFlagKind.StorageBudgetExceeded, sequestered[0].Flag);
    }

    // === A8.3 Rule 6: CP-record quorum participation ===

    [Fact]
    public async Task ApplyMigrationAsync_CpRecord_GetsFormFactorQuorumIneligibleFlag()
    {
        var store = new InMemorySequestrationStore();
        var service = new InMemoryFormFactorMigrationService(store);
        await store.RegisterAsync(NewRecord("cp1", "sensor.Camera", isCp: true));

        await service.ApplyMigrationAsync(Change(Phone(), Watch()));

        var sequestered = await store.GetSequesteredAsync(NodeId);
        Assert.Equal(SequestrationFlagKind.FormFactorQuorumIneligible, sequestered[0].Flag);
    }

    [Fact]
    public async Task IsQuorumEligibleAsync_ActiveCpRecord_ReturnsTrue()
    {
        var store = new InMemorySequestrationStore();
        var service = new InMemoryFormFactorMigrationService(store);
        await store.RegisterAsync(NewRecord("cp1", "sensor.Mic", isCp: true));

        // Mic is on both Phone + Watch — record stays active.
        await service.ApplyMigrationAsync(Change(Phone(), Watch()));

        Assert.True(await service.IsQuorumEligibleAsync(NodeId, "cp1"));
    }

    [Fact]
    public async Task IsQuorumEligibleAsync_SequesteredCpRecord_ReturnsFalse()
    {
        var store = new InMemorySequestrationStore();
        var service = new InMemoryFormFactorMigrationService(store);
        await store.RegisterAsync(NewRecord("cp1", "sensor.Camera", isCp: true));
        await service.ApplyMigrationAsync(Change(Phone(), Watch()));

        Assert.False(await service.IsQuorumEligibleAsync(NodeId, "cp1"));
    }

    [Fact]
    public async Task IsQuorumEligibleAsync_NonCpRecord_AlwaysReturnsTrue()
    {
        var store = new InMemorySequestrationStore();
        var service = new InMemoryFormFactorMigrationService(store);
        await store.RegisterAsync(NewRecord("r1", "sensor.Camera", isCp: false));
        await service.ApplyMigrationAsync(Change(Phone(), Watch()));

        // Sequestered, but not a CP record → quorum eligibility is N/A
        // (vacuously true; quorum is only meaningful on CP records).
        Assert.True(await service.IsQuorumEligibleAsync(NodeId, "r1"));
    }

    [Fact]
    public async Task IsQuorumEligibleAsync_UnknownRecord_VacuouslyTrue()
    {
        var store = new InMemorySequestrationStore();
        var service = new InMemoryFormFactorMigrationService(store);

        Assert.True(await service.IsQuorumEligibleAsync(NodeId, "never-registered"));
    }

    // === A8.5 Rule 6: field-level write authorization ===

    [Fact]
    public async Task CanWriteFieldAsync_ActiveField_ReturnsTrue()
    {
        var store = new InMemorySequestrationStore();
        var service = new InMemoryFormFactorMigrationService(store);
        await store.RegisterAsync(NewRecord("r1#tenant_demographics", "sensor.BiometricAuth"));

        // Watch profile has BiometricAuth → field is active.
        await service.ApplyMigrationAsync(Change(Phone(), Watch()));

        Assert.True(await service.CanWriteFieldAsync(NodeId, "r1#tenant_demographics"));
    }

    [Fact]
    public async Task CanWriteFieldAsync_SequesteredField_ReturnsFalse()
    {
        var store = new InMemorySequestrationStore();
        var service = new InMemoryFormFactorMigrationService(store);
        await store.RegisterAsync(NewRecord("r1#tenant_demographics", "sensor.BiometricAuth"));

        // Phone (no BiometricAuth) → field is sequestered.
        await service.ApplyMigrationAsync(Change(Watch(), Phone()));

        Assert.False(await service.CanWriteFieldAsync(NodeId, "r1#tenant_demographics"));
    }

    [Fact]
    public async Task CanWriteFieldAsync_UnknownField_VacuouslyTrue()
    {
        var store = new InMemorySequestrationStore();
        var service = new InMemoryFormFactorMigrationService(store);

        Assert.True(await service.CanWriteFieldAsync(NodeId, "never-registered#field"));
    }

    // === Cross-peer rescue (A5.4 rule 3) — can't fully exercise without
    //     a federation peer; verify the substrate doesn't delete data.

    [Fact]
    public async Task ApplyMigrationAsync_NeverDeletesRecord_OnlyToggles()
    {
        var store = new InMemorySequestrationStore();
        var service = new InMemoryFormFactorMigrationService(store);
        await store.RegisterAsync(NewRecord("r1", "sensor.Camera"));
        var before = await store.GetByNodeAsync(NodeId);
        Assert.Single(before);

        // Sequester
        await service.ApplyMigrationAsync(Change(Phone(), Watch()));
        var afterSequester = await store.GetByNodeAsync(NodeId);
        Assert.Single(afterSequester);
        Assert.NotNull(afterSequester[0].Flag);

        // Release
        await service.ApplyMigrationAsync(Change(Watch(), Phone()));
        var afterRelease = await store.GetByNodeAsync(NodeId);
        Assert.Single(afterRelease);
        Assert.Null(afterRelease[0].Flag);
    }

    // === Mixed corpus: simultaneous sequester + release transitions ===

    [Fact]
    public async Task ApplyMigrationAsync_MixedCorpus_AppliesPerRecordIndependently()
    {
        var store = new InMemorySequestrationStore();
        var service = new InMemoryFormFactorMigrationService(store);

        // r1 starts active (Mic available on both); r2 starts active
        // but Camera is lost on Watch; r3 starts sequestered with
        // BiometricAuth that becomes available on Watch.
        await store.RegisterAsync(NewRecord("r1", "sensor.Mic"));
        await store.RegisterAsync(NewRecord("r2", "sensor.Camera"));
        await store.RegisterAsync(NewRecord("r3", "sensor.BiometricAuth") with { Flag = SequestrationFlagKind.PlaintextSequestered });

        await service.ApplyMigrationAsync(Change(Phone(), Watch()));

        var entries = (await store.GetByNodeAsync(NodeId)).ToDictionary(e => e.RecordId);
        Assert.Null(entries["r1"].Flag);                                      // stayed active
        Assert.NotNull(entries["r2"].Flag);                                   // newly sequestered
        Assert.Null(entries["r3"].Flag);                                      // released
    }

    // === Per-record IsPrimaryKeyEncrypted flag pin (A8.3 rule 7 surface) ===

    [Fact]
    public async Task SequesteredRecord_PreservesIsPrimaryKeyEncryptedFlag_AcrossSequester()
    {
        var store = new InMemorySequestrationStore();
        var service = new InMemoryFormFactorMigrationService(store);
        await store.RegisterAsync(NewRecord("r1", "sensor.Camera", isEncrypted: true, isPkEncrypted: true));

        await service.ApplyMigrationAsync(Change(Phone(), Watch()));

        var entries = await store.GetByNodeAsync(NodeId);
        Assert.True(entries[0].IsPrimaryKeyEncrypted);
        Assert.True(entries[0].IsEncrypted);
        // Substrate consumers (apps/docs UX) read this flag to decide
        // record-level vs field-level redaction per A8.3 rule 7.
    }
}
