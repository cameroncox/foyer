using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Foyer.Core.Data.Migrations
{
    /// <inheritdoc />
    public partial class Profiles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Categories_Name",
                table: "Categories");

            // Every 1.0 category and bookmark moves into Default (id 1, from DefaultProfile), unshared.
            migrationBuilder.AddColumn<int>(
                name: "ProfileId",
                table: "Categories",
                type: "INTEGER",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<bool>(
                name: "IsShared",
                table: "Bookmarks",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "ProfileId",
                table: "Bookmarks",
                type: "INTEGER",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.CreateTable(
                name: "SharedPlacements",
                columns: table => new
                {
                    ProfileId = table.Column<int>(type: "INTEGER", nullable: false),
                    BookmarkId = table.Column<int>(type: "INTEGER", nullable: false),
                    CategoryId = table.Column<int>(type: "INTEGER", nullable: false),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SharedPlacements", x => new { x.ProfileId, x.BookmarkId });
                    table.ForeignKey(
                        name: "FK_SharedPlacements_Bookmarks_BookmarkId",
                        column: x => x.BookmarkId,
                        principalTable: "Bookmarks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SharedPlacements_Categories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "Categories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SharedPlacements_Profiles_ProfileId",
                        column: x => x.ProfileId,
                        principalTable: "Profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.UpdateData(
                table: "Categories",
                keyColumn: "Id",
                keyValue: 1,
                column: "ProfileId",
                value: 1);

            migrationBuilder.CreateIndex(
                name: "IX_Categories_ProfileId_Name",
                table: "Categories",
                columns: new[] { "ProfileId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Bookmarks_ProfileId",
                table: "Bookmarks",
                column: "ProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_SharedPlacements_BookmarkId",
                table: "SharedPlacements",
                column: "BookmarkId");

            migrationBuilder.CreateIndex(
                name: "IX_SharedPlacements_CategoryId_SortOrder",
                table: "SharedPlacements",
                columns: new[] { "CategoryId", "SortOrder" });

            migrationBuilder.AddForeignKey(
                name: "FK_Bookmarks_Profiles_ProfileId",
                table: "Bookmarks",
                column: "ProfileId",
                principalTable: "Profiles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Categories_Profiles_ProfileId",
                table: "Categories",
                column: "ProfileId",
                principalTable: "Profiles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Bookmarks_Profiles_ProfileId",
                table: "Bookmarks");

            migrationBuilder.DropForeignKey(
                name: "FK_Categories_Profiles_ProfileId",
                table: "Categories");

            migrationBuilder.DropTable(
                name: "SharedPlacements");

            // 1.0 has no profiles, so only Default's rows survive a rollback. Tags cascade.
            migrationBuilder.Sql("DELETE FROM \"Bookmarks\" WHERE \"ProfileId\" <> 1;");
            migrationBuilder.Sql("DELETE FROM \"Categories\" WHERE \"ProfileId\" <> 1;");

            migrationBuilder.DropIndex(
                name: "IX_Categories_ProfileId_Name",
                table: "Categories");

            migrationBuilder.DropIndex(
                name: "IX_Bookmarks_ProfileId",
                table: "Bookmarks");

            migrationBuilder.DropColumn(
                name: "ProfileId",
                table: "Categories");

            migrationBuilder.DropColumn(
                name: "IsShared",
                table: "Bookmarks");

            migrationBuilder.DropColumn(
                name: "ProfileId",
                table: "Bookmarks");

            migrationBuilder.CreateIndex(
                name: "IX_Categories_Name",
                table: "Categories",
                column: "Name",
                unique: true);
        }
    }
}
