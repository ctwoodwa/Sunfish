using System.Threading.Tasks;
using Xunit;

namespace Sunfish.UIAdapters.Blazor.A11y.Tests.Feedback.SignalR;

/// <summary>
/// SignalRConnectionPopup + HubConnectionRow are popup chrome / list rows used by
/// SunfishSignalRConnectionStatus; deferred together with the parent fixture.
/// </summary>
public class SignalRConnectionPopupA11yTests
{
    [Fact(Skip = "Requires complex fixture - tracked: depends on parent SignalR fixture")]
    public Task SignalRConnectionPopup_HasNoIsolatedDom() => Task.CompletedTask;
}
