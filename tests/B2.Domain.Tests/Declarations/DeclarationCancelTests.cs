using System.Globalization;
using B2.Domain;
using B2.Domain.Declarations;
using B2.Domain.Users;
using FluentAssertions;

namespace B2.Domain.Tests.Declarations;

public sealed class DeclarationCancelTests
{
    private static readonly DateTimeOffset Now =
        DateTimeOffset.Parse("2026-05-04T10:00:00Z", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal);

    private static Declaration NewOpenDeclaration(out Vehicle vehicle)
    {
        var decl = Declaration.CreateNew(
            Guid.NewGuid(),
            DeclarationNumber.Parse("123456789012"),
            Guid.NewGuid(),
            "Co",
            "Hang",
            Guid.NewGuid(),
            Guid.NewGuid(),
            Now,
            DateOnly.Parse("2026-05-04", CultureInfo.InvariantCulture));
        vehicle = decl.AddVehicle(Guid.NewGuid(), PlateNumber.Parse("29A12345"), Now, Guid.NewGuid(), null, null);
        return decl;
    }

    [Fact]
    public void Cancel_when_open_sets_status_and_audit_fields()
    {
        var decl = NewOpenDeclaration(out _);
        var adminId = Guid.NewGuid();

        decl.Cancel(Role.Admin, adminId, "  Nhập nhầm  ", Now.AddMinutes(1));

        decl.Status.Should().Be(DeclarationStatus.Cancelled);
        decl.CancelledByUserId.Should().Be(adminId);
        decl.CancelledAt.Should().Be(Now.AddMinutes(1));
        decl.CancellationReason.Should().Be("Nhập nhầm");
    }

    [Fact]
    public void Cancel_when_not_admin_throws_forbidden()
    {
        var decl = NewOpenDeclaration(out _);

        var act = () => decl.Cancel(Role.Supervisor, Guid.NewGuid(), "Lý do", Now);
        act.Should().Throw<DomainException>().Which.Code.Should().Be("FORBIDDEN");
    }

    [Fact]
    public void Cancel_without_reason_throws()
    {
        var decl = NewOpenDeclaration(out _);

        var act = () => decl.Cancel(Role.Admin, Guid.NewGuid(), "   ", Now);
        act.Should().Throw<DomainException>().Which.Code.Should().Be("CANCELLATION_REASON_REQUIRED");
    }

    [Fact]
    public void Cancel_when_not_open_throws_not_cancellable()
    {
        var decl = NewOpenDeclaration(out var vehicle);
        vehicle.MarkExit(Guid.NewGuid(), Now.AddMinutes(1));
        decl.RecomputeStatus();
        decl.Status.Should().Be(DeclarationStatus.AllExited);

        var act = () => decl.Cancel(Role.Admin, Guid.NewGuid(), "Quá muộn", Now.AddMinutes(2));
        act.Should().Throw<DomainException>().Which.Code.Should().Be("DECLARATION_NOT_CANCELLABLE");
    }

    [Fact]
    public void MarkExit_on_cancelled_declaration_throws()
    {
        var decl = NewOpenDeclaration(out var vehicle);
        decl.Cancel(Role.Admin, Guid.NewGuid(), "Hủy", Now.AddMinutes(1));

        var act = () => vehicle.MarkExit(Guid.NewGuid(), Now.AddMinutes(2));
        act.Should().Throw<DomainException>().Which.Code.Should().Be("DECLARATION_CANCELLED");
    }

    [Fact]
    public void UnExit_on_cancelled_declaration_throws()
    {
        var decl = NewOpenDeclaration(out var exited);
        decl.AddVehicle(Guid.NewGuid(), PlateNumber.Parse("29A99999"), Now, Guid.NewGuid(), null, null);
        exited.MarkExit(Guid.NewGuid(), Now.AddMinutes(1));
        decl.Status.Should().Be(DeclarationStatus.Open);
        decl.Cancel(Role.Admin, Guid.NewGuid(), "Hủy", Now.AddMinutes(2));

        var act = () => exited.UnExit();
        act.Should().Throw<DomainException>().Which.Code.Should().Be("DECLARATION_CANCELLED");
    }
}
