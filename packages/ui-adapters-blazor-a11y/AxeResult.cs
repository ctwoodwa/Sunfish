using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Sunfish.UIAdapters.Blazor.A11y;

/// <summary>
/// Strongly-typed projection of the JSON object returned by <c>axe.run(document, options)</c>.
/// Shape matches axe-core 4.x API surface documented at
/// https://github.com/dequelabs/axe-core/blob/develop/doc/API.md#results-object.
/// </summary>
/// <remarks>
/// Only the fields Sunfish's a11y harness consumes are typed; additional axe fields are
/// ignored via default-on-deserialization behaviour. Callers asserting "no moderate+
/// violations" filter <see cref="Violations"/> by <see cref="AxeResultItem.Impact"/>.
/// </remarks>
public sealed class AxeResult
{
    [JsonPropertyName("violations")]
    public IReadOnlyList<AxeResultItem> Violations { get; init; } = new List<AxeResultItem>();

    [JsonPropertyName("passes")]
    public IReadOnlyList<AxeResultItem> Passes { get; init; } = new List<AxeResultItem>();

    [JsonPropertyName("incomplete")]
    public IReadOnlyList<AxeResultItem> Incomplete { get; init; } = new List<AxeResultItem>();

    [JsonPropertyName("inapplicable")]
    public IReadOnlyList<AxeResultItem> Inapplicable { get; init; } = new List<AxeResultItem>();
}

public sealed class AxeResultItem
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    [JsonPropertyName("impact")]
    public AxeImpact? Impact { get; init; }

    [JsonPropertyName("description")]
    public string Description { get; init; } = string.Empty;

    [JsonPropertyName("help")]
    public string Help { get; init; } = string.Empty;

    [JsonPropertyName("helpUrl")]
    public string HelpUrl { get; init; } = string.Empty;

    [JsonPropertyName("tags")]
    public IReadOnlyList<string> Tags { get; init; } = new List<string>();

    [JsonPropertyName("nodes")]
    public IReadOnlyList<AxeNode> Nodes { get; init; } = new List<AxeNode>();
}

public sealed class AxeNode
{
    [JsonPropertyName("target")]
    public IReadOnlyList<string> Target { get; init; } = new List<string>();

    [JsonPropertyName("html")]
    public string Html { get; init; } = string.Empty;

    [JsonPropertyName("failureSummary")]
    public string? FailureSummary { get; init; }
}

/// <summary>
/// axe-core impact levels, ordered so <c>Moderate</c> and higher can be filtered with
/// <c>Impact.Value &gt;= AxeImpact.Moderate</c>. JSON strings are mapped case-insensitively.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum AxeImpact
{
    Minor = 0,
    Moderate = 1,
    Serious = 2,
    Critical = 3,
}
