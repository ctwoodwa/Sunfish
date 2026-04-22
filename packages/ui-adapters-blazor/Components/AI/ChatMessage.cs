using System;
using Sunfish.UIAdapters.Blazor.Enums;

namespace Sunfish.UIAdapters.Blazor.Components.AI;

/// <summary>
/// Spec-aligned chat message rendered by <see cref="SunfishChat"/>. Distinct from
/// the legacy <c>Sunfish.Foundation.Models.ChatMessage</c> (which is used by the
/// older <c>Sunfish.UIAdapters.Blazor.Components.Feedback.SunfishChat</c> surface)
/// and models conversations by role / content / timestamp instead of author string.
/// </summary>
/// <param name="Role">The author role (user / assistant / system).</param>
/// <param name="Content">The textual content of the message.</param>
/// <param name="Timestamp">When the message was created. Defaults to "now" via the factory.</param>
public sealed record ChatMessage(ChatRole Role, string Content, DateTimeOffset Timestamp)
{
    /// <summary>Create a <see cref="ChatMessage"/> stamped with <see cref="DateTimeOffset.Now"/>.</summary>
    public static ChatMessage Create(ChatRole role, string content)
        => new(role, content, DateTimeOffset.Now);
}
