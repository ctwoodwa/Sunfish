using Xunit;

namespace Sunfish.Compat.Syncfusion.Tests;

/// <summary>
/// Namespace + assembly invariants per POLICY §2. These lock the compat surface so future
/// refactors don't accidentally break consumer migration paths.
/// </summary>
public class NamespaceAndAssemblyTests
{
    [Fact]
    public void Assembly_NameMatchesExpected()
    {
        var asm = typeof(Sunfish.Compat.Syncfusion.SfButton).Assembly;
        Assert.Equal("Sunfish.Compat.Syncfusion", asm.GetName().Name);
    }

    [Fact]
    public void Assembly_DoesNotReferenceSyncfusion()
    {
        var asm = typeof(Sunfish.Compat.Syncfusion.SfButton).Assembly;
        foreach (var refAsm in asm.GetReferencedAssemblies())
        {
            Assert.False(
                refAsm.Name != null && refAsm.Name.StartsWith("Syncfusion.", System.StringComparison.OrdinalIgnoreCase),
                $"compat-syncfusion MUST NOT reference any Syncfusion assembly, but referenced: {refAsm.Name}");
        }
    }

    [Theory]
    [InlineData(typeof(Sunfish.Compat.Syncfusion.SfButton))]
    [InlineData(typeof(Sunfish.Compat.Syncfusion.SfIcon))]
    [InlineData(typeof(Sunfish.Compat.Syncfusion.SfTextBox))]
    [InlineData(typeof(Sunfish.Compat.Syncfusion.SfDataForm))]
    [InlineData(typeof(Sunfish.Compat.Syncfusion.SfDialog))]
    [InlineData(typeof(Sunfish.Compat.Syncfusion.SfTooltip))]
    [InlineData(typeof(Sunfish.Compat.Syncfusion.SfToast))]
    [InlineData(typeof(Sunfish.Compat.Syncfusion.GridColumns<>))]
    [InlineData(typeof(Sunfish.Compat.Syncfusion.DialogTemplates))]
    [InlineData(typeof(Sunfish.Compat.Syncfusion.DialogButtons))]
    [InlineData(typeof(Sunfish.Compat.Syncfusion.DialogButton))]
    [InlineData(typeof(Sunfish.Compat.Syncfusion.DropDownListFieldSettings))]
    public void Wrappers_LiveInRootNamespace(System.Type type)
    {
        Assert.True(type.IsPublic, $"{type.Name} must be public.");
        Assert.Equal("Sunfish.Compat.Syncfusion", type.Namespace);
    }
}
