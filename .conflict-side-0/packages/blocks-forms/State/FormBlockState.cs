namespace Sunfish.Blocks.Forms.State;

/// <summary>
/// Tracks submission state for a <see cref="FormBlock{TModel}"/> instance.
/// Updated by the block; exposed to consumers via OnStateChanged callback.
/// </summary>
public sealed class FormBlockState
{
    public bool IsSubmitting { get; internal set; }
    public bool HasSubmitted { get; internal set; }
    public bool LastSubmitWasValid { get; internal set; }
    public DateTime? LastSubmitAttemptUtc { get; internal set; }
}
