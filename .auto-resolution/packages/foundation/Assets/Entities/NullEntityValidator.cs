using System.Text.Json;
using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Foundation.Assets.Entities;

/// <summary>Null-object validator that accepts every body. Phase A default.</summary>
public sealed class NullEntityValidator : IEntityValidator
{
    /// <summary>Singleton instance.</summary>
    public static NullEntityValidator Instance { get; } = new();

    /// <inheritdoc />
    public Task ValidateAsync(SchemaId schema, JsonDocument body, CancellationToken ct = default)
        => Task.CompletedTask;
}
