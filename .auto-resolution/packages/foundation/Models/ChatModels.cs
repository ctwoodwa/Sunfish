namespace Sunfish.Foundation.Models;

/// <summary>
/// Represents a single message in a chat conversation.
/// </summary>
public class ChatMessage
{
    /// <summary>The text content of the message.</summary>
    public string Text { get; set; } = "";

    /// <summary>The display name of the message author.</summary>
    public string Author { get; set; } = "";

    /// <summary>The timestamp when the message was sent.</summary>
    public DateTime Timestamp { get; set; } = DateTime.Now;

    /// <summary>Whether this message was sent by the current user.</summary>
    public bool IsUser { get; set; }
}
