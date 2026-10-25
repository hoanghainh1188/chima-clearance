using B2.Desktop.Services;
using FluentAssertions;

namespace B2.Desktop.Tests.Exit;

/// <summary>#192 — what the exit-gate officer types becomes the 3–4 digit suffix the server accepts.</summary>
public sealed class ExitSearchQueryTests
{
    [Theory]
    [Trait("Feature", "F-07")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Empty_input_lists_every_vehicle_in_the_gate(string? raw)
    {
        var query = ExitSearchQuery.Parse(raw);

        query.Should().NotBeNull();
        query!.IsListAll.Should().BeTrue();
        query.Adjusted.Should().BeFalse();
    }

    [Theory]
    [Trait("Feature", "F-07")]
    [InlineData("5")]
    [InlineData("57")]
    [InlineData("29H")]
    public void One_or_two_digits_waits_for_more_input(string raw)
    {
        ExitSearchQuery.Parse(raw).Should().BeNull();
    }

    [Theory]
    [Trait("Feature", "F-07")]
    [InlineData("576", "576")]
    [InlineData(" 5769 ", "5769")]
    public void Three_or_four_digits_are_searched_as_typed(string raw, string expected)
    {
        var query = ExitSearchQuery.Parse(raw)!;

        query.Suffix.Should().Be(expected);
        query.Adjusted.Should().BeFalse();
    }

    [Theory]
    [Trait("Feature", "F-07")]
    [InlineData("12345", "2345")]
    [InlineData("29H-123.45", "2345")]
    [InlineData("12H05769", "5769")]
    [InlineData("57.69", "5769")]
    public void Longer_numbers_and_whole_plates_search_the_last_four_digits_and_say_so(string raw, string expected)
    {
        var query = ExitSearchQuery.Parse(raw)!;

        query.Suffix.Should().Be(expected);
        query.Adjusted.Should().BeTrue();
        query.IsListAll.Should().BeFalse();
    }
}
