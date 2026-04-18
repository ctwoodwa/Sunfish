namespace Sunfish.Foundation.Models;

/// <summary>
/// Represents a pre-existing file entry displayed in SunfishFileUpload.
/// </summary>
public class FileUploadInfo
{
    public string Name { get; set; } = string.Empty;
    public long Size { get; set; }
    public string? Extension { get; set; }
}
