using System;
using System.Text.Json;
using Sunfish.Foundation.Crypto;
using Sunfish.Foundation.Versioning;
using Xunit;

namespace Sunfish.Foundation.Migration.Tests;

public sealed class FormFactorProfileTests
{
    private static FormFactorProfile NewLaptopProfile() => new()
    {
        FormFactor = FormFactorKind.Laptop,
        InputModalities = new() { InputModalityKind.Pointer, InputModalityKind.Keyboard, InputModalityKind.Touch },
        DisplayClass = DisplayClassKind.Large,
        NetworkPosture = NetworkPostureKind.IntermittentConnected,
        StorageBudgetMb = 256_000,
        PowerProfile = PowerProfileKind.Battery,
        SensorSurface = new() { SensorKind.Camera, SensorKind.Mic },
        InstanceClass = InstanceClassKind.SelfHost,
    };

    [Fact]
    public void FormFactorProfile_RoundTripsThroughCanonicalJson()
    {
        // HashSet<T> uses reference equality inside records, so we
        // compare the canonical-JSON byte stream of the original vs the
        // re-serialized round-trip — that's the canonical-stability
        // property the wire format actually guarantees.
        var profile = NewLaptopProfile();
        var bytes = CanonicalJson.Serialize(profile);
        var json = System.Text.Encoding.UTF8.GetString(bytes);

        var deserialized = JsonSerializer.Deserialize<FormFactorProfile>(json);
        Assert.NotNull(deserialized);
        var reSerialized = CanonicalJson.Serialize(deserialized);
        Assert.Equal(bytes, reSerialized);
    }

    [Fact]
    public void FormFactorProfile_SerializesEnumLiterals_NotOrdinals()
    {
        var profile = NewLaptopProfile();
        var json = System.Text.Encoding.UTF8.GetString(CanonicalJson.Serialize(profile));

        Assert.Contains("\"Laptop\"", json);
        Assert.Contains("\"Large\"", json);
        Assert.Contains("\"IntermittentConnected\"", json);
        Assert.Contains("\"Battery\"", json);
        Assert.Contains("\"SelfHost\"", json);
        // Enum-ordinal regression guard.
        Assert.DoesNotContain("\"formFactor\":0", json);
    }

    [Fact]
    public void FormFactorProfile_UsesCamelCasePropertyNames()
    {
        var profile = NewLaptopProfile();
        var json = System.Text.Encoding.UTF8.GetString(CanonicalJson.Serialize(profile));

        Assert.Contains("\"formFactor\"", json);
        Assert.Contains("\"inputModalities\"", json);
        Assert.Contains("\"displayClass\"", json);
        Assert.Contains("\"networkPosture\"", json);
        Assert.Contains("\"storageBudgetMb\"", json);
        Assert.Contains("\"powerProfile\"", json);
        Assert.Contains("\"sensorSurface\"", json);
        Assert.Contains("\"instanceClass\"", json);

        // PascalCase regression guard.
        Assert.DoesNotContain("\"FormFactor\"", json);
        Assert.DoesNotContain("\"StorageBudgetMb\"", json);
    }

    [Fact]
    public void FormFactorProfile_RoundTripsWatchProfile_PostA8_4()
    {
        // Phone↔Watch is a post-A8.4 expanded migration table entry —
        // pin it here so the surface compiles for that combo.
        var watch = new FormFactorProfile
        {
            FormFactor = FormFactorKind.Watch,
            InputModalities = new() { InputModalityKind.Touch, InputModalityKind.Voice },
            DisplayClass = DisplayClassKind.MicroDisplay,
            NetworkPosture = NetworkPostureKind.IntermittentConnected,
            StorageBudgetMb = 4_096,
            PowerProfile = PowerProfileKind.LowPower,
            SensorSurface = new() { SensorKind.Accelerometer, SensorKind.BiometricAuth },
            InstanceClass = InstanceClassKind.SelfHost,
        };

        var bytes = CanonicalJson.Serialize(watch);
        var roundtripped = JsonSerializer.Deserialize<FormFactorProfile>(System.Text.Encoding.UTF8.GetString(bytes));
        Assert.NotNull(roundtripped);
        Assert.Equal(bytes, CanonicalJson.Serialize(roundtripped));
    }

