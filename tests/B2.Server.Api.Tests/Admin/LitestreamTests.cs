using B2.Infrastructure.Backup;
using FluentAssertions;
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
}
