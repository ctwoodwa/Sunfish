using System.Text.Json.Serialization;

namespace Sunfish.Foundation.Wayfinder;

/// <summary>
/// A single search hit produced by <see cref="IAtlasProjector.SearchAsync"/>.
/// Per ADR 0065 §5.
/// </summary>
/// <param name="Path">Dotted path of the matching setting.</param>
/// <param name="DisplayName">Display name of the matching setting (drawn from <see cref="AtlasSchemaDescriptor.DisplayName"/>).</param>
/// <param name="MatchSnippet">Matched portion of the search query, with surrounding context for display.</param>
/// <param name="Score">Relevance score in <c>[0.0, 1.0]</c>; higher is more relevant. Hits are streamed in descending order.</param>
public sealed record AtlasSearchHit(
    [property: JsonPropertyName("path")] string Path,
    [property: JsonPropertyName("displayName")] string DisplayName,
    [property: JsonPropertyName("matchSnippet")] string MatchSnippet,
    [property: JsonPropertyName("score")] double Score);
