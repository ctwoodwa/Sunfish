// Sunfish.Kernel — Type Forwards
//
// This file is the heart of the G1 "virtual facade" package. Each
// [TypeForwardedTo] entry publishes a Foundation type through the
// Sunfish.Kernel assembly under its ORIGINAL fully-qualified name
// (Sunfish.Foundation.*), so consumers that take a dependency on
// Sunfish.Kernel pick up the exact same primitive contracts as
// consumers depending on Sunfish.Foundation directly.
//
// Naming note: platform spec §3 uses the short names `IEntityStore`,
// `IVersionStore`, `IAuditLog`, `IPermissionEvaluator`, `IBlobStore`,
// etc. The shipped Foundation types use the same short names under
// sub-namespaces (`Sunfish.Foundation.Assets.Entities.IEntityStore`
// and so on). We forward types at their SHIPPED namespaces rather
// than fabricate synonyms in `Sunfish.Kernel.*`; see README for the
// rationale.
//
// Two primitives from §3 are NOT forwarded here because they are not
// yet shipped:
//   • §3.4 Schema Registry — stub lives at Sunfish.Kernel.Schema.ISchemaRegistry (gap G2)
//   • §3.6 Event Bus       — stub lives at Sunfish.Kernel.Events.IEventBus     (gap G3)

using System.Runtime.CompilerServices;

// -----------------------------------------------------------------------------
// §3.1 Entity Store
// -----------------------------------------------------------------------------
[assembly: TypeForwardedTo(typeof(Sunfish.Foundation.Assets.Entities.IEntityStore))]
[assembly: TypeForwardedTo(typeof(Sunfish.Foundation.Assets.Entities.InMemoryEntityStore))]
[assembly: TypeForwardedTo(typeof(Sunfish.Foundation.Assets.Entities.Entity))]
[assembly: TypeForwardedTo(typeof(Sunfish.Foundation.Assets.Entities.EntityQuery))]
[assembly: TypeForwardedTo(typeof(Sunfish.Foundation.Assets.Entities.CreateOptions))]
[assembly: TypeForwardedTo(typeof(Sunfish.Foundation.Assets.Entities.UpdateOptions))]
[assembly: TypeForwardedTo(typeof(Sunfish.Foundation.Assets.Entities.DeleteOptions))]
[assembly: TypeForwardedTo(typeof(Sunfish.Foundation.Assets.Entities.VersionSelector))]
[assembly: TypeForwardedTo(typeof(Sunfish.Foundation.Assets.Entities.IEntityValidator))]
[assembly: TypeForwardedTo(typeof(Sunfish.Foundation.Assets.Entities.NullEntityValidator))]
[assembly: TypeForwardedTo(typeof(Sunfish.Foundation.Assets.Entities.EntityValidationException))]
[assembly: TypeForwardedTo(typeof(Sunfish.Foundation.Assets.Entities.IdempotencyConflictException))]
[assembly: TypeForwardedTo(typeof(Sunfish.Foundation.Assets.Entities.ConcurrencyException))]

// -----------------------------------------------------------------------------
// §3.2 Version Store
// -----------------------------------------------------------------------------
[assembly: TypeForwardedTo(typeof(Sunfish.Foundation.Assets.Versions.IVersionStore))]
[assembly: TypeForwardedTo(typeof(Sunfish.Foundation.Assets.Versions.InMemoryVersionStore))]
[assembly: TypeForwardedTo(typeof(Sunfish.Foundation.Assets.Versions.Version))]
[assembly: TypeForwardedTo(typeof(Sunfish.Foundation.Assets.Versions.IVersionObserver))]
[assembly: TypeForwardedTo(typeof(Sunfish.Foundation.Assets.Versions.NullVersionObserver))]
[assembly: TypeForwardedTo(typeof(Sunfish.Foundation.Assets.Versions.BranchOptions))]
[assembly: TypeForwardedTo(typeof(Sunfish.Foundation.Assets.Versions.MergeOptions))]

