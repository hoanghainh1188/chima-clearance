using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace B2.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class DeclarationNumbersTable0003 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Declarations_DeclarationNumber",
                table: "Declarations");

            migrationBuilder.CreateTable(
                name: "DeclarationNumbers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    DeclarationId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Number = table.Column<string>(type: "TEXT", maxLength: 12, nullable: false),
                    Ordinal = table.Column<int>(type: "INTEGER", nullable: false),
                    IsCancelled = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeclarationNumbers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DeclarationNumbers_Declarations_DeclarationId",
                        column: x => x.DeclarationId,
                        principalTable: "Declarations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Declarations_DeclarationNumber",
                table: "Declarations",
                column: "DeclarationNumber");

            migrationBuilder.CreateIndex(
                name: "IX_DeclarationNumbers_DeclarationId",
                table: "DeclarationNumbers",
                column: "DeclarationId");

            // Data migration: carry every existing declaration's number into the new table as
            // ordinal 0. Without this, every row already in the ledger would lose its cột C.
            // IsCancelled mirrors the parent so INV-1 stays scoped to declarations that count.
            migrationBuilder.Sql(
                """
                INSERT INTO "DeclarationNumbers" ("Id", "DeclarationId", "Number", "Ordinal", "IsCancelled")
                SELECT
                    lower(
                        hex(randomblob(4)) || '-' ||
                        hex(randomblob(2)) || '-4' ||
                        substr(hex(randomblob(2)), 2) || '-' ||
                        substr('89ab', abs(random()) % 4 + 1, 1) ||
                        substr(hex(randomblob(2)), 2) || '-' ||
                        hex(randomblob(6))
                    ),
                    d."Id",
                    d."DeclarationNumber",
                    0,
                    CASE WHEN d."Status" = 'Cancelled' THEN 1 ELSE 0 END
                FROM "Declarations" AS d;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_DeclarationNumbers_Number",
                table: "DeclarationNumbers",
                column: "Number",
                unique: true,
                filter: "\"IsCancelled\" = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DeclarationNumbers");

            migrationBuilder.DropIndex(
                name: "IX_Declarations_DeclarationNumber",
                table: "Declarations");

            migrationBuilder.CreateIndex(
                name: "IX_Declarations_DeclarationNumber",
                table: "Declarations",
                column: "DeclarationNumber",
                unique: true,
                filter: "\"Status\" <> 'Cancelled'");
        }
    }
}
