using System.Reflection;
using Sunfish.Foundation.Enums;
using Sunfish.Providers.Bootstrap;
using Sunfish.Providers.FluentUI;
using Sunfish.Providers.Material;
using Sunfish.UICore.Contracts;
using Xunit;

namespace Sunfish.UICore.Tests;

/// <summary>
/// Verifies the ISunfishCssProvider interface shape.
/// These tests protect against accidental method deletions during the migration.
/// </summary>
public class CssProviderContractTests
{
    private static readonly Type ContractType = typeof(ISunfishCssProvider);

    [Fact]
    public void ISunfishCssProvider_HasExpectedMethodCount()
    {
        var methods = ContractType.GetMethods(BindingFlags.Public | BindingFlags.Instance);
        Assert.True(methods.Length >= 80, $"Expected at least 80 methods, got {methods.Length}");
    }

    [Fact]
    public void ISunfishCssProvider_HasButtonClass()
    {
        var method = ContractType.GetMethod("ButtonClass", [typeof(ButtonVariant), typeof(ButtonSize), typeof(bool), typeof(bool)]);
        Assert.NotNull(method);
        Assert.Equal(typeof(string), method.ReturnType);
    }

    [Fact]
    public void ISunfishCssProvider_HasDataGridClass()
    {
        var method = ContractType.GetMethod("DataGridClass");
        Assert.NotNull(method);
        Assert.Equal(typeof(string), method.ReturnType);
    }

    [Fact]
    public void ISunfishCssProvider_HasAllocationSchedulerMethods()
    {
        var method = ContractType.GetMethod("AllocationSchedulerClass");
        Assert.NotNull(method);
    }

    [Fact]
    public void ISunfishCssProvider_HasResizableContainerHandleClass_WithResizeEdgesParam()
    {
        // Verifies MariloResizeEdges was correctly renamed to ResizeEdges
        var method = ContractType.GetMethod("ResizableContainerHandleClass");
        Assert.NotNull(method);
        var param = method.GetParameters().FirstOrDefault(p => p.Name == "edge");
        Assert.NotNull(param);
        Assert.Equal(typeof(ResizeEdges), param.ParameterType);
    }

    // -------------------------------------------------------------------------
    // ADR 0024 — exhaustive ButtonVariant × ButtonSize × FillMode × Provider
    // parity test. Verifies every combination produces a non-empty CSS class
    // string on every first-party provider, so the Sunfish→Bootstrap/Fluent/
    // Material mapping can never silently fall through to an empty string
    // even if a non-exhaustive switch is ever reintroduced.
    //
    // Combinatorial count: 10 variants × 3 sizes × 5 fill-modes × 3 providers
    //                    = 450 assertions per test-case row.
    // -------------------------------------------------------------------------

    public static IEnumerable<object[]> CssProviders() =>
    [
        [new BootstrapCssProvider(), "Bootstrap"],
        [new FluentUICssProvider(),  "FluentUI"],
        [new MaterialCssProvider(),  "Material"],
    ];

    [Theory]
    [MemberData(nameof(CssProviders))]
    public void ButtonClass_Covers_All_ButtonVariant_Size_FillMode_Combinations(
        ISunfishCssProvider provider, string providerName)
    {
        foreach (var variant in Enum.GetValues<ButtonVariant>())
        foreach (var size in Enum.GetValues<ButtonSize>())
        foreach (var fill in Enum.GetValues<FillMode>())
        foreach (var rounded in new[] { RoundedMode.Medium }) // rounded axis sampled
        foreach (var disabled in new[] { false, true })
        {
            var cls = provider.ButtonClass(variant, size, fill, rounded, disabled);

            Assert.False(
                string.IsNullOrWhiteSpace(cls),
                $"{providerName}.ButtonClass({variant}, {size}, {fill}, {rounded}, disabled={disabled}) returned empty.");
        }
    }

    [Theory]
    [MemberData(nameof(CssProviders))]
    public void ButtonClass_LegacyOverload_Covers_All_ButtonVariant_Size_Outline_Combinations(
        ISunfishCssProvider provider, string providerName)
    {
        foreach (var variant in Enum.GetValues<ButtonVariant>())
        foreach (var size in Enum.GetValues<ButtonSize>())
        foreach (var isOutline in new[] { false, true })
        foreach (var disabled in new[] { false, true })
        {
            var cls = provider.ButtonClass(variant, size, isOutline, disabled);

            Assert.False(
                string.IsNullOrWhiteSpace(cls),
                $"{providerName}.ButtonClass({variant}, {size}, outline={isOutline}, disabled={disabled}) returned empty.");
        }
    }

    [Fact]
    public void ButtonVariant_Has_All_Ten_ADR0024_Values()
    {
        // Guard against an accidental rollback of ADR 0024 — Subtle, Transparent,
        // Light, Dark must remain on the enum for Fluent / BS5 / Material parity.
        var names = Enum.GetNames<ButtonVariant>().ToHashSet();
        Assert.Contains("Primary", names);
        Assert.Contains("Secondary", names);
        Assert.Contains("Danger", names);
        Assert.Contains("Warning", names);
        Assert.Contains("Info", names);
        Assert.Contains("Success", names);
        Assert.Contains("Subtle", names);
        Assert.Contains("Transparent", names);
        Assert.Contains("Light", names);
        Assert.Contains("Dark", names);
        Assert.Equal(10, names.Count);
    }

    [Theory]
    [MemberData(nameof(CssProviders))]
    public void ButtonClass_NewVariants_Emit_Distinct_Classes(
        ISunfishCssProvider provider, string providerName)
    {
        // ADR 0024 — the four new variants must produce class strings that
        // differ from the Primary baseline. This ensures providers have not
        // silently collapsed the new values via a `_ => "primary"` fallback.
        var baseline = provider.ButtonClass(
            ButtonVariant.Primary, ButtonSize.Medium, FillMode.Solid, RoundedMode.Medium, false);

        foreach (var variant in new[]
                 {
                     ButtonVariant.Subtle,
                     ButtonVariant.Transparent,
                     ButtonVariant.Light,
                     ButtonVariant.Dark,
                 })
        {
            var cls = provider.ButtonClass(
                variant, ButtonSize.Medium, FillMode.Solid, RoundedMode.Medium, false);

            Assert.NotEqual(baseline, cls);
            Assert.False(
                string.IsNullOrWhiteSpace(cls),
                $"{providerName}.ButtonClass({variant}) returned empty.");
        }
    }
}