    [Fact]
    public void FormFactorProfile_RoundTripsVehicleProfile_PostA8_4_CarPlayCase()
    {
        var vehicle = new FormFactorProfile
        {
            FormFactor = FormFactorKind.Vehicle,
            InputModalities = new() { InputModalityKind.Touch, InputModalityKind.Voice },
            DisplayClass = DisplayClassKind.Medium,
            NetworkPosture = NetworkPostureKind.IntermittentConnected,
            StorageBudgetMb = 16_384,
            PowerProfile = PowerProfileKind.Wallpower,
            SensorSurface = new() { SensorKind.Gps, SensorKind.Mic, SensorKind.Camera },
            InstanceClass = InstanceClassKind.ManagedBridge,
        };
        var bytes = CanonicalJson.Serialize(vehicle);
        var roundtripped = JsonSerializer.Deserialize<FormFactorProfile>(System.Text.Encoding.UTF8.GetString(bytes));
        Assert.NotNull(roundtripped);
        Assert.Equal(bytes, CanonicalJson.Serialize(roundtripped));
    }

    [Fact]
    public void HardwareTierChangeEvent_RoundTripsThroughCanonicalJson()
    {
        var prior = NewLaptopProfile();
        var current = prior with { StorageBudgetMb = prior.StorageBudgetMb / 2 };
        var ev = new HardwareTierChangeEvent
        {
            NodeId = "node-A",
            PreviousProfile = prior,
            CurrentProfile = current,
            TriggeringEvent = TriggeringEventKind.StorageBudgetChanged,
            DetectedAt = new DateTimeOffset(2026, 5, 1, 12, 0, 0, TimeSpan.Zero),
        };

        var bytes = CanonicalJson.Serialize(ev);
        var roundtripped = JsonSerializer.Deserialize<HardwareTierChangeEvent>(System.Text.Encoding.UTF8.GetString(bytes));
        Assert.NotNull(roundtripped);
        Assert.Equal(bytes, CanonicalJson.Serialize(roundtripped));
    }

    [Fact]
    public void HardwareTierChangeEvent_SerializesTriggeringEventAsLiteral()
    {
        var prior = NewLaptopProfile();
        var ev = new HardwareTierChangeEvent
        {
            NodeId = "node-B",
            PreviousProfile = prior,
            CurrentProfile = prior with { NetworkPosture = NetworkPostureKind.OfflineFirst },
            TriggeringEvent = TriggeringEventKind.NetworkPostureChanged,
            DetectedAt = DateTimeOffset.UtcNow,
        };
        var json = System.Text.Encoding.UTF8.GetString(CanonicalJson.Serialize(ev));
        Assert.Contains("\"NetworkPostureChanged\"", json);
        Assert.Contains("\"triggeringEvent\"", json);
    }

    [Theory]
    [InlineData(FormFactorKind.Laptop)]
    [InlineData(FormFactorKind.Desktop)]
    [InlineData(FormFactorKind.Tablet)]
    [InlineData(FormFactorKind.Phone)]
    [InlineData(FormFactorKind.Watch)]
    [InlineData(FormFactorKind.Headless)]
    [InlineData(FormFactorKind.Iot)]
    [InlineData(FormFactorKind.Vehicle)]
    public void FormFactorKind_AllEightValuesRoundTrip(FormFactorKind kind)
    {
        var profile = NewLaptopProfile() with { FormFactor = kind };
        var json = System.Text.Encoding.UTF8.GetString(CanonicalJson.Serialize(profile));
        var roundtripped = JsonSerializer.Deserialize<FormFactorProfile>(json);
        Assert.NotNull(roundtripped);
        Assert.Equal(kind, roundtripped!.FormFactor);
        Assert.Contains($"\"{kind}\"", json); // literal-name on the wire
    }

    [Fact]
    public void SequestrationFlagKind_AllFiveValuesRoundTrip()
    {
        // Lightweight pin: every SequestrationFlagKind value parses
        // through Enum.TryParse so future serialization (P3 sequestration
        // store) can rely on the literal-name surface.
        foreach (var flag in Enum.GetValues<SequestrationFlagKind>())
        {
            Assert.True(Enum.TryParse<SequestrationFlagKind>(flag.ToString(), out var parsed));
            Assert.Equal(flag, parsed);
        }
    }
}
