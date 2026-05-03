namespace Sunfish.Foundation.Integrations.Messaging;

/// <summary>Stable identifier for a thread in the messaging substrate (the <c>Thread</c> entity ships in <c>blocks-messaging</c> per W#20 Phase 2).</summary>
/// <param name="Value">Underlying GUID.</param>
public readonly record struct ThreadId(Guid Value)
{
    /// <summary>Returns the GUID's standard string representation.</summary>
    public override string ToString() => Value.ToString("D");
}

/// <summary>Stable identifier for a single message within a thread (the <c>Message</c> entity ships in <c>blocks-messaging</c> per W#20 Phase 2).</summary>
/// <param name="Value">Underlying GUID.</param>
public readonly record struct MessageId(Guid Value)
{
    /// <inheritdoc cref="ThreadId.ToString"/>
    public override string ToString() => Value.ToString("D");
}

/// <summary>Stable identifier for a <see cref="Participant"/> in the messaging substrate.</summary>
/// <param name="Value">Underlying GUID.</param>
public readonly record struct ParticipantId(Guid Value)
{
    /// <inheritdoc cref="ThreadId.ToString"/>
    public override string ToString() => Value.ToString("D");
}

/// <summary>
/// Opaque thread token round-tripped via inbound provider headers
/// (Email <c>Reply-To</c>, SMS reserved-token line) so replies route back to
/// the originating thread. 34-char base32 + <c>"."</c> + epoch per ADR 0052
/// amendment A2.
/// </summary>
/// <param name="Value">Encoded token string.</param>
public readonly record struct ThreadToken(string Value)
{
    /// <summary>Returns the encoded token string.</summary>
    public override string ToString() => Value;
}
