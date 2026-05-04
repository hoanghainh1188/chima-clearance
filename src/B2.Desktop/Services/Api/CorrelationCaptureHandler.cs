namespace B2.Desktop.Services.Api;

/// <summary>
/// Reads the <c>X-Correlation-Id</c> header off every API response and forwards
/// it to <see cref="ISessionCorrelationService"/> so the status bar (wireframe
/// § 5) reflects the most recent server-known correlation id. The header name
/// matches <c>CorrelationIdMiddleware.LegacyHeader</c> on the server.
/// </summary>
public sealed class CorrelationCaptureHandler : DelegatingHandler
{
    public const string CorrelationHeader = "X-Correlation-Id";

    private readonly ISessionCorrelationService _correlation;

    public CorrelationCaptureHandler(ISessionCorrelationService correlation)
    {
        _correlation = correlation;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (response.Headers.TryGetValues(CorrelationHeader, out var values))
        {
            _correlation.Capture(values.FirstOrDefault());
        }

        return response;
    }
}
