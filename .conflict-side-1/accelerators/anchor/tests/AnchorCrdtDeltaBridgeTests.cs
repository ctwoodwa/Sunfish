using Microsoft.Extensions.Logging.Abstractions;

using Sunfish.Anchor.Services;
using Sunfish.Kernel.Crdt;
using Sunfish.Kernel.Crdt.Backends;

namespace Sunfish.Anchor.Tests;

/// <summary>
/// Phase 1 G2 — coverage for <see cref="AnchorCrdtDeltaBridge"/>, the wiring
/// between <c>kernel-sync</c>'s DELTA_STREAM callbacks and Anchor's local
/// <see cref="ICrdtDocument"/>. The wire-protocol round-trip is exercised by
/// <c>packages/kernel-sync/tests/TwoNodeDeltaStreamTests.cs</c>; these tests
/// only verify the bridge's encode/apply delegation + error policy.
/// </summary>
public sealed class AnchorCrdtDeltaBridgeTests
{
    [Fact]
    public async Task EncodeOutboundDelta_returns_documents_delta_bytes()
    {
        var engine = new StubCrdtEngine();
        await using var doc = engine.CreateDocument("default");
        doc.GetText("greeting").Insert(0, "hello");

        var bridge = new AnchorCrdtDeltaBridge(doc, NullLogger<AnchorCrdtDeltaBridge>.Instance);

        var bytes = await bridge.EncodeOutboundDeltaAsync(
            "default",
            ReadOnlyMemory<byte>.Empty,
            CancellationToken.None);

        Assert.NotNull(bytes);
        Assert.False(bytes!.Value.IsEmpty);
    }

    [Fact]
    public async Task ApplyInboundDelta_propagates_remote_mutation_to_local_document()
    {
        var engine = new StubCrdtEngine();
        await using var sourceDoc = engine.CreateDocument("default");
        await using var targetDoc = engine.CreateDocument("default");

        sourceDoc.GetText("greeting").Insert(0, "hola");
        var deltaBytes = sourceDoc.EncodeDelta(ReadOnlyMemory<byte>.Empty);

        var bridge = new AnchorCrdtDeltaBridge(targetDoc, NullLogger<AnchorCrdtDeltaBridge>.Instance);

        await bridge.ApplyInboundDeltaAsync(
            documentId: "default",
            opSequence: 1,
            delta: deltaBytes,
            ct: CancellationToken.None);

        Assert.Equal("hola", targetDoc.GetText("greeting").Value);
    }

    [Fact]
    public async Task ApplyInboundDelta_swallows_malformed_payload_without_throwing()
    {
        var engine = new StubCrdtEngine();
        await using var doc = engine.CreateDocument("default");
        var bridge = new AnchorCrdtDeltaBridge(doc, NullLogger<AnchorCrdtDeltaBridge>.Instance);

        // Random bytes — neither a valid stub-engine delta nor any known CRDT
        // wire format. Bridge must log + drop, not throw, so a single bad
        // peer frame doesn't trigger dead-peer backoff for the round.
        var malformed = new byte[] { 0xFF, 0xDE, 0xAD, 0xBE, 0xEF };

        await bridge.ApplyInboundDeltaAsync(
            documentId: "default",
            opSequence: 999,
            delta: malformed,
            ct: CancellationToken.None);

        // Document still empty — malformed delta did not corrupt local state.
        Assert.Equal("", doc.GetText("greeting").Value);
    }
}
