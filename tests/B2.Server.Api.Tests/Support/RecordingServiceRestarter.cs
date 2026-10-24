using B2.Server.Api.Hosting;

namespace B2.Server.Api.Tests.Support;

/// <summary>Counts restart requests instead of stopping the test host.</summary>
public sealed class RecordingServiceRestarter : IServiceRestarter
{
    private int _requests;

    public int Requests => Volatile.Read(ref _requests);

    public void RequestRestart() => Interlocked.Increment(ref _requests);
}