// -----------------------------------------------------------------------------
// §3.3 Audit Log
// -----------------------------------------------------------------------------
[assembly: TypeForwardedTo(typeof(Sunfish.Foundation.Assets.Audit.IAuditLog))]
[assembly: TypeForwardedTo(typeof(Sunfish.Foundation.Assets.Audit.InMemoryAuditLog))]
[assembly: TypeForwardedTo(typeof(Sunfish.Foundation.Assets.Audit.AuditRecord))]
[assembly: TypeForwardedTo(typeof(Sunfish.Foundation.Assets.Audit.AuditAppend))]
[assembly: TypeForwardedTo(typeof(Sunfish.Foundation.Assets.Audit.AuditQuery))]
[assembly: TypeForwardedTo(typeof(Sunfish.Foundation.Assets.Audit.AuditId))]
[assembly: TypeForwardedTo(typeof(Sunfish.Foundation.Assets.Audit.HashChain))]
[assembly: TypeForwardedTo(typeof(Sunfish.Foundation.Assets.Audit.IAuditContextProvider))]
[assembly: TypeForwardedTo(typeof(Sunfish.Foundation.Assets.Audit.NullAuditContextProvider))]
[assembly: TypeForwardedTo(typeof(Sunfish.Foundation.Assets.Audit.Op))]

// -----------------------------------------------------------------------------
// §3.3 Supporting: Entity Hierarchy
// Treated as part of the entity-store surface per spec §3.1; re-exported for
// parity with consumers that compose hierarchy on top of entities.
// -----------------------------------------------------------------------------
[assembly: TypeForwardedTo(typeof(Sunfish.Foundation.Assets.Hierarchy.IHierarchyService))]
[assembly: TypeForwardedTo(typeof(Sunfish.Foundation.Assets.Hierarchy.InMemoryHierarchyService))]

// -----------------------------------------------------------------------------
// §3.5 Permission Evaluator
// -----------------------------------------------------------------------------
[assembly: TypeForwardedTo(typeof(Sunfish.Foundation.PolicyEvaluator.IPermissionEvaluator))]
[assembly: TypeForwardedTo(typeof(Sunfish.Foundation.PolicyEvaluator.ReBACPolicyEvaluator))]
[assembly: TypeForwardedTo(typeof(Sunfish.Foundation.PolicyEvaluator.Decision))]
[assembly: TypeForwardedTo(typeof(Sunfish.Foundation.PolicyEvaluator.DecisionKind))]
[assembly: TypeForwardedTo(typeof(Sunfish.Foundation.PolicyEvaluator.Subject))]
[assembly: TypeForwardedTo(typeof(Sunfish.Foundation.PolicyEvaluator.ActionType))]
[assembly: TypeForwardedTo(typeof(Sunfish.Foundation.PolicyEvaluator.PolicyResource))]
[assembly: TypeForwardedTo(typeof(Sunfish.Foundation.PolicyEvaluator.ContextEnvelope))]
[assembly: TypeForwardedTo(typeof(Sunfish.Foundation.PolicyEvaluator.Obligation))]

// -----------------------------------------------------------------------------
// §3.7 Blob Store
// -----------------------------------------------------------------------------
[assembly: TypeForwardedTo(typeof(Sunfish.Foundation.Blobs.IBlobStore))]
[assembly: TypeForwardedTo(typeof(Sunfish.Foundation.Blobs.FileSystemBlobStore))]
[assembly: TypeForwardedTo(typeof(Sunfish.Foundation.Blobs.Cid))]

// -----------------------------------------------------------------------------
// Common identity types — referenced across multiple primitives. Re-exported
// per gap G1 scope ("common identity types used across primitives"). The
// broader Crypto / Capabilities / Macaroons surfaces stay in Foundation; this
// re-export is intentionally minimal.
// -----------------------------------------------------------------------------
[assembly: TypeForwardedTo(typeof(Sunfish.Foundation.Assets.Common.EntityId))]
[assembly: TypeForwardedTo(typeof(Sunfish.Foundation.Assets.Common.VersionId))]
[assembly: TypeForwardedTo(typeof(Sunfish.Foundation.Assets.Common.Instant))]
[assembly: TypeForwardedTo(typeof(Sunfish.Foundation.Assets.Common.ActorId))]
[assembly: TypeForwardedTo(typeof(Sunfish.Foundation.Assets.Common.TenantId))]
[assembly: TypeForwardedTo(typeof(Sunfish.Foundation.Assets.Common.SchemaId))]

[assembly: TypeForwardedTo(typeof(Sunfish.Foundation.Crypto.PrincipalId))]
[assembly: TypeForwardedTo(typeof(Sunfish.Foundation.Crypto.Signature))]
[assembly: TypeForwardedTo(typeof(Sunfish.Foundation.Crypto.SignedOperation<>))]
