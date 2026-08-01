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
        decl.ConfirmKvgs(supId, now.AddMinutes(2));

        decl.Status.Should().Be(DeclarationStatus.KVGSConfirmed);
        decl.KvgsConfirmedByUserId.Should().Be(supId);
        decl.KvgsConfirmedAt.Should().Be(now.AddMinutes(2));
    }

    [Fact]
    public void UnExit_on_kvgs_confirmed_declaration_throws_locked()
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
        decl.ConfirmKvgs(Guid.NewGuid(), now.AddMinutes(2));

        var act = () => v.UnExit();

        act.Should().Throw<DomainException>().Which.Code.Should().Be("DECLARATION_LOCKED");
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

        var act = () => decl.ConfirmKvgs(Guid.NewGuid(), now);
        act.Should().Throw<DomainException>()
            .Which.Code.Should().Be("DECLARATION_NOT_READY_FOR_KVGS");
    }

    [Fact]
    public void ConfirmKvgs_by_the_exit_gate_operator_succeeds()
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

        // Business rules § B.2: the exit-gate officer ticks cột J then cột L. INV-6 no longer
        // restricts the role — only the AllExited precondition stands.
        var operatorId = Guid.NewGuid();
        decl.ConfirmKvgs(operatorId, now.AddMinutes(2));

        decl.Status.Should().Be(DeclarationStatus.KVGSConfirmed);
        decl.KvgsConfirmedByUserId.Should().Be(operatorId);
    }
}
