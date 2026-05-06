using System;

namespace Sunfish.Foundation.EngineRoom;

/// <summary>
/// Outcome of <see cref="IEngineRoomCommandService.QuarantineDocumentAsync"/>
/// per ADR 0079 §2. Returned only when the operation succeeded;
/// permission denial throws <see cref="EngineRoomUnauthorizedException"/>.
/// </summary>
/// <param name="DocumentId">Document that was quarantined.</param>
/// <param name="QuarantinedAt">Wall-clock time the quarantine was applied.</param>
public sealed record QuarantineResult(
    string DocumentId,
    DateTimeOffset QuarantinedAt);
