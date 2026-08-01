using B2.Domain;
using B2.Domain.Declarations;
using FluentAssertions;

namespace B2.Domain.Tests.Declarations;

/// <summary>
/// Cột E: "tự động in hoa chữ cái sau khi nhập liệu (12h05769 => 12H05769)" and the column may be
/// pasted from the unit's internal system. Confirmed with the unit 2026-08-01: a plate carrying
/// separators is normalised, not rejected.
/// </summary>
public sealed class PlateNumberNormalisationTests
{
    [Theory]
    [InlineData("12h05769", "12H05769")]
    [InlineData("29C12345", "29C12345")]
    [InlineData("29c-12345", "29C12345")]
    [InlineData("29C - 12345", "29C12345")]
    [InlineData("  29c.12345  ", "29C12345")]
    [InlineData("29C_12345", "29C12345")]
    [InlineData("51LD-09876", "51LD09876")]
    public void Parse_normalises_case_and_separators(string raw, string expected)
    {
        PlateNumber.Parse(raw).Value.Should().Be(expected);
    }

    [Theory]
    [InlineData("29C-12345", true)]
    [InlineData("29C1234", true)]
    [InlineData("2C-12345", false)]
    [InlineData("29CDE-12345", false)]
    [InlineData("29C-123", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsValid_agrees_with_Parse(string? raw, bool expected)
    {
        PlateNumber.IsValid(raw).Should().Be(expected);

        if (!expected)
        {
            var act = () => PlateNumber.Parse(raw);
            act.Should().Throw<DomainException>().Which.Code.Should().Be("PLATE_NUMBER_INVALID");
        }
    }

    [Theory]
    [InlineData("12rm05769", "12RM05769")]
    [InlineData("12rm-05769", "12RM05769")]
    [InlineData("abcu 123456-7", "ABCU1234567")]
    public void Container_number_is_normalised_the_same_way(string raw, string expected)
    {
        ContainerNumber.TryParse(raw)!.Value.Value.Should().Be(expected);
    }
}
