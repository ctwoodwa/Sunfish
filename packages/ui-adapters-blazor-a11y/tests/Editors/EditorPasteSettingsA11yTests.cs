using System.Threading.Tasks;
using Xunit;

namespace Sunfish.UIAdapters.Blazor.A11y.Tests.Editors;

/// <summary>
/// EditorPasteSettings is a configuration child of SunfishEditor; it has no
/// isolated DOM. Coverage lands on the SunfishEditor parent test.
/// </summary>
public class EditorPasteSettingsA11yTests
{
    [Fact(Skip = "Definition-only - configures parent SunfishEditor, no isolated DOM")]
    public Task EditorPasteSettings_HasNoIsolatedDom() => Task.CompletedTask;
}
