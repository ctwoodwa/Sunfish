using System;

namespace Sunfish.Foundation.Channels;

/// <summary>
/// Flags-combined capability set for a crew-comms channel. Per ADR 0076.
/// </summary>
[Flags]
public enum ChannelCapability : byte
{
    /// <summary>No channel capability advertised.</summary>
    None = 0,

    /// <summary>Bidirectional plain-text messaging (Phase 1 of ADR 0076).</summary>
    Text = 1 << 0,

    /// <summary>Push-to-talk audio (Phase 3 of ADR 0076; Opus-encoded frames).</summary>
    Audio = 1 << 1,

    /// <summary>Video (Phase 4 of ADR 0076).</summary>
    Video = 1 << 2,
}
