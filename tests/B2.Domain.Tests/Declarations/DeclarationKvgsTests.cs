using System.Globalization;
using B2.Domain;
using B2.Domain.Declarations;
using B2.Domain.Users;
using FluentAssertions;

namespace B2.Domain.Tests.Declarations;

public sealed class DeclarationKvgsTests
{
    [Fact]
    public void ConfirmKvgs_when_all_exited_sets_status_and_audit_fields()
    {
        var now = DateTimeOffset.Parse("2026-05-04T10:00:00Z", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal);
        var decl = Declaration.CreateNew(
            Guid.NewGuid(),
            DeclarationNumber.Parse("123456789012"),
            Guid.NewGuid(),
            "Co",
            "Hang",
            Guid.NewGuid(),
            Guid.NewGuid(),
            now,
            DateOnly.Parse("2026-05-04", CultureInfo.InvariantCulture));
        var v = decl.AddVehicle(
            Guid.NewGuid(),
            PlateNumber.Parse("29A12345"),
            now,
            Guid.NewGuid(),
            null,
            null);
        v.MarkExit(Guid.NewGuid(), now.AddMinutes(1));
        decl.RecomputeStatus();
        decl.Status.Should().Be(DeclarationStatus.AllExited);

        var supId = Guid.NewGuid();
        decl.ConfirmKvgs(Role.Supervisor, supId, now.AddMinutes(2));

        decl.Status.Should().Be(DeclarationStatus.KVGSConfirmed);
        decl.KvgsConfirmedByUserId.Should().Be(supId);
        decl.KvgsConfirmedAt.Should().Be(now.AddMinutes(2));
    }

    [Fact]
    public void ConfirmKvgs_when_open_throws_not_ready()
    {
        var now = DateTimeOffset.Parse("2026-05-04T10:00:00Z", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal);
        var decl = Declaration.CreateNew(
            Guid.NewGuid(),
            DeclarationNumber.Parse("123456789012"),
            Guid.NewGuid(),
            "Co",
            "Hang",
            Guid.NewGuid(),
            Guid.NewGuid(),
            now,
            DateOnly.Parse("2026-05-04", CultureInfo.InvariantCulture));
        decl.AddVehicle(Guid.NewGuid(), PlateNumber.Parse("29A12345"), now, Guid.NewGuid(), null, null);

        var act = () => decl.ConfirmKvgs(Role.Admin, Guid.NewGuid(), now);
        act.Should().Throw<DomainException>()
            .Which.Code.Should().Be("DECLARATION_NOT_READY_FOR_KVGS");
    }

    [Fact]
    public void ConfirmKvgs_when_operator_throws_forbidden()
    {
        var now = DateTimeOffset.Parse("2026-05-04T10:00:00Z", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal);
        var decl = Declaration.CreateNew(
            Guid.NewGuid(),
            DeclarationNumber.Parse("123456789012"),
            Guid.NewGuid(),
            "Co",
            "Hang",
            Guid.NewGuid(),
            Guid.NewGuid(),
            now,
            DateOnly.Parse("2026-05-04", CultureInfo.InvariantCulture));
        var v = decl.AddVehicle(Guid.NewGuid(), PlateNumber.Parse("29A12345"), now, Guid.NewGuid(), null, null);
        v.MarkExit(Guid.NewGuid(), now.AddMinutes(1));
        decl.RecomputeStatus();

        var act = () => decl.ConfirmKvgs(Role.Operator, Guid.NewGuid(), now.AddMinutes(2));
        act.Should().Throw<DomainException>().Which.Code.Should().Be("FORBIDDEN");
    }
}
