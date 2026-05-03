using System.Text.Json;
using System.Text.Json.Serialization;

namespace B2.Server.Api.Tests.Support;

public static class TestJson
{
    /// <summary>Matches <see cref="Microsoft.AspNetCore.Http.Json.JsonOptions"/> in <c>B2ApiHost</c> (PascalCase enum strings).</summary>
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
    };
}
