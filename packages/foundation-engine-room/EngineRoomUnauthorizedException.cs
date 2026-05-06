using System;

namespace Sunfish.Foundation.EngineRoom;

/// <summary>
/// Thrown by <see cref="IEngineRoomCommandService"/> implementations when
/// the caller's <c>IPermissionResolver.ResolveAsync</c> result is a
/// <c>PermissionDecision.Denied</c> for the requested action. Per ADR
/// 0079 §Trust: Damage Control operations (quarantine / release /
/// compact) are §Trust-elevated; the command service throws rather than
/// returning a structured outcome so caller side cannot accidentally
/// interpret a denial as success.
/// </summary>
/// <remarks>
/// Inherits from <see cref="UnauthorizedAccessException"/> so existing
/// authorization-pattern catch blocks (per ADR 0049 audit-by-construction)
/// pick it up automatically.
/// </remarks>
public sealed class EngineRoomUnauthorizedException : UnauthorizedAccessException
{
    /// <summary>Creates a new instance with the supplied <paramref name="message"/>.</summary>
    public EngineRoomUnauthorizedException(string message) : base(message) { }

    /// <summary>Creates a new instance with the supplied <paramref name="message"/> + inner exception.</summary>
    public EngineRoomUnauthorizedException(string message, Exception inner) : base(message, inner) { }
}
