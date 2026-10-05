using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Foyer.Core.Data.Migrations
{
    /// <inheritdoc />
    public partial class PruneMissing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "MissingSince",
                table: "Bookmarks",
                type: "TEXT",
                nullable: true);

            // Bookmarks already hidden start their wait now, in the format EF writes DateTimeOffsets.
            migrationBuilder.Sql(
                "UPDATE \"Bookmarks\" SET \"MissingSince\" = strftime('%Y-%m-%d %H:%M:%f', 'now') || '+00:00' " +
                "WHERE \"IsPresent\" = 0;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MissingSince",
                table: "Bookmarks");
        }
    }
}
