using System;

namespace Sunfish.Foundation.EngineRoom;

/// <summary>
/// Outcome of <see cref="IEngineRoomCommandService.ReleaseQuarantineAsync"/>
/// per ADR 0079 §2. Returned only when the release succeeded; permission
/// denial throws <see cref="EngineRoomUnauthorizedException"/>.
/// </summary>
/// <param name="DocumentId">Document that was released from quarantine.</param>
/// <param name="ReleasedAt">Wall-clock time the release was applied.</param>
public sealed record ReleaseResult(
    string DocumentId,
    DateTimeOffset ReleasedAt);
