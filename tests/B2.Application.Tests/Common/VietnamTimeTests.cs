using B2.Application.Common;
using FluentAssertions;

namespace B2.Application.Tests.Common;

public sealed class VietnamTimeTests
{
    [Fact]
    [Trait("Feature", "F-12")]
    public void Utc_range_for_business_date_aligns_with_business_date_from_utc()
    {
        var date = new DateOnly(2026, 5, 24);
        var (start, end) = VietnamTime.UtcRangeForBusinessDate(date);

        start.Should().Be(new DateTimeOffset(2026, 5, 23, 17, 0, 0, TimeSpan.Zero));
        end.Should().Be(new DateTimeOffset(2026, 5, 24, 17, 0, 0, TimeSpan.Zero));

        VietnamTime.BusinessDateFromUtc(start).Should().Be(date);
        VietnamTime.BusinessDateFromUtc(end.AddTicks(-1)).Should().Be(date);
        VietnamTime.BusinessDateFromUtc(end).Should().Be(new DateOnly(2026, 5, 25));
    }
}
