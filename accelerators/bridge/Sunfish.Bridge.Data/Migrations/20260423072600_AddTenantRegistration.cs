using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sunfish.Bridge.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTenantRegistration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "tenant_registrations",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Slug = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Plan = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false, defaultValue: "Free"),
                    TeamPublicKey = table.Column<byte[]>(type: "bytea", nullable: true),
                    TrustLevel = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    SupportContacts = table.Column<string>(type: "jsonb", nullable: false),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tenant_registrations", x => x.TenantId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_tenant_registrations_Slug",
                table: "tenant_registrations",
                column: "Slug",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "tenant_registrations");
        }
    }
}
