using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Foyer.Core.Data.Migrations
{
    /// <inheritdoc />
    public partial class DefaultProfile : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Profiles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Slug = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false, collation: "NOCASE"),
                    OwnerUser = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true, collation: "NOCASE"),
                    IsSystem = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsPersonal = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Profiles", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "Profiles",
                columns: new[] { "Id", "CreatedAt", "IsPersonal", "IsSystem", "Name", "OwnerUser", "Slug" },
                values: new object[] { 1, new DateTimeOffset(new DateTime(2026, 10, 5, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), false, true, "Default", null, "default" });

            migrationBuilder.CreateIndex(
                name: "IX_Profiles_OwnerUser_Slug",
                table: "Profiles",
                columns: new[] { "OwnerUser", "Slug" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Profiles");
        }
    }
}
