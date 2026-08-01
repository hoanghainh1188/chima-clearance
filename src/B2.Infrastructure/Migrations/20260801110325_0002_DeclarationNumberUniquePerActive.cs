using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace B2.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class DeclarationNumberUniquePerActive0002 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
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

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Declarations_DeclarationNumber",
                table: "Declarations");

            migrationBuilder.CreateIndex(
                name: "IX_Declarations_DeclarationNumber",
                table: "Declarations",
                column: "DeclarationNumber",
                unique: true);
        }
    }
}
