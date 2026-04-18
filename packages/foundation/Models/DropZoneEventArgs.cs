namespace Sunfish.Foundation.Models;

public class DropZoneEventArgs : EventArgs
{
    public string[] FileNames { get; set; } = [];
    public long[] FileSizes { get; set; } = [];
}
