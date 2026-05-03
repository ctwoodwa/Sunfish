namespace Sunfish.UIAdapters.Blazor.Components.LocalFirst;

/// <summary>
/// State machine for <see cref="SunfishOptimisticButton"/> (paper §5.2 optimistic-write button).
/// </summary>
/// <remarks>
/// Transitions:
/// <c>Idle → Pending</c> (on click) →
/// <c>Confirmed</c> (success; auto-reverts to <c>Idle</c> after ~2s) or
/// <c>Failed</c> (exception / predicate returned <c>false</c>; clickable to retry → <c>Pending</c>).
/// </remarks>
public enum OptimisticButtonState
{
    /// <summary>Ready to accept a click.</summary>
    Idle,

    /// <summary>An in-flight write is awaiting confirmation.</summary>
    Pending,

    /// <summary>The write completed successfully (shows a checkmark then reverts).</summary>
    Confirmed,

    /// <summary>The write failed or returned <c>false</c> (clickable to retry).</summary>
    Failed,
}
