using System.Security.Cryptography;
using Sunfish.Foundation.Capabilities;
using Sunfish.Foundation.Crypto;
using Sunfish.Foundation.Macaroons;
using Sunfish.Foundation.PolicyEvaluator;
using Xunit;

namespace Sunfish.Foundation.Tests.Integration;

/// <summary>
/// End-to-end Phase B integration — wires the capability graph, policy evaluator, and macaroon
/// issuer/verifier into the §3.5 property-management narrative. Exercises the "two primitives
/// coexist" property: the ReBAC evaluator and the capability graph reach the same permit/deny
/// outcome over independently-maintained state, and the macaroon layer operates orthogonally on
/// a time-bound bearer credential.
/// </summary>
/// <remarks>
/// <para>Cast:</para>
/// <list type="bullet">
///   <item><description><c>sam</c> — landlord, root owner of <c>inspection:2026-04-17</c></description></item>
///   <item><description><c>jim</c> — inspector; gets access via membership in <c>acmeFirm</c></description></item>
///   <item><description><c>alice</c> — acme-inspection admin; root owner of <c>acmeFirm</c></description></item>
///   <item><description><c>acmeFirm</c> — group principal, initial members [alice]</description></item>
/// </list>
/// <para>
/// Group-principal identity: Phase B does not mandate a specific derivation for a group's
/// <see cref="PrincipalId"/> (spec §10.2.1 suggests SHA-256 over the canonical JSON of the
/// <see cref="MintPrincipal"/> op, but this is not yet in the implementation). For test
/// convenience we generate a standalone <see cref="KeyPair"/> and treat its
/// <see cref="PrincipalId"/> as the group id — the keypair itself is discarded, and the group
/// is operated on by its (already-minted) admin <c>alice</c>. This is a Phase-B-convenience
/// shortcut, not a guarantee of how groups will be identified in later phases.
/// </para>
/// </remarks>
public sealed class DecentralizationEndToEndTests : IDisposable
{
    private readonly Ed25519Verifier _verifier;
    private readonly InMemoryCapabilityGraph _graph;
    private readonly InMemoryRelationTupleStore _tupleStore;
    private readonly PolicyModel _policyModel;
    private readonly ReBACPolicyEvaluator _evaluator;

    private readonly KeyPair _sam, _jim, _alice;
    private readonly Ed25519Signer _samSigner, _jimSigner, _aliceSigner;
    private readonly PrincipalId _acmeFirmPrincipal;

    // Policy-layer handles.
    private readonly PolicyResource _acmeFirmPolicyResource;
    private readonly PolicyResource _inspectionPolicyResource;

    // Capability-layer handles.
    private readonly Resource _inspectionCapabilityResource;

    // Timeline anchors.
    private static readonly DateTimeOffset T0 = DateTimeOffset.Parse("2026-04-10T09:00:00Z");
    private static readonly DateTimeOffset InsideWindow = DateTimeOffset.Parse("2026-04-17T14:00:00Z");
    private static readonly DateTimeOffset AfterExpiry = DateTimeOffset.Parse("2026-07-15T00:00:00Z");

