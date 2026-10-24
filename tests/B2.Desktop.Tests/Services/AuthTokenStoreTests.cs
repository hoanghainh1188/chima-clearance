using B2.Desktop.Services.Api;
using FluentAssertions;

namespace B2.Desktop.Tests.Services;

public sealed class AuthTokenStoreTests
{
    [Fact]
    public void Should_initialize_empty()
    {
        var store = new AuthTokenStore();
        store.AccessToken.Should().BeNull();
        store.RefreshToken.Should().BeNull();
    }

    [Fact]
    public void Should_store_and_retrieve_tokens()
    {
        var store = new AuthTokenStore();
        store.AccessToken = "access-123";
        store.RefreshToken = "refresh-456";

        store.AccessToken.Should().Be("access-123");
        store.RefreshToken.Should().Be("refresh-456");
    }

    [Fact]
    public void Should_clear_all_tokens()
    {
        var store = new AuthTokenStore
        {
            AccessToken = "access-123",
            RefreshToken = "refresh-456"
        };

        store.Clear();

        store.AccessToken.Should().BeNull();
        store.RefreshToken.Should().BeNull();
    }
}
