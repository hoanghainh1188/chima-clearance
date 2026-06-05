using B2.Domain;
using B2.Infrastructure.Backup;
using FluentAssertions;

namespace B2.Server.Api.Tests.Admin;

public sealed class BackupSecretsTests
{
    [Fact]
    [Trait("Requirement", "FR-026")]
    public void Should_generate_unambiguous_base32_recovery_codes()
    {
        var code = RecoveryCodeGenerator.Generate(24);

        code.Should().HaveLength(24);
        code.Should().MatchRegex("^[0-9A-HJKMNP-TV-Z]+$", "Crockford base32 excludes I/L/O/U");
        RecoveryCodeGenerator.Generate(24).Should().NotBe(code, "codes are random");
    }

    [Fact]
    [Trait("Requirement", "FR-026")]
    public void Should_persist_and_reload_the_same_secrets()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"b2-secrets-{Guid.NewGuid():N}");
        try
        {
            var first = new BackupSecretsStore(dir).GetOrCreate();
            first.InstallPassword.Should().NotBeNullOrEmpty();
            first.RecoveryCode.Should().NotBeNullOrEmpty();
            first.InstallPassword.Should().NotBe(first.RecoveryCode);
            first.AcknowledgedAtInstall.Should().BeFalse();

            // A fresh store over the same directory reads the persisted pair, not a new one.
            var reloaded = new BackupSecretsStore(dir).GetOrCreate();
            reloaded.InstallPassword.Should().Be(first.InstallPassword);
            reloaded.RecoveryCode.Should().Be(first.RecoveryCode);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    [Trait("Requirement", "FR-026")]
    public void Should_fail_instead_of_rotating_when_dev_secrets_file_is_corrupt()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"b2-secrets-{Guid.NewGuid():N}");
        var devPath = Path.Combine(dir, "keys", "backup-secrets.dev.json");
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(devPath)!);
            File.WriteAllText(devPath, "not-valid-json{{{");

            var act = () => new BackupSecretsStore(dir).GetOrCreate();

            act.Should().Throw<DomainException>()
                .Which.Code.Should().Be("BACKUP_SECRETS_UNREADABLE");
            File.ReadAllText(devPath).Should().Be("not-valid-json{{{", "corrupt file must not be overwritten with new secrets");
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    [Trait("Requirement", "FR-026")]
    public void Should_mark_acknowledged_and_persist_it()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"b2-secrets-{Guid.NewGuid():N}");
        try
        {
            var store = new BackupSecretsStore(dir);
            var before = store.GetOrCreate();
            store.MarkAcknowledged();

            var reloaded = new BackupSecretsStore(dir).GetOrCreate();
            reloaded.AcknowledgedAtInstall.Should().BeTrue();
            reloaded.InstallPassword.Should().Be(before.InstallPassword, "acknowledging must not rotate the secrets");
            reloaded.RecoveryCode.Should().Be(before.RecoveryCode);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}
