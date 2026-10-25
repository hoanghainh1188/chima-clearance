using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using B2.Application.Abstractions;
using B2.Server.Api.Hosting;
using B2.Server.Api.Tests.Support;
using B2.Shared.Contracts.Declarations;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace B2.Server.Api.Tests.Admin;

/// <summary>Bug #173 — the host must come back up after a restore is staged.</summary>
public sealed class ServiceRestartTests
{
    private const string InstallPassword = "install-secret-Password-1!";
    private const string RecoveryCode = "B2-RECOVERY-CODE-TEST-9999";

    [Fact]
    [Trait("Feature", "F-15")]
    public async Task Should_request_restart_after_staging_a_restore()
    {
        await using var factory = new B2ApiFactory();
        var client = factory.CreateClient();
        var admin = await client.LoginAsAdminAsync();
        client.SetBearer(admin.AccessToken);
        var create = await client.PostAsJsonAsync(
            "/api/declarations",
            new DeclarationCreateRequest(
                [TestDeclarationNumbers.Next()],
                SeedIds.ProcessorId,
                "Co",
                "Hang",
                SeedIds.YardId,
                new VehicleAddRequest("31A60001", null, null)),
            TestJson.Options);
        create.EnsureSuccessStatusCode();

        var snapshots = factory.Services.GetRequiredService<IBackupSnapshotService>();
        var container = await SnapshotTestHelpers.CreateAsync(snapshots, InstallPassword, RecoveryCode);

        using var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(container);
        file.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        form.Add(file, "file", "b2-snapshot.bak");
        form.Add(new StringContent(InstallPassword), "password");

        var response = await client.PostAsync("/api/admin/restore", form);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        factory.Restarter.Requests.Should().Be(1);
    }

    [Fact]
    [Trait("Feature", "F-15")]
    public async Task Should_not_request_restart_when_restore_is_rejected()
    {
        await using var factory = new B2ApiFactory();
        var client = factory.CreateClient();
        var admin = await client.LoginAsAdminAsync();
        client.SetBearer(admin.AccessToken);

        using var form = new MultipartFormDataContent();
        form.Add(new StringContent(InstallPassword), "password");

        var response = await client.PostAsync("/api/admin/restore", form);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        factory.Restarter.Requests.Should().Be(0);
    }

    [Fact]
    [Trait("Feature", "F-15")]
    public async Task Should_stop_the_host_after_the_response_flush_delay()
    {
        var lifetime = new FakeLifetime();
        new HostShutdownRestarter(lifetime).RequestRestart();

        lifetime.Stopped.IsCompleted.Should().BeFalse("the HTTP 200 must be flushed before stopping");
        // The restarter's 1 s delay runs on the thread pool, which the Windows runner starves while
        // dozens of WebApplicationFactory hosts run in parallel — 10 s flaked on main (run 40198035879).
        // The bound only catches "never stops"; the pass case still finishes in ~1 s.
        var finished = await Task.WhenAny(lifetime.Stopped, Task.Delay(TimeSpan.FromSeconds(60)));
        finished.Should().BeSameAs(lifetime.Stopped);
    }

    [Fact]
    [Trait("Feature", "F-15")]
    public void Should_wait_for_the_old_process_before_retrying_service_start()
    {
        var psi = WindowsServiceRestarter.BuildHelperStartInfo(4242, WindowsServiceRestarter.ServiceName);

        psi.FileName.Should().Be("powershell.exe");
        psi.UseShellExecute.Should().BeFalse();
        psi.ArgumentList.Should().StartWith(["-NoProfile", "-NonInteractive", "-Command"]);

        var script = psi.ArgumentList[^1];
        script.Should().StartWith("Wait-Process -Id 4242 ");
        script.IndexOf("Start-Service -Name 'B2ClearanceService'", StringComparison.Ordinal)
            .Should().BeGreaterThan(script.IndexOf("Wait-Process", StringComparison.Ordinal));
        script.Should().Contain($"$i -lt {WindowsServiceRestarter.StartAttempts}")
            .And.Contain($"Start-Sleep -Seconds {WindowsServiceRestarter.StartRetryDelaySeconds}");
    }

    private sealed class FakeLifetime : IHostApplicationLifetime
    {
        private readonly TaskCompletionSource _stopped = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task Stopped => _stopped.Task;

        public CancellationToken ApplicationStarted => CancellationToken.None;

        public CancellationToken ApplicationStopping => CancellationToken.None;

        public CancellationToken ApplicationStopped => CancellationToken.None;

        public void StopApplication() => _stopped.TrySetResult();
    }
}
