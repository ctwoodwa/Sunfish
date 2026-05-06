using System.Threading;
using System.Threading.Tasks;

namespace Sunfish.UICore.Primitives;

/// <summary>
/// Focus-trap primitive per ADR 0077 §4 + WCAG 2.2 SC 2.4.3 (Focus
/// Order) + SC 2.1.2 (No Keyboard Trap). Adapter implementations
/// trap focus within a modal/dialog boundary on
/// <see cref="EnterAsync"/> and release on <see cref="ExitAsync"/>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Symmetric pair contract:</b> every successful
/// <see cref="EnterAsync"/> MUST be paired with exactly one
/// <see cref="ExitAsync"/>. Re-entry without exit is a renderer-side
/// programming error; implementations SHOULD log + ignore the second
/// enter rather than nest.
/// </para>
/// <para>
/// <b>Keyboard-reachable</b> per WCAG SC 2.1.2: implementations MUST
/// provide a keyboard escape route (typically <c>Escape</c>) that
/// invokes <see cref="ExitAsync"/> — focus traps without an escape
/// fail the W#46 a11y CI gate.
/// </para>
/// </remarks>
public interface IFocusTrap
{
    /// <summary>Enter the focus-trap region; cancellation aborts the enter.</summary>
    ValueTask EnterAsync(CancellationToken ct = default);

    /// <summary>Exit the focus-trap region; restores prior focus per SC 2.4.3.</summary>
    ValueTask ExitAsync(CancellationToken ct = default);
}
