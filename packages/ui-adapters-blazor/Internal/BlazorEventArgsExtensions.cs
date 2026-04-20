using Microsoft.AspNetCore.Components.Web;
using Sunfish.Foundation.Models;

namespace Sunfish.UIAdapters.Blazor.Internal;

/// <summary>
/// Converts Blazor event args types into their framework-agnostic Sunfish equivalents.
/// Foundation defines the agnostic shapes (e.g., <see cref="SunfishMouseEventArgs"/>);
/// Blazor adapter code calls these extensions at the boundary where Blazor events
/// leak into framework-neutral event handlers.
/// </summary>
public static class BlazorEventArgsExtensions
{
    public static SunfishMouseEventArgs ToSunfish(this MouseEventArgs args) => new()
    {
        ClientX = args.ClientX,
        ClientY = args.ClientY,
        ScreenX = args.ScreenX,
        ScreenY = args.ScreenY,
        OffsetX = args.OffsetX,
        OffsetY = args.OffsetY,
        AltKey = args.AltKey,
        CtrlKey = args.CtrlKey,
        ShiftKey = args.ShiftKey,
        MetaKey = args.MetaKey,
        Button = (int)args.Button,
    };
}
