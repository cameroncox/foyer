using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Foyer.Core.Data.Migrations
{
    /// <inheritdoc />
    public partial class DefaultHandover : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "HandoverSettledAt",
                table: "Profiles",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // SQLite drops a plain column in place. EF's DropColumn rebuilds the table, and dropping
            // Profiles inside the migration's transaction would cascade into every category.
            migrationBuilder.Sql("ALTER TABLE \"Profiles\" DROP COLUMN \"HandoverSettledAt\";");
        }
    }
}
