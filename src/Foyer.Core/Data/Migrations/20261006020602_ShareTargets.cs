using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Foyer.Core.Data.Migrations
{
    /// <inheritdoc />
    public partial class ShareTargets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "ShareWithEveryone",
                table: "Bookmarks",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "ShareTargets",
                columns: table => new
                {
                    BookmarkId = table.Column<int>(type: "INTEGER", nullable: false),
                    ProfileId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ShareTargets", x => new { x.BookmarkId, x.ProfileId });
                    table.ForeignKey(
                        name: "FK_ShareTargets_Bookmarks_BookmarkId",
                        column: x => x.BookmarkId,
                        principalTable: "Bookmarks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ShareTargets_Profiles_ProfileId",
                        column: x => x.ProfileId,
                        principalTable: "Profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ShareTargets_ProfileId",
                table: "ShareTargets",
                column: "ProfileId");

            // Everything shared so far was shared with everyone.
            migrationBuilder.Sql("UPDATE \"Bookmarks\" SET \"ShareWithEveryone\" = 1 WHERE \"IsShared\" = 1;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Without ShareWithEveryone, IsShared means everyone, so bookmarks shared with chosen
            // profiles are unshared rather than widened. Docker ones stay where profiles show them.
            migrationBuilder.Sql("""
                DELETE FROM "SharedPlacements"
                WHERE "BookmarkId" IN (SELECT "Id" FROM "Bookmarks" WHERE "IsShared" = 1 AND "ShareWithEveryone" = 0)
                  AND NOT EXISTS (
                    SELECT 1 FROM "Bookmarks" b JOIN "Profiles" p ON p."Id" = "SharedPlacements"."ProfileId"
                    WHERE b."Id" = "SharedPlacements"."BookmarkId" AND b."Source" = 'Docker' AND p."ShowsDockerBookmarks" = 1);
                """);
            migrationBuilder.Sql("UPDATE \"Bookmarks\" SET \"IsShared\" = 0 WHERE \"IsShared\" = 1 AND \"ShareWithEveryone\" = 0;");

            migrationBuilder.DropTable(
                name: "ShareTargets");

            // In place, as in DefaultHandover: EF's DropColumn rebuilds Bookmarks, which cascades
            // into tags and placements.
            migrationBuilder.Sql("ALTER TABLE \"Bookmarks\" DROP COLUMN \"ShareWithEveryone\";");
        }
    }
}