    public DecentralizationEndToEndTests()
    {
        _verifier = new Ed25519Verifier();
        _graph = new InMemoryCapabilityGraph(_verifier);
        _tupleStore = new InMemoryRelationTupleStore();

        _sam = KeyPair.Generate();
        _jim = KeyPair.Generate();
        _alice = KeyPair.Generate();
        _samSigner = new Ed25519Signer(_sam);
        _jimSigner = new Ed25519Signer(_jim);
        _aliceSigner = new Ed25519Signer(_alice);

        // Phase-B-convenience: treat a freshly-generated keypair's PrincipalId as the group id.
        // The keypair is disposed immediately — alice (the group admin) is the authority.
        using (var groupKp = KeyPair.Generate())
        {
            _acmeFirmPrincipal = groupKp.PrincipalId;
        }

        _inspectionCapabilityResource = new Resource("inspection:2026-04-17");
        _acmeFirmPolicyResource = new PolicyResource("inspection_firm", _acmeFirmPrincipal.ToBase64Url());
        _inspectionPolicyResource = new PolicyResource("inspection", "2026-04-17");

        // --- Capability-graph setup ----------------------------------------------------------

        // Everyone mints themselves (Phase B MintPrincipal is open-mint).
        MutateOrThrow(_samSigner.SignAsync<CapabilityOp>(
            new MintPrincipal(_sam.PrincipalId, PrincipalKind.Individual), T0, Guid.NewGuid())
            .AsTask().GetAwaiter().GetResult());
        MutateOrThrow(_jimSigner.SignAsync<CapabilityOp>(
            new MintPrincipal(_jim.PrincipalId, PrincipalKind.Individual), T0, Guid.NewGuid())
            .AsTask().GetAwaiter().GetResult());
        MutateOrThrow(_aliceSigner.SignAsync<CapabilityOp>(
            new MintPrincipal(_alice.PrincipalId, PrincipalKind.Individual), T0, Guid.NewGuid())
            .AsTask().GetAwaiter().GetResult());

        // Alice mints the acmeFirm group with herself as the initial (sole) member.
        MutateOrThrow(_aliceSigner.SignAsync<CapabilityOp>(
            new MintPrincipal(_acmeFirmPrincipal, PrincipalKind.Group, new[] { _alice.PrincipalId }),
            T0, Guid.NewGuid()).AsTask().GetAwaiter().GetResult());

        // Alice adds jim to acmeFirm. Bootstrap authority: she is the root admin (issuer of the
        // first membership op on this group).
        MutateOrThrow(_aliceSigner.SignAsync<CapabilityOp>(
            new AddMember(_acmeFirmPrincipal, _jim.PrincipalId), T0, Guid.NewGuid())
            .AsTask().GetAwaiter().GetResult());

        // Sam delegates can_write on the inspection to acmeFirm, expires 2026-06-30.
        // Bootstrap: no prior Delegate on this resource — Sam becomes the root owner.
        MutateOrThrow(_samSigner.SignAsync<CapabilityOp>(
            new Sunfish.Foundation.Capabilities.Delegate(
                _acmeFirmPrincipal,
                _inspectionCapabilityResource,
                new CapabilityAction("can_write"),
                Expires: DateTimeOffset.Parse("2026-06-30T23:59:59Z")),
            T0, Guid.NewGuid()).AsTask().GetAwaiter().GetResult());

        // --- Policy-evaluator setup ----------------------------------------------------------
        //
        // Mirrors the §3.5 worked example, scaled to the "can_write" verb:
        //   user                    — leaf principals
        //   inspection_firm         — has employees (direct users of type "user")
        //   inspection              — can_write = direct users OR employees-of-firm
        //
        // The ReBAC evaluator consults its tuple store, not the capability graph — we seed both
        // to prove the two primitives coexist.
        _policyModel = PolicyModel.Create()
            .Type("user")
            .Type("inspection_firm", t => t
                .Relation("employee", new RelationRewrite.DirectUsers(new[] { "user" })))
            .Type("inspection", t => t
                .Relation("can_write", new RelationRewrite.Union(new RelationRewrite[]
                {
                    new RelationRewrite.DirectUsers(new[] { "user" }),
                    new RelationRewrite.TupleToUserset("firm", "employee"),
                }))
                .Relation("firm", new RelationRewrite.DirectUsers(new[] { "inspection_firm" })))
            .Build();

        // (acmeFirm-as-resource) --firm--> inspection:2026-04-17
        _tupleStore.AddAsync(
            new UsersetRef.SelfRef(_acmeFirmPolicyResource), "firm", _inspectionPolicyResource)
            .AsTask().GetAwaiter().GetResult();
        // jim --employee--> acmeFirm
        _tupleStore.AddAsync(
            new UsersetRef.User(new Subject(_jim.PrincipalId)), "employee", _acmeFirmPolicyResource)
            .AsTask().GetAwaiter().GetResult();

        _evaluator = new ReBACPolicyEvaluator(_policyModel, _tupleStore);
    }

    /// <summary>
    /// Wraps <see cref="ICapabilityGraph.MutateAsync"/> and throws if the op is rejected.
    /// Keeps setup code linear at the cost of losing the rejection reason if a setup op trips a
    /// gate unexpectedly — which is fine, a rejected setup is a test-authoring bug.
    /// </summary>
    private void MutateOrThrow(SignedOperation<CapabilityOp> op)
    {
        var result = _graph.MutateAsync(op).AsTask().GetAwaiter().GetResult();
        if (result.Kind == MutationKind.Rejected)
            throw new InvalidOperationException(
                $"Mutate rejected: {result.Reason} for op {op.Payload.GetType().Name}");
    }

    [Fact]
    public async Task Fact_JimCanWriteInspection_WhenInFirmAndWithinExpiryWindow()
    {
        // Capability-graph view: jim holds can_write on the inspection because acmeFirm does
        // (via Sam's delegate) and jim is a member of acmeFirm.
        var capAllowed = await _graph.QueryAsync(
            _jim.PrincipalId, _inspectionCapabilityResource, new CapabilityAction("can_write"), InsideWindow);
        Assert.True(capAllowed);

        // Policy-evaluator view: jim has can_write because he is an employee of acmeFirm, which
        // is the firm for the inspection. Same outcome, different primitive.
        var decision = await _evaluator.EvaluateAsync(
            new Subject(_jim.PrincipalId),
            new ActionType("write"),
            _inspectionPolicyResource,
            new ContextEnvelope(InsideWindow, Purpose: "e2e"));
        Assert.Equal(DecisionKind.Permit, decision.Kind);
        Assert.Equal("inspection#can_write", Assert.Single(decision.MatchedPolicies));
    }

