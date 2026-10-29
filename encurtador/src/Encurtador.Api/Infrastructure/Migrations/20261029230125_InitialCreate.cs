using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Encurtador.Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Links",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Code = table.Column<string>(type: "TEXT", nullable: false),
                    TargetUrl = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: false),
                    IsCustomCode = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    AccessCount = table.Column<long>(type: "INTEGER", nullable: false, defaultValue: 0L),
                    ManagementKeyHash = table.Column<byte[]>(type: "BLOB", maxLength: 32, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Links", x => x.Id);
                    table.CheckConstraint("CK_Links_AccessCount_NonNegative", "AccessCount >= 0");
                    table.CheckConstraint("CK_Links_Code_Length", "length(Code) BETWEEN 4 AND 20");
                    table.CheckConstraint("CK_Links_ManagementKeyHash_Length", "length(ManagementKeyHash) = 32");
                    table.CheckConstraint("CK_Links_TargetUrl_Length", "length(TargetUrl) <= 2048");
                });

            migrationBuilder.CreateIndex(
                name: "IX_Links_Code",
                table: "Links",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Links_ManagementKeyHash",
                table: "Links",
                column: "ManagementKeyHash",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Links");
        }
    }
}
