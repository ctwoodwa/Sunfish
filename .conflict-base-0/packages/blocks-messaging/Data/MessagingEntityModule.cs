using Microsoft.EntityFrameworkCore;
using Sunfish.Foundation.Persistence;

namespace Sunfish.Blocks.Messaging.Data;

/// <summary>
/// <see cref="ISunfishEntityModule"/> contribution for the messaging block.
/// Applies every <c>IEntityTypeConfiguration&lt;T&gt;</c> declared in this
/// assembly to the shared Bridge <c>DbContext</c> (per ADR 0015). Phase 2.1
/// has no <c>IEntityTypeConfiguration</c>s yet (in-memory only); the module
/// is registered now so durable backends ship by adding configs only.
/// </summary>
public sealed class MessagingEntityModule : ISunfishEntityModule
{
    /// <summary>The stable module key registered by this block.</summary>
    public const string Key = "sunfish.blocks.messaging";

    /// <inheritdoc />
    public string ModuleKey => Key;

    /// <inheritdoc />
    public void Configure(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(MessagingEntityModule).Assembly);
    }
}
