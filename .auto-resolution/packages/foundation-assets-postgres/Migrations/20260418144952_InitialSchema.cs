using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Sunfish.Foundation.Assets.Postgres.Migrations
{
    /// <inheritdoc />
    public partial class InitialSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "audit_records",
                columns: table => new
                {
                    audit_id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    entity_scheme = table.Column<string>(type: "text", nullable: false),
                    entity_authority = table.Column<string>(type: "text", nullable: false),
                    entity_local_part = table.Column<string>(type: "text", nullable: false),
                    version_sequence = table.Column<int>(type: "integer", nullable: true),
                    version_hash = table.Column<string>(type: "text", nullable: true),
                    op = table.Column<int>(type: "integer", nullable: false),
                    actor = table.Column<string>(type: "text", nullable: false),
                    tenant = table.Column<string>(type: "text", nullable: false),
                    at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    justification = table.Column<string>(type: "text", nullable: true),
                    payload_json = table.Column<string>(type: "jsonb", nullable: false),
                    signature = table.Column<byte[]>(type: "bytea", nullable: true),
                    prev_audit_id = table.Column<long>(type: "bigint", nullable: true),
                    hash = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_audit_records", x => x.audit_id);
                });

            migrationBuilder.CreateTable(
                name: "entities",
                columns: table => new
                {
                    entity_scheme = table.Column<string>(type: "text", nullable: false),
                    entity_authority = table.Column<string>(type: "text", nullable: false),
                    entity_local_part = table.Column<string>(type: "text", nullable: false),
                    schema = table.Column<string>(type: "text", nullable: false),
                    tenant = table.Column<string>(type: "text", nullable: false),
                    current_sequence = table.Column<int>(type: "integer", nullable: false),
                    current_hash = table.Column<string>(type: "text", nullable: false),
                    body_json = table.Column<string>(type: "jsonb", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    creation_nonce = table.Column<string>(type: "text", nullable: true),
                    creation_issuer = table.Column<string>(type: "text", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_entities", x => new { x.entity_scheme, x.entity_authority, x.entity_local_part });
                });

            migrationBuilder.CreateTable(
                name: "hierarchy_closure",
                columns: table => new
                {
                    closure_id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ancestor_scheme = table.Column<string>(type: "text", nullable: false),
                    ancestor_authority = table.Column<string>(type: "text", nullable: false),
                    ancestor_local_part = table.Column<string>(type: "text", nullable: false),
                    descendant_scheme = table.Column<string>(type: "text", nullable: false),
                    descendant_authority = table.Column<string>(type: "text", nullable: false),
                    descendant_local_part = table.Column<string>(type: "text", nullable: false),
                    depth = table.Column<int>(type: "integer", nullable: false),
                    valid_from = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    valid_to = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_hierarchy_closure", x => x.closure_id);
                });

            migrationBuilder.CreateTable(
                name: "hierarchy_edges",
                columns: table => new
                {
                    edge_id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    from_scheme = table.Column<string>(type: "text", nullable: false),
                    from_authority = table.Column<string>(type: "text", nullable: false),
                    from_local_part = table.Column<string>(type: "text", nullable: false),
                    to_scheme = table.Column<string>(type: "text", nullable: false),
                    to_authority = table.Column<string>(type: "text", nullable: false),
                    to_local_part = table.Column<string>(type: "text", nullable: false),
                    kind = table.Column<int>(type: "integer", nullable: false),
                    valid_from = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    valid_to = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    metadata_json = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_hierarchy_edges", x => x.edge_id);
                });

            migrationBuilder.CreateTable(
                name: "versions",
                columns: table => new
                {
                    entity_scheme = table.Column<string>(type: "text", nullable: false),
                    entity_authority = table.Column<string>(type: "text", nullable: false),
                    entity_local_part = table.Column<string>(type: "text", nullable: false),
                    sequence = table.Column<int>(type: "integer", nullable: false),
                    hash = table.Column<string>(type: "text", nullable: false),
                    parent_sequence = table.Column<int>(type: "integer", nullable: true),
                    parent_hash = table.Column<string>(type: "text", nullable: true),
                    body_json = table.Column<string>(type: "jsonb", nullable: false),
                    valid_from = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    valid_to = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    author = table.Column<string>(type: "text", nullable: false),
                    signature = table.Column<byte[]>(type: "bytea", nullable: true),
                    diff_json = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_versions", x => new { x.entity_scheme, x.entity_authority, x.entity_local_part, x.sequence });
                });

            migrationBuilder.CreateIndex(
                name: "ix_audit_at",
                table: "audit_records",
                column: "at");

            migrationBuilder.CreateIndex(
                name: "ix_audit_entity_at",
                table: "audit_records",
                columns: new[] { "entity_scheme", "entity_authority", "entity_local_part", "at" });

            migrationBuilder.CreateIndex(
                name: "ix_entities_schema",
                table: "entities",
                column: "schema");

            migrationBuilder.CreateIndex(
                name: "ix_entities_tenant",
                table: "entities",
                column: "tenant");

            migrationBuilder.CreateIndex(
                name: "ix_closure_descendant",
                table: "hierarchy_closure",
                columns: new[] { "descendant_scheme", "descendant_authority", "descendant_local_part" });

            migrationBuilder.CreateIndex(
                name: "ux_closure_edge_from",
                table: "hierarchy_closure",
                columns: new[] { "ancestor_scheme", "ancestor_authority", "ancestor_local_part", "descendant_scheme", "descendant_authority", "descendant_local_part", "depth", "valid_from" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_hierarchy_edges_from",
                table: "hierarchy_edges",
                columns: new[] { "kind", "from_scheme", "from_authority", "from_local_part" });

            migrationBuilder.CreateIndex(
                name: "ix_hierarchy_edges_to",
                table: "hierarchy_edges",
                columns: new[] { "kind", "to_scheme", "to_authority", "to_local_part" });

            migrationBuilder.CreateIndex(
                name: "ix_versions_entity_valid_from",
                table: "versions",
                columns: new[] { "entity_scheme", "entity_authority", "entity_local_part", "valid_from" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "audit_records");

            migrationBuilder.DropTable(
                name: "entities");

            migrationBuilder.DropTable(
                name: "hierarchy_closure");

            migrationBuilder.DropTable(
                name: "hierarchy_edges");

            migrationBuilder.DropTable(
                name: "versions");
        }
    }
}
