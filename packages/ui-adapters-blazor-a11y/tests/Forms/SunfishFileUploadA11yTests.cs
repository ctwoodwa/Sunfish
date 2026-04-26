using System.Threading.Tasks;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using NSubstitute;
using Sunfish.Foundation.Services;
using Sunfish.UICore.Contracts;
using Xunit;

namespace Sunfish.UIAdapters.Blazor.A11y.Tests.Forms;

/// <summary>
/// Wave 4 cascade extension — bUnit-axe a11y harness for SunfishFileUpload.
/// Currently skipped: SunfishFileUpload [Inject]s the internal IDropZoneService and
/// HttpClient; the internal interop service can't be substituted from this test
/// assembly without an InternalsVisibleTo entry. Tracked for follow-up.
/// </summary>
public class SunfishFileUploadA11yTests
{
    [Fact(Skip = "Requires complex fixture - tracked: SunfishFileUpload injects internal IDropZoneService + HttpClient + IJSRuntime")]
    public Task SunfishFileUpload_DefaultRender_HasNoModeratePlusAxeViolations() => Task.CompletedTask;
}
