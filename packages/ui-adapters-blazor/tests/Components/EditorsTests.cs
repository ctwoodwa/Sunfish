using Sunfish.UIAdapters.Blazor.Components.Editors;
using Xunit;

namespace Sunfish.UIAdapters.Blazor.Tests.Components;

// Smoke test for the Editors category migration. SunfishEditor wires two internal
// JS-module interop services (IElementMeasurementService, IResizeObserverService)
// via [Inject] that require Castle DynamicProxy access to internals — stubbing them
// would require a second InternalsVisibleTo for the proxy assembly plus a real
// JSRuntime harness. For Phase 3b smoke-test scope, the type-existence + namespace
// check below is sufficient to confirm the migration produced a compilable, loadable
// component type. Deeper behavior is validated in Phase 7 (kitchen-sink) and visual
// parity tests.
public class EditorsTests
{
    [Fact]
    public void SunfishEditor_TypeIsPublicAndInNamespace()
    {
        var type = typeof(SunfishEditor);
        Assert.True(type.IsPublic);
        Assert.Equal("Sunfish.UIAdapters.Blazor.Components.Editors", type.Namespace);
    }

    [Fact]
    public void EditorCustomTool_TypeExists()
    {
        var type = typeof(EditorCustomTool);
        Assert.Equal("Sunfish.UIAdapters.Blazor.Components.Editors", type.Namespace);
    }
}
