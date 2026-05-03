using Xunit;

namespace Sunfish.Compat.Infragistics.Tests;

/// <summary>
/// Locks the core assembly / namespace invariants for compat-infragistics:
/// - Assembly name matches the Sunfish.Compat.* naming convention.
/// - All wrapper types live in the root namespace (per POLICY Hard Invariant #2).
/// - Wrappers are public and instantiable where expected.
/// </summary>
public class AssemblyAndNamespaceTests
{
    [Fact]
    public void AssemblyName_MatchesPackageId()
    {
        var asm = typeof(Sunfish.Compat.Infragistics.IgbButton).Assembly;
        Assert.Equal("Sunfish.Compat.Infragistics", asm.GetName().Name);
    }

    [Theory]
    [InlineData(typeof(Sunfish.Compat.Infragistics.IgbButton))]
    [InlineData(typeof(Sunfish.Compat.Infragistics.IgbIcon))]
    [InlineData(typeof(Sunfish.Compat.Infragistics.IgbCheckbox))]
    [InlineData(typeof(Sunfish.Compat.Infragistics.IgbInput))]
    [InlineData(typeof(Sunfish.Compat.Infragistics.IgbTooltip))]
    [InlineData(typeof(Sunfish.Compat.Infragistics.IgbToast))]
    [InlineData(typeof(Sunfish.Compat.Infragistics.IgbDatePicker))]
    [InlineData(typeof(Sunfish.Compat.Infragistics.IgbDialog))]
    public void NonGenericWrappers_AreInRootNamespaceAndPublic(System.Type t)
    {
        Assert.True(t.IsPublic);
        Assert.Equal("Sunfish.Compat.Infragistics", t.Namespace);
    }

    [Theory]
    [InlineData(typeof(Sunfish.Compat.Infragistics.IgbSelect<>))]
    [InlineData(typeof(Sunfish.Compat.Infragistics.IgbSelectItem<>))]
    [InlineData(typeof(Sunfish.Compat.Infragistics.IgbCombo<,>))]
    [InlineData(typeof(Sunfish.Compat.Infragistics.IgbGrid<>))]
    [InlineData(typeof(Sunfish.Compat.Infragistics.IgbColumn<>))]
    public void GenericWrappers_AreInRootNamespaceAndPublic(System.Type t)
    {
        Assert.True(t.IsPublic);
        Assert.True(t.IsGenericTypeDefinition);
        Assert.Equal("Sunfish.Compat.Infragistics", t.Namespace);
    }

    [Fact]
    public void NoInfragisticsNuGetReference_InAssemblyReferences()
    {
        // Policy Hard Invariant #1: no IgniteUI.* / Infragistics.* references.
        var referenced = typeof(Sunfish.Compat.Infragistics.IgbButton).Assembly.GetReferencedAssemblies();
        foreach (var r in referenced)
        {
            var name = r.Name ?? string.Empty;
            Assert.False(
                name.StartsWith("IgniteUI.", System.StringComparison.OrdinalIgnoreCase),
                $"compat-infragistics must not reference an IgniteUI.* assembly, but found: {name}");
            Assert.False(
                name.StartsWith("Infragistics.", System.StringComparison.OrdinalIgnoreCase),
                $"compat-infragistics must not reference an Infragistics.* assembly, but found: {name}");
        }
    }
}
