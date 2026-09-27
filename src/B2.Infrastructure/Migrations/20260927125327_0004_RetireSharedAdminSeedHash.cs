using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace B2.Infrastructure.Migrations
{
    /// <summary>
    /// F-01 — retires the initial Admin password that 0001_BaselineSchema seeded identically
    /// into every install. The seeded hash is replaced by a well-formed BCrypt hash of a
    /// random value that was discarded when this migration was written, so no password
    /// matches it; the per-install password from the installer is applied right after
    /// migrations (AdminPasswordBootstrapper). Only the untouched seed is matched: an Admin
    /// who already changed their password, or was reset by another Admin, is left alone.
    /// </summary>
    public partial class RetireSharedAdminSeedHash0004 : Migration
    {
        public const string SeedAdminId = "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa";
        public const string SharedSeedHash = "$2a$10$DC08sVNrW.UQgIG3Yspllu6ugYvMu8VPWtkLPYROYKs.er.3h.eWy";

        // Kept literal (migrations are frozen snapshots); a test pins it to
        // AdminPasswordBootstrapper.RetiredSeedPlaceholderHash.
        public const string UnusablePlaceholderHash = "$2a$10$Rc6l/bZiNu2wd84mpsm8Q.90Xgwy433DRX6qrg62WdrnKgXqAcZxa";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                $"UPDATE \"Users\" SET \"PasswordHash\" = '{UnusablePlaceholderHash}' " +
                $"WHERE upper(\"Id\") = upper('{SeedAdminId}') AND \"PasswordHash\" = '{SharedSeedHash}';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Deliberately irreversible: restoring a password shared by every install would
            // reopen the hole this migration closes.
        }
    }
}
