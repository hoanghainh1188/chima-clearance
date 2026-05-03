using B2.Server.Api.Middleware;
using FluentAssertions;

namespace B2.Server.Api.Tests.Middleware;

public sealed class CorrelationIdMiddlewareTests
{
    [Theory]
    [InlineData("00-0af7651916cd43dd8448eb211c80319c-b7ad6b7169203331-01", "0af7651916cd43dd8448eb211c80319c")]
    [InlineData("00-0af7651916cd43dd8448eb211c80319c-b7ad6b7169203331-00", "0af7651916cd43dd8448eb211c80319c")]
    public void TryExtractTraceId_returns_traceId_for_valid_traceparent(string header, string expected)
    {
        CorrelationIdMiddleware.TryExtractTraceId(header, out var traceId).Should().BeTrue();
        traceId.Should().Be(expected);
    }

    [Theory]
    [InlineData("")]
    [InlineData("00-00000000000000000000000000000000-b7ad6b7169203331-00")] // all-zero invalid
    [InlineData("00-shorttrace-b7ad6b7169203331-00")] // wrong length
    [InlineData("00-0AF7651916CD43DD8448EB211C80319C-b7ad6b7169203331-00")] // not lowercase
    [InlineData("not-a-traceparent")]
    public void TryExtractTraceId_returns_false_for_invalid(string header)
    {
        CorrelationIdMiddleware.TryExtractTraceId(header, out _).Should().BeFalse();
    }
}
