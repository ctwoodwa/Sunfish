using Sunfish.Foundation.Enums;

namespace Sunfish.Foundation.Models;

public class BreakpointChangedEventArgs : EventArgs
{
    public required Breakpoint OldBreakpoint { get; init; }
    public required Breakpoint NewBreakpoint { get; init; }
}
