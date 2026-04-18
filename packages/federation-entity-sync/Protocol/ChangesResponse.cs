namespace Sunfish.Federation.EntitySync.Protocol;

/// <summary>
/// Payload for <see cref="Sunfish.Federation.Common.SyncMessageKind.EntityChangesResponse"/>. Carries
/// the signed change records the responder believes the requester is missing. An empty list is a
/// valid ack (nothing to transfer).
/// </summary>
/// <param name="Changes">The signed change records being delivered, in DTO form for JSON transport.</param>
public sealed record ChangesResponse(
    IReadOnlyList<SignedChangeRecordDto> Changes);
