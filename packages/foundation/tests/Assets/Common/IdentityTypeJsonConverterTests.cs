using System.Text.Json;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Blobs;

namespace Sunfish.Foundation.Tests.Assets.Common;

/// <summary>
/// Round-trip tests verifying that identity-wrapper types serialize as flat strings
/// rather than nested objects. G36 Item 1.
/// </summary>
public sealed class IdentityTypeJsonConverterTests
{
    // ──────────────────────────── ActorId ────────────────────────────

    [Fact]
    public void ActorId_SerializesAsFlatString()
    {
        var id = new ActorId("alice");
        var json = JsonSerializer.Serialize(id);
        Assert.Equal("\"alice\"", json);
    }

    [Fact]
    public void ActorId_DeserializesFromFlatString()
    {
        var id = JsonSerializer.Deserialize<ActorId>("\"alice\"");
        Assert.Equal(new ActorId("alice"), id);
    }

    [Fact]
    public void ActorId_RoundTrips()
    {
        var original = new ActorId("service-account@org");
        var json = JsonSerializer.Serialize(original);
        var restored = JsonSerializer.Deserialize<ActorId>(json);
        Assert.Equal(original, restored);
    }

    // ──────────────────────────── SchemaId ────────────────────────────

    [Fact]
    public void SchemaId_SerializesAsFlatString()
    {
        var id = new SchemaId("property.v1");
        var json = JsonSerializer.Serialize(id);
        Assert.Equal("\"property.v1\"", json);
    }

    [Fact]
    public void SchemaId_DeserializesFromFlatString()
    {
        var id = JsonSerializer.Deserialize<SchemaId>("\"lease.v2\"");
        Assert.Equal(new SchemaId("lease.v2"), id);
    }

    [Fact]
    public void SchemaId_RoundTrips()
    {
        var original = new SchemaId("my.schema.v3");
        var json = JsonSerializer.Serialize(original);
        var restored = JsonSerializer.Deserialize<SchemaId>(json);
        Assert.Equal(original, restored);
    }

    // ──────────────────────────── TenantId ────────────────────────────

    [Fact]
    public void TenantId_SerializesAsFlatString()
    {
        var id = new TenantId("acme-rentals");
        var json = JsonSerializer.Serialize(id);
        Assert.Equal("\"acme-rentals\"", json);
    }

    [Fact]
    public void TenantId_DeserializesFromFlatString()
    {
        var id = JsonSerializer.Deserialize<TenantId>("\"default\"");
        Assert.Equal(TenantId.Default, id);
    }

    [Fact]
    public void TenantId_RoundTrips()
    {
        var original = new TenantId("tenant-xyz");
        var json = JsonSerializer.Serialize(original);
        var restored = JsonSerializer.Deserialize<TenantId>(json);
        Assert.Equal(original, restored);
    }

    // ──────────────────────────── EntityId ────────────────────────────

    [Fact]
    public void EntityId_SerializesAsFlatString()
    {
        var id = new EntityId("property", "acme-rentals", "42");
        var json = JsonSerializer.Serialize(id);
        Assert.Equal("\"property:acme-rentals/42\"", json);
    }

    [Fact]
    public void EntityId_DeserializesFromFlatString()
    {
        var id = JsonSerializer.Deserialize<EntityId>("\"property:acme-rentals/42\"");
        Assert.Equal(new EntityId("property", "acme-rentals", "42"), id);
    }

    [Fact]
    public void EntityId_RoundTrips()
    {
        var original = new EntityId("lease", "sunfish", "99");
        var json = JsonSerializer.Serialize(original);
        var restored = JsonSerializer.Deserialize<EntityId>(json);
        Assert.Equal(original, restored);
    }

    [Fact]
    public void EntityId_Deserialize_Throws_OnMalformedString()
    {
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<EntityId>("\"not-valid\""));
    }

    // ──────────────────────────── VersionId ────────────────────────────

    [Fact]
    public void VersionId_SerializesAsFlatStringWithFullHash()
    {
        var entity = new EntityId("property", "acme", "42");
        var id = new VersionId(entity, 3, "abcdef1234567890");
        var json = JsonSerializer.Serialize(id);
        Assert.Equal("\"property:acme/42@3:abcdef1234567890\"", json);
    }

    [Fact]
    public void VersionId_DeserializesFromFlatString()
    {
        var id = JsonSerializer.Deserialize<VersionId>("\"property:acme/42@3:abcdef1234567890\"");
        var expected = new VersionId(new EntityId("property", "acme", "42"), 3, "abcdef1234567890");
        Assert.Equal(expected, id);
    }

    [Fact]
    public void VersionId_RoundTrips_WithFullHash()
    {
        var entity = new EntityId("lease", "sunfish", "7");
        var original = new VersionId(entity, 1, "sha256-" + new string('a', 64));
        var json = JsonSerializer.Serialize(original);
        var restored = JsonSerializer.Deserialize<VersionId>(json);
        Assert.Equal(original, restored);
        // Verify full hash is preserved (not truncated like ToString())
        Assert.Equal(original.Hash, restored.Hash);
    }

    // ──────────────────────────── Instant ────────────────────────────

    [Fact]
    public void Instant_SerializesAsIso8601FlatString()
    {
        var dto = new DateTimeOffset(2024, 6, 15, 12, 0, 0, TimeSpan.Zero);
        var instant = new Instant(dto);
        var json = JsonSerializer.Serialize(instant);
        // Should be a JSON string, not an object
        Assert.StartsWith("\"", json);
        Assert.EndsWith("\"", json);
        Assert.DoesNotContain("Value", json);
    }

    [Fact]
    public void Instant_RoundTrips()
    {
        var dto = new DateTimeOffset(2024, 6, 15, 12, 30, 45, 123, TimeSpan.Zero);
        var original = new Instant(dto);
        var json = JsonSerializer.Serialize(original);
        var restored = JsonSerializer.Deserialize<Instant>(json);
        Assert.Equal(original.Value, restored.Value);
    }

    [Fact]
    public void Instant_Deserialize_Throws_OnNonIso8601String()
    {
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<Instant>("\"not-a-date\""));
    }

    // ──────────────────────────── Cid ────────────────────────────

    [Fact]
    public void Cid_SerializesAsFlatString()
    {
        var cid = Cid.FromBytes("hello"u8);
        var json = JsonSerializer.Serialize(cid);
        // Should be a JSON string starting with 'b' (base32-lowercase multibase prefix)
        Assert.StartsWith("\"b", json);
        Assert.DoesNotContain("Value", json);
    }

    [Fact]
    public void Cid_RoundTrips()
    {
        var original = Cid.FromBytes("round-trip test"u8);
        var json = JsonSerializer.Serialize(original);
        var restored = JsonSerializer.Deserialize<Cid>(json);
        Assert.Equal(original, restored);
    }

    [Fact]
    public void Cid_Deserialize_Throws_OnMalformedString()
    {
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<Cid>("\"not-a-cid\""));
    }
}