    [Fact]
    public async Task Fact_JimCannotWriteInspection_AfterExpiry()
    {
        // Capability-graph view: Sam's delegate expired 2026-06-30 — the query at AfterExpiry
        // must return false.
        var capAllowed = await _graph.QueryAsync(
            _jim.PrincipalId, _inspectionCapabilityResource, new CapabilityAction("can_write"), AfterExpiry);
        Assert.False(capAllowed);
    }

    [Fact]
    public async Task Fact_RemoveMember_RemovesCapability()
    {
        // Alice removes jim from acmeFirm on 2026-04-20. Before removal the capability query at
        // 2026-04-21 would have returned true; after removal it must return false.
        var removeOp = await _aliceSigner.SignAsync<CapabilityOp>(
            new RemoveMember(_acmeFirmPrincipal, _jim.PrincipalId),
            DateTimeOffset.Parse("2026-04-20T09:00:00Z"),
            Guid.NewGuid());
        var result = await _graph.MutateAsync(removeOp);
        Assert.Equal(MutationKind.Accepted, result.Kind);

        // Mirror the removal into the policy-evaluator tuple store — the two stores are
        // independent by design, so the application layer is responsible for the write-through.
        await _tupleStore.RemoveAsync(
            new UsersetRef.User(new Subject(_jim.PrincipalId)), "employee", _acmeFirmPolicyResource);

        var afterRemoval = DateTimeOffset.Parse("2026-04-21T14:00:00Z");

        var capAllowed = await _graph.QueryAsync(
            _jim.PrincipalId, _inspectionCapabilityResource, new CapabilityAction("can_write"), afterRemoval);
        Assert.False(capAllowed);

        var decision = await _evaluator.EvaluateAsync(
            new Subject(_jim.PrincipalId),
            new ActionType("write"),
            _inspectionPolicyResource,
            new ContextEnvelope(afterRemoval, Purpose: "e2e"));
        Assert.Equal(DecisionKind.Deny, decision.Kind);
    }

    [Fact]
    public async Task Fact_Macaroon_MintAndVerify_IndependentOfGraph()
    {
        // Sam can independently mint a 48h bearer token that permits jim to read the inspection.
        // The macaroon layer does not consult the capability graph — it is an orthogonal
        // authority primitive operating over a shared root key.
        var keyStore = new InMemoryRootKeyStore();
        var rootKey = new byte[32];
        RandomNumberGenerator.Fill(rootKey);
        const string location = "https://acme-rentals.sunfish.example/";
        keyStore.Set(location, rootKey);

        var issuer = new DefaultMacaroonIssuer(keyStore);
        var verifier = new DefaultMacaroonVerifier(keyStore);

        var macaroon = await issuer.MintAsync(
            location,
            "emergency-inspection-2026-04-17-01",
            new[]
            {
                new Caveat("time <= \"2026-04-19T00:00:00Z\""),
                new Caveat("subject == \"individual:jim\""),
                new Caveat("action in [\"read\"]"),
            });

        var validCtx = new MacaroonContext(
            Now: DateTimeOffset.Parse("2026-04-18T12:00:00Z"),
            SubjectUri: "individual:jim",
            ResourceSchema: null,
            RequestedAction: "read",
            DeviceIp: null);
        var validResult = await verifier.VerifyAsync(macaroon, validCtx);
        Assert.True(validResult.IsValid);

        var expiredCtx = validCtx with { Now = DateTimeOffset.Parse("2026-04-20T00:00:00Z") };
        var expiredResult = await verifier.VerifyAsync(macaroon, expiredCtx);
        Assert.False(expiredResult.IsValid);
    }

    [Fact]
    public async Task Fact_ExportProof_ReturnsDelegateAndMembershipOps()
    {
        // The exported proof for jim's can_write on the inspection should include at least one
        // Delegate (Sam → acmeFirm) and one AddMember (alice adds jim).
        var proof = await _graph.ExportProofAsync(
            _jim.PrincipalId, _inspectionCapabilityResource, new CapabilityAction("can_write"), InsideWindow);

        Assert.NotNull(proof);
        Assert.Contains(proof!.OpChain, o => o.Payload is Sunfish.Foundation.Capabilities.Delegate);
        Assert.Contains(proof.OpChain, o => o.Payload is AddMember);
    }

    [Fact]
    public async Task Fact_ListOpsAsync_EnumeratesMutations()
    {
        // Count: 3 MintPrincipal (individuals: sam, jim, alice)
        //      + 1 MintPrincipal (group: acmeFirm)
        //      + 1 AddMember (alice adds jim)
        //      + 1 Delegate   (sam → acmeFirm, can_write, inspection)
        //      = 6 applied ops.
        var ops = new List<SignedOperation<CapabilityOp>>();
        await foreach (var op in _graph.ListOpsAsync())
            ops.Add(op);

        Assert.Equal(6, ops.Count);
    }

    public void Dispose()
    {
        _sam.Dispose();
        _jim.Dispose();
        _alice.Dispose();
    }
}
