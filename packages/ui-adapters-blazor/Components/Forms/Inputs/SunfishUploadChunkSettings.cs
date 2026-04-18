using Microsoft.AspNetCore.Components;

namespace Sunfish.Components.Blazor.Components.Forms.Inputs;

/// <summary>
/// Internal cascade sink used by <see cref="SunfishUpload"/> to receive chunk-settings
/// registrations from the non-generic <see cref="SunfishUploadChunkSettings"/> child component.
/// The interface decouples the child from any type parameters on the parent.
/// </summary>
internal interface IUploadChunkSettingsSink
{
    void RegisterChunkSettings(SunfishUploadChunkSettings settings);
    void UnregisterChunkSettings(SunfishUploadChunkSettings settings);
}

/// <summary>
/// Declarative child component for <see cref="SunfishUpload"/> that configures chunk-upload
/// behaviour via a nested tag API. All parameters are nullable; a null value falls through
/// to the parent's flat parameter or its default.
/// </summary>
/// <example>
/// <code>
/// &lt;SunfishUpload SaveUrl="/api/upload"&gt;
///     &lt;UploadChunkSettings ChunkSize="524288"
///                           AutoRetryAfter="2000"
///                           MaxAutoRetries="3"
///                           MetadataField="fileMetadata"
///                           Resumable="true" /&gt;
/// &lt;/SunfishUpload&gt;
/// </code>
/// </example>
public class SunfishUploadChunkSettings : ComponentBase, IDisposable
{
    [CascadingParameter] internal IUploadChunkSettingsSink? ParentSink { get; set; }

    /// <summary>
    /// Overrides the parent <see cref="SunfishUpload.ChunkSize"/> when set.
    /// Null means fall through to the parent parameter.
    /// </summary>
    [Parameter] public long? ChunkSize { get; set; }

    /// <summary>
    /// Milliseconds to wait before automatically retrying a failed chunk.
    /// Null means no auto-retry (consumer must call RetryFile manually).
    /// </summary>
    [Parameter] public int? AutoRetryAfter { get; set; }

    /// <summary>
    /// Maximum number of automatic chunk-level retries before the file is marked Failed.
    /// Ignored when <see cref="AutoRetryAfter"/> is null.
    /// </summary>
    [Parameter] public int? MaxAutoRetries { get; set; }

    /// <summary>
    /// Custom form-data field name appended to every chunk request for server-side metadata.
    /// When null, no extra field is sent.
    /// </summary>
    [Parameter] public string? MetadataField { get; set; }

    /// <summary>
    /// When true, the upload tracks bytes sent so that a paused/cancelled upload can be
    /// resumed from where it left off. When false, resume always restarts from byte 0.
    /// Defaults to the parent's existing behaviour (true).
    /// </summary>
    [Parameter] public bool? Resumable { get; set; }

    protected override void OnInitialized()
    {
        ParentSink?.RegisterChunkSettings(this);
    }

    public void Dispose()
    {
        ParentSink?.UnregisterChunkSettings(this);
    }
}
