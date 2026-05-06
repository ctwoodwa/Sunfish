using System;

namespace Sunfish.Foundation.EngineRoom;

/// <summary>
/// Outcome of <see cref="IEngineRoomCommandService.CompactDocumentAsync"/>
/// per ADR 0079 §2. Returned only when compaction succeeded; an
/// ineligible document throws <see cref="System.InvalidOperationException"/>
/// (NOT <see cref="EngineRoomUnauthorizedException"/> — eligibility is a
/// state check, not an authority check).
/// </summary>
/// <param name="DocumentId">Document that was compacted.</param>
/// <param name="BytesBefore">Approximate byte size before compaction.</param>
/// <param name="BytesAfter">Approximate byte size after compaction.</param>
/// <param name="CompletedAt">Wall-clock time compaction completed.</param>
public sealed record CompactionResult(
    string DocumentId,
    long BytesBefore,
    long BytesAfter,
    DateTimeOffset CompletedAt);
