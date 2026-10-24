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
    public void Should_refuse_to_rotate_secrets_when_existing_file_is_unreadable()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"b2-secrets-{Guid.NewGuid():N}");
        var keys = Path.Combine(dir, "keys");
        Directory.CreateDirectory(keys);

        // Write corrupt content to the platform-appropriate secrets file.
        // On Windows the store checks backup-secrets.dpapi; on non-Windows it checks backup-secrets.dev.json.
        string corruptFilePath;
        if (OperatingSystem.IsWindows())
        {
            corruptFilePath = Path.Combine(keys, "backup-secrets.dpapi");
            File.WriteAllBytes(corruptFilePath, [0xDE, 0xAD, 0xBE, 0xEF]); // invalid DPAPI blob
        }
        else
        {
            corruptFilePath = Path.Combine(keys, "backup-secrets.dev.json");
            File.WriteAllText(corruptFilePath, "{ this is not valid json");
        }

        try
        {
            var act = () => new BackupSecretsStore(dir).GetOrCreate();
            act.Should().Throw<DomainException>()
                .Which.Code.Should().Be("BACKUP_SECRETS_UNREADABLE");
            File.Exists(corruptFilePath).Should().BeTrue("corrupt file must not be deleted or overwritten");
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

    [Fact]
    [Trait("Issue", "169")]
    public void Should_replace_the_secrets_file_atomically_without_leaving_a_temp_file()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"b2-secrets-{Guid.NewGuid():N}");
        try
        {
            var store = new BackupSecretsStore(dir);
            var created = store.GetOrCreate();
            store.MarkAcknowledged();

            var keys = Path.Combine(dir, "keys");
            Directory.GetFiles(keys, "*.tmp").Should().BeEmpty("writes go to a temp file renamed over the target");
            var reloaded = new BackupSecretsStore(dir).GetOrCreate();
            reloaded.InstallPassword.Should().Be(created.InstallPassword);
            reloaded.AcknowledgedAtInstall.Should().BeTrue();
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}
