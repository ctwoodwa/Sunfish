using Microsoft.EntityFrameworkCore;
using Sunfish.Foundation.Assets.Postgres.Audit;
using Sunfish.Foundation.Assets.Postgres.Entities;
using Sunfish.Foundation.Assets.Postgres.Hierarchy;
using Sunfish.Foundation.Assets.Postgres.Versions;

namespace Sunfish.Foundation.Assets.Postgres;

/// <summary>
/// EF Core <see cref="DbContext"/> backing the Postgres asset-store implementation.
/// </summary>
/// <remarks>
/// <para>
/// Persists the four Phase A primitives onto a normalized relational schema:
/// entities, an append-only version log, a hash-chained audit log, and a temporal
/// hierarchy (edges + closure table). All <see cref="Common.EntityId"/> identities
/// are stored as three separate columns (<c>scheme</c>, <c>authority</c>, <c>local_part</c>)
/// so indexes and joins are natural.
/// </para>
/// <para>
/// Plan D-POSTGRES-BACKEND (issue #9).
/// </para>
/// </remarks>
public sealed class AssetStoreDbContext(DbContextOptions<AssetStoreDbContext> options) : DbContext(options)
{
    /// <summary>Materialized "current" projection of each entity.</summary>
    public DbSet<EntityRow> Entities => Set<EntityRow>();

    /// <summary>Append-only version log.</summary>
    public DbSet<VersionRow> Versions => Set<VersionRow>();

    /// <summary>Append-only, hash-chained audit log.</summary>
    public DbSet<AuditRow> AuditRecords => Set<AuditRow>();

    /// <summary>Temporal hierarchy edges.</summary>
    public DbSet<EdgeRow> HierarchyEdges => Set<EdgeRow>();

