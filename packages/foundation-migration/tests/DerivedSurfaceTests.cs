using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Sunfish.Foundation.Versioning;
using Xunit;

namespace Sunfish.Foundation.Migration.Tests;

public sealed class DerivedSurfaceTests
{
    private static readonly InMemoryFormFactorMigrationService Service = new();

    private static FormFactorProfile NewProfile(
        FormFactorKind kind = FormFactorKind.Laptop,
        DisplayClassKind display = DisplayClassKind.Large,
        NetworkPostureKind network = NetworkPostureKind.IntermittentConnected,
        PowerProfileKind power = PowerProfileKind.Battery,
        uint storageMb = 256_000,
        IReadOnlySet<InputModalityKind>? input = null,
        IReadOnlySet<SensorKind>? sensors = null,
        InstanceClassKind instance = InstanceClassKind.SelfHost) =>
        new()
        {
            FormFactor = kind,
            InputModalities = new HashSet<InputModalityKind>(input ?? new HashSet<InputModalityKind> { InputModalityKind.Pointer, InputModalityKind.Keyboard }),
            DisplayClass = display,
            NetworkPosture = network,
            StorageBudgetMb = storageMb,
            PowerProfile = power,
            SensorSurface = new HashSet<SensorKind>(sensors ?? new HashSet<SensorKind>()),
            InstanceClass = instance,
        };

    [Fact]
    public void DeriveHostCapabilities_LaptopProfile_HasFormFactorTag()
    {
        var caps = InMemoryFormFactorMigrationService.DeriveHostCapabilities(NewProfile());
        Assert.Contains("formFactor.Laptop", caps);
        Assert.Contains("display.Large", caps);
        Assert.Contains("network.IntermittentConnected", caps);
        Assert.Contains("power.Battery", caps);
        Assert.Contains("instanceClass.SelfHost", caps);
        Assert.Contains("input.Pointer", caps);
        Assert.Contains("input.Keyboard", caps);
        Assert.Contains("storage.budgetMb=256000", caps);
    }

    [Fact]
    public void DeriveHostCapabilities_WatchProfile_PostA8_4_HasMicroDisplayAndAccelerometer()
    {
        var watch = NewProfile(
            kind: FormFactorKind.Watch,
            display: DisplayClassKind.MicroDisplay,
            power: PowerProfileKind.LowPower,
            input: new HashSet<InputModalityKind> { InputModalityKind.Touch, InputModalityKind.Voice },
            sensors: new HashSet<SensorKind> { SensorKind.Accelerometer, SensorKind.BiometricAuth });
        var caps = InMemoryFormFactorMigrationService.DeriveHostCapabilities(watch);

        Assert.Contains("formFactor.Watch", caps);
        Assert.Contains("display.MicroDisplay", caps);
        Assert.Contains("power.LowPower", caps);
        Assert.Contains("input.Touch", caps);
        Assert.Contains("input.Voice", caps);
        Assert.Contains("sensor.Accelerometer", caps);
        Assert.Contains("sensor.BiometricAuth", caps);
    }

    [Fact]
    public async Task ComputeDerivedSurfaceAsync_AllRequirementsSupported_AllIncluded()
    {
        var profile = NewProfile(input: new HashSet<InputModalityKind> { InputModalityKind.Pointer, InputModalityKind.Keyboard });
        IReadOnlySet<string> declared = new HashSet<string> { "input.Pointer", "input.Keyboard", "display.Large" };

        var surface = await Service.ComputeDerivedSurfaceAsync(profile, declared);

        Assert.Equal(FormFactorKind.Laptop, surface.FormFactor);
        Assert.Equal(3, surface.IncludedCapabilities.Count);
        Assert.Empty(surface.ExcludedCapabilities);
    }

    [Fact]
    public async Task ComputeDerivedSurfaceAsync_MissingCapability_LandsInExcluded()
    {
        // Workspace declares it needs a barcode scanner, but a desktop
        // doesn't have one — that capability gets excluded.
        var desktop = NewProfile(kind: FormFactorKind.Desktop, sensors: new HashSet<SensorKind>());
        IReadOnlySet<string> declared = new HashSet<string> { "input.Keyboard", "sensor.BarcodeScanner" };

        var surface = await Service.ComputeDerivedSurfaceAsync(desktop, declared);

        Assert.Contains("input.Keyboard", surface.IncludedCapabilities);
        Assert.Contains("sensor.BarcodeScanner", surface.ExcludedCapabilities);
        Assert.Single(surface.IncludedCapabilities);
        Assert.Single(surface.ExcludedCapabilities);
    }

    [Fact]
    public async Task ComputeDerivedSurfaceAsync_EmptyDeclaration_BothSetsEmpty()
    {
        var surface = await Service.ComputeDerivedSurfaceAsync(NewProfile(), new HashSet<string>());

        Assert.Empty(surface.IncludedCapabilities);
        Assert.Empty(surface.ExcludedCapabilities);
    }

