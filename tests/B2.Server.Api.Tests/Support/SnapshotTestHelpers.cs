using B2.Application.Abstractions;

namespace B2.Server.Api.Tests.Support;

/// <summary>Byte-array conveniences over the streaming F-15 snapshot APIs (ADR-0011).</summary>
public static class SnapshotTestHelpers
{
    public static async Task<byte[]> CreateAsync(IBackupSnapshotService service, string installPassword, string recoveryCode)
    {
        using var output = new MemoryStream();
        await service.CreateSnapshotAsync(installPassword, recoveryCode, output, CancellationToken.None);
        return output.ToArray();
    }

    public static async Task<byte[]> DecryptAsync(IBackupSnapshotService service, byte[] container, string secret)
    {
        using var input = new MemoryStream(container, writable: false);
        using var output = new MemoryStream();
        await service.DecryptSnapshotAsync(input, secret, output, CancellationToken.None);
        return output.ToArray();
    }

    public static Task StageBytesAsync(this IDatabaseRestoreService service, byte[] databaseBytes, RestoreStagingMetadata metadata) =>
        service.StageAsync(
            async (stream, ct) => await stream.WriteAsync(databaseBytes, ct),
            metadata,
            CancellationToken.None);
}
