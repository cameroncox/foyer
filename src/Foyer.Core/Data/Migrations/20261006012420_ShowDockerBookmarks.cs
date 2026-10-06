using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Foyer.Core.Data.Migrations
{
    /// <inheritdoc />
    public partial class ShowDockerBookmarks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "ShowsDockerBookmarks",
                table: "Profiles",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // In place, as in DefaultHandover: EF's DropColumn rebuilds Profiles, which cascades.
            migrationBuilder.Sql("ALTER TABLE \"Profiles\" DROP COLUMN \"ShowsDockerBookmarks\";");
        }
    }
}
