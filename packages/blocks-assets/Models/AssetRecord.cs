namespace Sunfish.Blocks.Assets.Models;

public sealed record AssetRecord
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string Path { get; init; }
    public long SizeBytes { get; init; }
    public DateTime? LastModifiedUtc { get; init; }
}
