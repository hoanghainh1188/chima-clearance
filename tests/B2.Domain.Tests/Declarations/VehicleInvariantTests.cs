using System.Globalization;
using System.Runtime.CompilerServices;
using B2.Domain;
using B2.Domain.Declarations;
using FluentAssertions;

namespace B2.Domain.Tests.Declarations;

public sealed class VehicleInvariantTests
{
    private static readonly DateTimeOffset Now =
        DateTimeOffset.Parse("2026-05-04T10:00:00Z", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal);

    [Fact]
    [Trait("Issue", "183")]
    public void UpdateNote_on_kvgs_confirmed_declaration_throws_locked()
    {
        var decl = NewDeclaration();
        var v = decl.AddVehicle(Guid.NewGuid(), PlateNumber.Parse("29A12345"), Now, Guid.NewGuid(), null, null);
        v.MarkExit(Guid.NewGuid(), Now.AddMinutes(1));
        decl.RecomputeStatus();
        decl.ConfirmKvgs(Guid.NewGuid(), Now.AddMinutes(2));

        var act = () => v.UpdateNote("xe hỏng");

        act.Should().Throw<DomainException>().Which.Code.Should().Be("DECLARATION_LOCKED");
        v.Note.Should().BeNull();
    }

    [Fact]
    [Trait("Issue", "183")]
    public void UpdateNote_on_open_declaration_is_allowed()
    {
        var decl = NewDeclaration();
        var v = decl.AddVehicle(Guid.NewGuid(), PlateNumber.Parse("29A12345"), Now, Guid.NewGuid(), null, null);

        v.UpdateNote("  xe hỏng  ");

        v.Note.Should().Be("xe hỏng");
    }

    [Fact]
    [Trait("Issue", "139")]
    public void Mutators_throw_when_declaration_navigation_is_not_loaded()
    {
        // Mimics an EF load without .Include(v => v.Declaration): the navigation stays null.
        var detached = (Vehicle)RuntimeHelpers.GetUninitializedObject(typeof(Vehicle));

        detached.Invoking(v => v.MarkExit(Guid.NewGuid(), Now)).Should().Throw<InvalidOperationException>();
        detached.Invoking(v => v.UnExit()).Should().Throw<InvalidOperationException>();
        detached.Invoking(v => v.UpdateNote("x")).Should().Throw<InvalidOperationException>();
    }

    private static Declaration NewDeclaration() => Declaration.CreateNew(
        Guid.NewGuid(),
        [DeclarationNumber.Parse("123456789012")],
        Guid.NewGuid(),
        "Co",
        "Hang",
        Guid.NewGuid(),
        Guid.NewGuid(),
        Now,
        DateOnly.Parse("2026-05-04", CultureInfo.InvariantCulture));
}
