using System.Threading.Tasks;
using Xunit;

namespace Sunfish.UIAdapters.Blazor.A11y.Tests.Forms;

/// <summary>
/// Wave 4 cascade extension — bUnit-axe a11y harness for SunfishUpload.
/// Currently skipped: SunfishUpload [Inject]s the internal IDropZoneService and
/// HttpClient; the internal interop service can't be substituted from this test
/// assembly without an InternalsVisibleTo entry. Tracked for follow-up.
/// </summary>
public class SunfishUploadA11yTests
{
    [Fact(Skip = "Requires complex fixture - tracked: SunfishUpload injects internal IDropZoneService + HttpClient + IJSRuntime")]
    public Task SunfishUpload_DefaultRender_HasNoModeratePlusAxeViolations() => Task.CompletedTask;
}
