using B2.Infrastructure.Backup;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace B2.Server.Api.Tests.Admin;

public sealed class LitestreamTests
{
    [Fact]
    [Trait("Feature", "F-15")]
    public void Should_render_yaml_for_each_replica_target()
    {
        var options = new LitestreamOptions { DbPath = @"C:\ProgramData\B2\db\b2.db" };
        options.Replicas.Add(new LitestreamReplica("usb", @"D:\B2-Backup\replica"));
        options.Replicas.Add(new LitestreamReplica("lan", @"\\entry-pc\B2-Backup"));

        var yaml = LitestreamConfigWriter.ToYaml(options);

        yaml.Should().Contain("path: C:\\ProgramData\\B2\\db\\b2.db");
        yaml.Should().Contain("name: usb").And.Contain("path: D:\\B2-Backup\\replica");
        yaml.Should().Contain("name: lan").And.Contain(@"path: \\entry-pc\B2-Backup");
    }

    [Fact]
    [Trait("Feature", "F-15")]
    public async Task Should_no_op_when_disabled()
    {
        using var supervisor = new LitestreamSupervisor(
            new LitestreamOptions { Enabled = false },
            Path.GetTempPath(),
            NullLogger<LitestreamSupervisor>.Instance);

        await supervisor.StartAsync(CancellationToken.None);
        supervisor.IsActive.Should().BeFalse();
        await supervisor.StopAsync(CancellationToken.None);
    }

    [Fact]
    [Trait("Feature", "F-15")]
    public async Task Should_no_op_when_binary_is_missing()
    {
        var options = new LitestreamOptions
        {
            Enabled = true,
            ExecutablePath = Path.Combine(Path.GetTempPath(), $"litestream-{Guid.NewGuid():N}.exe"),
            DbPath = @"C:\ProgramData\B2\db\b2.db",
        };
        options.Replicas.Add(new LitestreamReplica("usb", @"D:\B2-Backup"));

        using var supervisor = new LitestreamSupervisor(options, Path.GetTempPath(), NullLogger<LitestreamSupervisor>.Instance);

        await supervisor.StartAsync(CancellationToken.None);
        supervisor.IsActive.Should().BeFalse("a missing binary must not start the loop or crash the host");
        await supervisor.StopAsync(CancellationToken.None);
    }

    private static IConfigurationSection Section(Dictionary<string, string?> values) =>
        new ConfigurationBuilder().AddInMemoryCollection(values).Build().GetSection("B2:Litestream");

    [Fact]
    [Trait("Feature", "F-15")]
    [Trait("Requirement", "FR-012")]
    public void Windows_without_config_enables_a_local_replica_in_the_data_directory()
    {
        var dataDir = Path.Combine("C:", "ProgramData", "B2");

        var options = LitestreamOptions.FromConfiguration(Section(new()), dataDir, isWindows: true);

        options.Enabled.Should().BeTrue("the gate site has no IT staff to write a config");
        options.ExecutablePath.Should().Be(Path.Combine(dataDir, "litestream.exe"));
        options.DbPath.Should().Be(Path.Combine(dataDir, "db", "b2.db"));
        options.Replicas.Should().ContainSingle()
            .Which.Should().Be(new LitestreamReplica(
                LitestreamOptions.DefaultReplicaName, Path.Combine(dataDir, "backup", "replica")));
    }

    [Fact]
    [Trait("Feature", "F-15")]
    public void Dev_machines_stay_disabled_without_config()
    {
        var options = LitestreamOptions.FromConfiguration(Section(new()), "/tmp/b2", isWindows: false);

        options.Enabled.Should().BeFalse();
    }

    [Fact]
    [Trait("Feature", "F-15")]
    [Trait("Requirement", "FR-012")]
    public void Configuration_overrides_the_defaults()
    {
        var options = LitestreamOptions.FromConfiguration(
            Section(new()
            {
                ["B2:Litestream:Enabled"] = "false",
                ["B2:Litestream:Replicas:0:Name"] = "usb",
                ["B2:Litestream:Replicas:0:Path"] = @"E:\B2-Backup",
            }),
            @"C:\ProgramData\B2",
            isWindows: true);

        options.Enabled.Should().BeFalse();
        options.Replicas.Should().ContainSingle().Which.Name.Should().Be("usb");
    }
}
