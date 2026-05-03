using System.Text.Json;
using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Foundation.Assets.Entities;

/// <summary>
/// A single pending entity creation, bundling schema, body, and options for use with
/// <see cref="IEntityStore.CreateBatchAsync"/>.
/// </summary>
/// <param name="Schema">The schema the new entity must conform to.</param>
/// <param name="Body">The initial JSON body.</param>
/// <param name="Options">Creation options (nonce, issuer, tenant, etc.).</param>
public sealed record EntityDraft(
    SchemaId Schema,
    JsonDocument Body,
    CreateOptions Options);
