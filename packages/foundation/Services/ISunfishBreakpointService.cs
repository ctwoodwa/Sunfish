using Sunfish.Foundation.Enums;
using Sunfish.Foundation.Models;

namespace Sunfish.Foundation.Services;

public interface ISunfishBreakpointService
{
    Breakpoint Current { get; }
    event EventHandler<BreakpointChangedEventArgs>? BreakpointChanged;
    Task InitializeAsync();
}