    /// <summary>Temporal hierarchy closure table.</summary>
    public DbSet<ClosureRow> HierarchyClosure => Set<ClosureRow>();

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder mb)
    {
        ArgumentNullException.ThrowIfNull(mb);

        // --- entities -----------------------------------------------------------
        mb.Entity<EntityRow>(b =>
        {
            b.ToTable("entities");
            b.HasKey(e => new { e.EntityScheme, e.EntityAuthority, e.EntityLocalPart });
            b.Property(e => e.EntityScheme).HasColumnName("entity_scheme").IsRequired();
            b.Property(e => e.EntityAuthority).HasColumnName("entity_authority").IsRequired();
            b.Property(e => e.EntityLocalPart).HasColumnName("entity_local_part").IsRequired();
            b.Property(e => e.Schema).HasColumnName("schema").IsRequired();
            b.Property(e => e.Tenant).HasColumnName("tenant").IsRequired();
            b.Property(e => e.CurrentSequence).HasColumnName("current_sequence");
            b.Property(e => e.CurrentHash).HasColumnName("current_hash").IsRequired();
            b.Property(e => e.BodyJson).HasColumnName("body_json").HasColumnType("jsonb").IsRequired();
            b.Property(e => e.CreatedAt).HasColumnName("created_at").HasColumnType("timestamptz");
            b.Property(e => e.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamptz");
            b.Property(e => e.DeletedAt).HasColumnName("deleted_at").HasColumnType("timestamptz");
            b.Property(e => e.CreationNonce).HasColumnName("creation_nonce");
            b.Property(e => e.CreationIssuer).HasColumnName("creation_issuer").IsRequired();
            b.Property(e => e.RowVersion)
                .HasColumnName("xmin")
                .HasColumnType("xid")
                .ValueGeneratedOnAddOrUpdate()
                .IsConcurrencyToken();
            b.HasIndex(e => e.Schema).HasDatabaseName("ix_entities_schema");
            b.HasIndex(e => e.Tenant).HasDatabaseName("ix_entities_tenant");
        });

        // --- versions -----------------------------------------------------------
        mb.Entity<VersionRow>(b =>
        {
            b.ToTable("versions");
            b.HasKey(v => new { v.EntityScheme, v.EntityAuthority, v.EntityLocalPart, v.Sequence });
            b.Property(v => v.EntityScheme).HasColumnName("entity_scheme").IsRequired();
            b.Property(v => v.EntityAuthority).HasColumnName("entity_authority").IsRequired();
            b.Property(v => v.EntityLocalPart).HasColumnName("entity_local_part").IsRequired();
            b.Property(v => v.Sequence).HasColumnName("sequence");
            b.Property(v => v.Hash).HasColumnName("hash").IsRequired();
            b.Property(v => v.ParentSequence).HasColumnName("parent_sequence");
            b.Property(v => v.ParentHash).HasColumnName("parent_hash");
            b.Property(v => v.BodyJson).HasColumnName("body_json").HasColumnType("jsonb").IsRequired();
            b.Property(v => v.ValidFrom).HasColumnName("valid_from").HasColumnType("timestamptz");
            b.Property(v => v.ValidTo).HasColumnName("valid_to").HasColumnType("timestamptz");
            b.Property(v => v.Author).HasColumnName("author").IsRequired();
            b.Property(v => v.Signature).HasColumnName("signature").HasColumnType("bytea");
            b.Property(v => v.DiffJson).HasColumnName("diff_json").HasColumnType("jsonb");
            b.HasIndex(v => new { v.EntityScheme, v.EntityAuthority, v.EntityLocalPart, v.ValidFrom })
                .HasDatabaseName("ix_versions_entity_valid_from");
        });

        // --- audit --------------------------------------------------------------
        mb.Entity<AuditRow>(b =>
        {
            b.ToTable("audit_records");
            b.HasKey(a => a.AuditId);
            b.Property(a => a.AuditId).HasColumnName("audit_id").ValueGeneratedOnAdd();
            b.Property(a => a.EntityScheme).HasColumnName("entity_scheme").IsRequired();
            b.Property(a => a.EntityAuthority).HasColumnName("entity_authority").IsRequired();
            b.Property(a => a.EntityLocalPart).HasColumnName("entity_local_part").IsRequired();
            b.Property(a => a.VersionSequence).HasColumnName("version_sequence");
            b.Property(a => a.VersionHash).HasColumnName("version_hash");
            b.Property(a => a.Op).HasColumnName("op");
            b.Property(a => a.Actor).HasColumnName("actor").IsRequired();
            b.Property(a => a.Tenant).HasColumnName("tenant").IsRequired();
            b.Property(a => a.At).HasColumnName("at").HasColumnType("timestamptz");
            b.Property(a => a.Justification).HasColumnName("justification");
            b.Property(a => a.PayloadJson).HasColumnName("payload_json").HasColumnType("jsonb").IsRequired();
            b.Property(a => a.Signature).HasColumnName("signature").HasColumnType("bytea");
            b.Property(a => a.PrevAuditId).HasColumnName("prev_audit_id");
            b.Property(a => a.Hash).HasColumnName("hash").IsRequired();
            b.HasIndex(a => new { a.EntityScheme, a.EntityAuthority, a.EntityLocalPart, a.At })
                .HasDatabaseName("ix_audit_entity_at");
            b.HasIndex(a => a.At).HasDatabaseName("ix_audit_at");
        });

        // --- hierarchy edges ----------------------------------------------------
        mb.Entity<EdgeRow>(b =>
        {
            b.ToTable("hierarchy_edges");
            b.HasKey(e => e.EdgeId);
            b.Property(e => e.EdgeId).HasColumnName("edge_id").ValueGeneratedOnAdd();
            b.Property(e => e.FromScheme).HasColumnName("from_scheme").IsRequired();
            b.Property(e => e.FromAuthority).HasColumnName("from_authority").IsRequired();
            b.Property(e => e.FromLocalPart).HasColumnName("from_local_part").IsRequired();
            b.Property(e => e.ToScheme).HasColumnName("to_scheme").IsRequired();
            b.Property(e => e.ToAuthority).HasColumnName("to_authority").IsRequired();
            b.Property(e => e.ToLocalPart).HasColumnName("to_local_part").IsRequired();
            b.Property(e => e.Kind).HasColumnName("kind");
            b.Property(e => e.ValidFrom).HasColumnName("valid_from").HasColumnType("timestamptz");
            b.Property(e => e.ValidTo).HasColumnName("valid_to").HasColumnType("timestamptz");
            b.Property(e => e.MetadataJson).HasColumnName("metadata_json").HasColumnType("jsonb");
            b.HasIndex(e => new { e.Kind, e.ToScheme, e.ToAuthority, e.ToLocalPart })
                .HasDatabaseName("ix_hierarchy_edges_to");
            b.HasIndex(e => new { e.Kind, e.FromScheme, e.FromAuthority, e.FromLocalPart })
                .HasDatabaseName("ix_hierarchy_edges_from");
        });

        // --- hierarchy closure --------------------------------------------------
        mb.Entity<ClosureRow>(b =>
        {
            b.ToTable("hierarchy_closure");
            b.HasKey(c => c.ClosureId);
            b.Property(c => c.ClosureId).HasColumnName("closure_id").ValueGeneratedOnAdd();
            b.Property(c => c.AncestorScheme).HasColumnName("ancestor_scheme").IsRequired();
            b.Property(c => c.AncestorAuthority).HasColumnName("ancestor_authority").IsRequired();
            b.Property(c => c.AncestorLocalPart).HasColumnName("ancestor_local_part").IsRequired();
            b.Property(c => c.DescendantScheme).HasColumnName("descendant_scheme").IsRequired();
            b.Property(c => c.DescendantAuthority).HasColumnName("descendant_authority").IsRequired();
            b.Property(c => c.DescendantLocalPart).HasColumnName("descendant_local_part").IsRequired();
            b.Property(c => c.Depth).HasColumnName("depth");
            b.Property(c => c.ValidFrom).HasColumnName("valid_from").HasColumnType("timestamptz");
            b.Property(c => c.ValidTo).HasColumnName("valid_to").HasColumnType("timestamptz");
            b.HasIndex(c => new
            {
                c.AncestorScheme,
                c.AncestorAuthority,
                c.AncestorLocalPart,
                c.DescendantScheme,
                c.DescendantAuthority,
                c.DescendantLocalPart,
                c.Depth,
                c.ValidFrom,
            })
                .IsUnique()
                .HasDatabaseName("ux_closure_edge_from");
            b.HasIndex(c => new { c.DescendantScheme, c.DescendantAuthority, c.DescendantLocalPart })
                .HasDatabaseName("ix_closure_descendant");
        });
    }
}
