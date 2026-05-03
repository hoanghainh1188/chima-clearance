using System.Net.Http.Headers;

namespace B2.Desktop.Services.Api;

public sealed class AuthTokenHandler : DelegatingHandler
{
    public string? AccessToken { get; set; }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrEmpty(AccessToken))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", AccessToken);
        }

        return base.SendAsync(request, cancellationToken);
    }
}
