using B2.Desktop.Services;
using FluentAssertions;

namespace B2.Desktop.Tests.Services;

/// <summary>#201 — report headers show which column is sorted and which way.</summary>
public sealed class ReportTableSortTests
{
    private static ReportTableSort NewSort() => new("▲", "▼");

    [Fact]
    [Trait("Feature", "F-14")]
    public void No_header_shows_a_glyph_before_any_click()
    {
        var sort = NewSort();

        sort["name"].Should().BeEmpty();
        sort.Column.Should().BeNull();
    }

    [Fact]
    [Trait("Feature", "F-14")]
    public void First_click_sorts_ascending_and_the_second_flips_it()
    {
        var sort = NewSort();

        sort.Toggle("name").Should().BeTrue();
        sort["name"].Should().Be("▲");

        sort.Toggle("name").Should().BeFalse();
        sort["name"].Should().Be("▼");
    }

    [Fact]
    [Trait("Feature", "F-14")]
    public void Another_column_starts_ascending_and_clears_the_previous_glyph()
    {
        var sort = NewSort();
        sort.Toggle("name");
        sort.Toggle("name");

        sort.Toggle("total").Should().BeTrue();

        sort["total"].Should().Be("▲");
        sort["name"].Should().BeEmpty();
    }

    [Fact]
    [Trait("Feature", "F-14")]
    public void Reset_clears_the_indicator()
    {
        var sort = NewSort();
        sort.Toggle("name");

        sort.Reset();

        sort["name"].Should().BeEmpty();
        sort.Toggle("name").Should().BeTrue();
    }

    [Fact]
    [Trait("Feature", "F-14")]
    public void Every_change_notifies_bindings_to_refresh_all_glyphs()
    {
        var sort = NewSort();
        var names = new List<string?>();
        sort.PropertyChanged += (_, e) => names.Add(e.PropertyName);

        sort.Toggle("name");
        sort.Reset();

        names.Should().Equal(string.Empty, string.Empty);
    }
}
