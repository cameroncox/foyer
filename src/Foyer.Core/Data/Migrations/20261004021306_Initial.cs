using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Foyer.Core.Data.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Categories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false, collation: "NOCASE"),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    IsSystem = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Categories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Bookmarks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Source = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Url = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: false),
                    Icon = table.Column<string>(type: "TEXT", nullable: true),
                    CategoryId = table.Column<int>(type: "INTEGER", nullable: false),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    DockerHost = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    ContainerName = table.Column<string>(type: "TEXT", maxLength: 255, nullable: true),
                    ContainerState = table.Column<string>(type: "TEXT", maxLength: 32, nullable: true),
                    Health = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    IsPresent = table.Column<bool>(type: "INTEGER", nullable: false),
                    LabelCategory = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    LabelTags = table.Column<string>(type: "TEXT", nullable: false),
                    CategoryOverridden = table.Column<bool>(type: "INTEGER", nullable: false),
                    TagsOverridden = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Bookmarks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Bookmarks_Categories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "Categories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "BookmarkTags",
                columns: table => new
                {
                    BookmarkId = table.Column<int>(type: "INTEGER", nullable: false),
                    Tag = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false, collation: "NOCASE")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BookmarkTags", x => new { x.BookmarkId, x.Tag });
                    table.ForeignKey(
                        name: "FK_BookmarkTags_Bookmarks_BookmarkId",
                        column: x => x.BookmarkId,
                        principalTable: "Bookmarks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "Categories",
                columns: new[] { "Id", "IsSystem", "Name", "SortOrder" },
                values: new object[] { 1, true, "Uncategorized", 0 });

            migrationBuilder.CreateIndex(
                name: "IX_Bookmarks_CategoryId_SortOrder",
                table: "Bookmarks",
                columns: new[] { "CategoryId", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_Bookmarks_DockerHost_ContainerName",
                table: "Bookmarks",
                columns: new[] { "DockerHost", "ContainerName" },
                unique: true,
                filter: "\"DockerHost\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Categories_Name",
                table: "Categories",
                column: "Name",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BookmarkTags");

            migrationBuilder.DropTable(
                name: "Bookmarks");

            migrationBuilder.DropTable(
                name: "Categories");
        }
    }
}