    [Fact]
    public async Task ComputeDerivedSurfaceAsync_NullProfile_Throws()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            await Service.ComputeDerivedSurfaceAsync(null!, new HashSet<string>()));
    }

    [Fact]
    public async Task ComputeDerivedSurfaceAsync_NullDeclaration_Throws()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            await Service.ComputeDerivedSurfaceAsync(NewProfile(), null!));
    }

    [Fact]
    public async Task ComputeDerivedSurfaceAsync_HonorsCancellation()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
            await Service.ComputeDerivedSurfaceAsync(NewProfile(), new HashSet<string>(), cts.Token));
    }

    [Fact]
    public async Task ComputeDerivedSurfaceAsync_PreservesOriginalDeclaration()
    {
        IReadOnlySet<string> declared = new HashSet<string> { "input.Keyboard", "sensor.Camera" };
        var surface = await Service.ComputeDerivedSurfaceAsync(NewProfile(), declared);

        Assert.Equal(2, surface.WorkspaceDeclaredCapabilities.Count);
        Assert.Contains("input.Keyboard", surface.WorkspaceDeclaredCapabilities);
        Assert.Contains("sensor.Camera", surface.WorkspaceDeclaredCapabilities);
    }

    // === Migration-table coverage: 8 form-factor combos per A5.1 + A8.4 ===

    [Theory]
    [InlineData(FormFactorKind.Laptop)]
    [InlineData(FormFactorKind.Desktop)]
    [InlineData(FormFactorKind.Tablet)]
    [InlineData(FormFactorKind.Phone)]
    [InlineData(FormFactorKind.Watch)]
    [InlineData(FormFactorKind.Headless)]
    [InlineData(FormFactorKind.Iot)]
    [InlineData(FormFactorKind.Vehicle)]
    public async Task ComputeDerivedSurfaceAsync_AllEightFormFactors_ProduceMatchingFormFactorTag(FormFactorKind kind)
    {
        var profile = NewProfile(kind: kind);
        IReadOnlySet<string> declared = new HashSet<string> { $"formFactor.{kind}" };

        var surface = await Service.ComputeDerivedSurfaceAsync(profile, declared);

        Assert.Equal(kind, surface.FormFactor);
        Assert.Contains($"formFactor.{kind}", surface.IncludedCapabilities);
        Assert.Empty(surface.ExcludedCapabilities);
    }

    [Fact]
    public async Task ComputeDerivedSurfaceAsync_PhoneToWatchTransition_PostA8_4_ShrinksSensorSurface()
    {
        // Migration-table pin per A8.4: a workspace that wants {Mic, Camera, GPS, Accelerometer}
        // is fully supported on a Phone but loses Camera + GPS on a Watch (typical post-A8.4
        // hardware-tier delta). The Invariant DLF guarantee in P3 ensures the lost capabilities'
        // records get sequestered, not deleted.
        IReadOnlySet<string> declared = new HashSet<string>
        {
            "sensor.Mic", "sensor.Camera", "sensor.Gps", "sensor.Accelerometer",
        };

        var phone = NewProfile(
            kind: FormFactorKind.Phone,
            sensors: new HashSet<SensorKind> { SensorKind.Mic, SensorKind.Camera, SensorKind.Gps, SensorKind.Accelerometer });
        var phoneSurface = await Service.ComputeDerivedSurfaceAsync(phone, declared);
        Assert.Equal(4, phoneSurface.IncludedCapabilities.Count);
        Assert.Empty(phoneSurface.ExcludedCapabilities);

        var watch = NewProfile(
            kind: FormFactorKind.Watch,
            sensors: new HashSet<SensorKind> { SensorKind.Mic, SensorKind.Accelerometer });
        var watchSurface = await Service.ComputeDerivedSurfaceAsync(watch, declared);
        Assert.Equal(2, watchSurface.IncludedCapabilities.Count);
        Assert.Contains("sensor.Mic", watchSurface.IncludedCapabilities);
        Assert.Contains("sensor.Accelerometer", watchSurface.IncludedCapabilities);
        Assert.Equal(2, watchSurface.ExcludedCapabilities.Count);
        Assert.Contains("sensor.Camera", watchSurface.ExcludedCapabilities);
        Assert.Contains("sensor.Gps", watchSurface.ExcludedCapabilities);
    }

    [Fact]
    public async Task ComputeDerivedSurfaceAsync_VehicleCarPlayCase_PostA8_4_HasMediumDisplayPlusVoicePlusGps()
    {
        // CarPlay / Android-Auto pattern per A8.4: medium display, voice
        // input dominant, GPS sensor present, wallpower, intermittent
        // connected. The migration table places this distinct from
        // Phone/Tablet (no Camera by default; touch + voice combo).
        IReadOnlySet<string> declared = new HashSet<string>
        {
            "input.Voice", "input.Touch", "display.Medium", "sensor.Gps", "power.Wallpower",
        };
        var vehicle = NewProfile(
            kind: FormFactorKind.Vehicle,
            display: DisplayClassKind.Medium,
            network: NetworkPostureKind.IntermittentConnected,
            power: PowerProfileKind.Wallpower,
            input: new HashSet<InputModalityKind> { InputModalityKind.Touch, InputModalityKind.Voice },
            sensors: new HashSet<SensorKind> { SensorKind.Gps });

        var surface = await Service.ComputeDerivedSurfaceAsync(vehicle, declared);

        Assert.Equal(5, surface.IncludedCapabilities.Count);
        Assert.Empty(surface.ExcludedCapabilities);
    }

    [Fact]
    public async Task ApplyMigrationAsync_NotImplemented_ThrowsUntilPhase3()
    {
        var profile = NewProfile();
        var change = new HardwareTierChangeEvent
        {
            NodeId = "node-A",
            PreviousProfile = profile,
            CurrentProfile = profile,
            TriggeringEvent = TriggeringEventKind.ManualReprofile,
            DetectedAt = DateTimeOffset.UtcNow,
        };
        await Assert.ThrowsAsync<NotImplementedException>(async () =>
            await Service.ApplyMigrationAsync(change));
    }
}
