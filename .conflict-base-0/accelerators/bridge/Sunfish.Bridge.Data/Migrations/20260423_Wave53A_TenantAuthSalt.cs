using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sunfish.Bridge.Data.Migrations
{
    /// <inheritdoc />
    /// <remarks>
    /// Wave 5.3.A — adds the browser-shell per-tenant auth salt column
    /// consumed by <c>TenantRegistry.CreateAsync</c> and the Wave 5.3.B
    /// <c>GET /auth/salt?slug=…</c> endpoint
    /// (see <c>_shared/product/wave-5.3-decomposition.md</c> §2.2):
    /// <list type="bullet">
    ///   <item><c>AuthSalt</c> — nullable <c>bytea</c>, 16 random bytes per
    ///     tenant populated at <c>CreateAsync</c> time. Nullable so the
    ///     migration is non-destructive for rows that predate Wave 5.3.A;
    ///     those rows are backfilled in a separate pass and the demo tenant
    ///     is populated by <c>BridgeSeeder</c>.</item>
    /// </list>
    /// The salt is non-secret — the browser fetches it by slug on the login
    /// page — so it does not need to be unique-indexed or otherwise protected
    /// at the DB layer.
    /// </remarks>
    public partial class Wave53A_TenantAuthSalt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<byte[]>(
                name: "AuthSalt",
                table: "tenant_registrations",
                type: "bytea",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AuthSalt",
                table: "tenant_registrations");
        }
    }
}
