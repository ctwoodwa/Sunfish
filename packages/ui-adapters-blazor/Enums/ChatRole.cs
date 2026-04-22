namespace Sunfish.UIAdapters.Blazor.Enums;

/// <summary>
/// Identifies the author of a <see cref="Components.AI.ChatMessage"/> rendered by
/// <see cref="Components.AI.SunfishChat"/>. Mirrors the standard LLM role taxonomy
/// (user / assistant / system) so message lists round-trip cleanly with
/// Microsoft.Extensions.AI <c>ChatMessage</c> records and similar upstream APIs.
/// </summary>
public enum ChatRole
{
    /// <summary>Message sent by the local end-user.</summary>
    User,

    /// <summary>Message returned by the AI assistant / bot / model.</summary>
    Assistant,

    /// <summary>System-level instruction or metadata message (e.g. prompt).</summary>
    System
}
