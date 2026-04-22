using System;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Xunit;

namespace Sunfish.Compat.Syncfusion.Tests;

/// <summary>
/// Locks: each grid/dialog child shim throws <see cref="InvalidOperationException"/> when
/// rendered outside its expected parent. The shared base
/// <c>CompatChildComponent&lt;TParent&gt;.RequireParent()</c> provides the behavior.
/// </summary>
public class ChildComponentParentTests
{
    [Fact]
    public async System.Threading.Tasks.Task GridPageSettings_ThrowsOutsideSfGrid()
    {
        await AssertThrowsOutsideParent(() => new Sunfish.Compat.Syncfusion.GridPageSettings<object>());
    }

    [Fact]
    public async System.Threading.Tasks.Task GridSelectionSettings_ThrowsOutsideSfGrid()
    {
        await AssertThrowsOutsideParent(() => new Sunfish.Compat.Syncfusion.GridSelectionSettings<object>());
    }

    [Fact]
    public async System.Threading.Tasks.Task GridFilterSettings_ThrowsOutsideSfGrid()
    {
        await AssertThrowsOutsideParent(() => new Sunfish.Compat.Syncfusion.GridFilterSettings<object>());
    }

    [Fact]
    public async System.Threading.Tasks.Task GridSortSettings_ThrowsOutsideSfGrid()
    {
        await AssertThrowsOutsideParent(() => new Sunfish.Compat.Syncfusion.GridSortSettings<object>());
    }

    [Fact]
    public async System.Threading.Tasks.Task GridEditSettings_ThrowsOutsideSfGrid()
    {
        await AssertThrowsOutsideParent(() => new Sunfish.Compat.Syncfusion.GridEditSettings<object>());
    }

    [Fact]
    public async System.Threading.Tasks.Task GridEvents_ThrowsOutsideSfGrid()
    {
        await AssertThrowsOutsideParent(() => new Sunfish.Compat.Syncfusion.GridEvents<object>());
    }

    [Fact]
    public async System.Threading.Tasks.Task DialogTemplates_ThrowsOutsideSfDialog()
    {
        await AssertThrowsOutsideParent(() => new Sunfish.Compat.Syncfusion.DialogTemplates());
    }

    [Fact]
    public async System.Threading.Tasks.Task DialogButtons_ThrowsOutsideSfDialog()
    {
        await AssertThrowsOutsideParent(() => new Sunfish.Compat.Syncfusion.DialogButtons());
    }

    private static async System.Threading.Tasks.Task AssertThrowsOutsideParent(Func<ComponentBase> factory)
    {
        var component = factory();
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await component.SetParametersAsync(ParameterView.Empty);
        });
        Assert.Contains("must be used inside", ex.Message);
    }
}
