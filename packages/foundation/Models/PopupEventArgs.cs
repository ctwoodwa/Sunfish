namespace Sunfish.Foundation.Models;

/// <summary>
/// Event arguments for popup lifecycle events (OnOpen, OnClose).
/// Set <see cref="IsCancelled"/> to <c>true</c> in the handler to prevent
/// the popup from opening or closing.
/// </summary>
public class PopupEventArgs
{
    /// <summary>
    /// Set to <c>true</c> to cancel the popup transition.
    /// When cancelled during OnOpen, the popup remains closed.
    /// When cancelled during OnClose, the popup remains open.
    /// </summary>
    public bool IsCancelled { get; set; }
}
