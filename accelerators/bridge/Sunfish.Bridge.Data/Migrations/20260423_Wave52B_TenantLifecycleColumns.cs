using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sunfish.Bridge.Data.Migrations
{
    /// <inheritdoc />
    /// <remarks>
    /// Wave 5.2.B — adds the two lifecycle columns consumed by
    /// <c>TenantRegistry.SuspendAsync</c> / <c>CancelAsync</c>
    /// (see <c>_shared/product/wave-5.2-decomposition.md</c> §2.2 and §2.4):
    /// <list type="bullet">
    ///   <item><c>SuspendedReason</c> — nullable varchar(500), human-readable
    ///     audit note attached to the most recent Suspended transition.</item>
    ///   <item><c>CancelledAt</c> — nullable timestamptz, UTC instant of the
    ///     most recent Cancelled transition, consumed by the Wave 5.2.C
    ///     supervisor when composing the graveyard folder name.</item>
    /// </list>
    /// Both columns default to <c>NULL</c> so the migration is non-destructive
    /// for pre-5.2.B rows.
    /// </remarks>
    public partial class Wave52B_TenantLifecycleColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SuspendedReason",
                table: "tenant_registrations",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<System.DateTime>(
                name: "CancelledAt",
                table: "tenant_registrations",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SuspendedReason",
                table: "tenant_registrations");

            migrationBuilder.DropColumn(
                name: "CancelledAt",
                table: "tenant_registrations");
        }
    }
}
