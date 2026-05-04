using System.Data;
using B2.Application.Abstractions;
using B2.Application.Declarations.Exit;
using B2.Domain.Declarations;
using Microsoft.EntityFrameworkCore;

namespace B2.Infrastructure.Persistence.Repositories;

public sealed class DeclarationRepository : IDeclarationRepository
{
    private readonly B2DbContext _db;

    public DeclarationRepository(B2DbContext db)
    {
        _db = db;
    }

    public Task<bool> DeclarationNumberExistsAsync(string declarationNumber, CancellationToken cancellationToken) =>
        _db.Declarations.AnyAsync(d => d.DeclarationNumber == declarationNumber, cancellationToken);

    public async Task AddAsync(Declaration declaration, CancellationToken cancellationToken) =>
        await _db.Declarations.AddAsync(declaration, cancellationToken);

    public Task<Declaration?> GetByIdWithVehiclesAsync(Guid id, CancellationToken cancellationToken) =>
        _db.Declarations.Include(d => d.Vehicles).FirstOrDefaultAsync(d => d.Id == id, cancellationToken);

    public Task<Declaration?> GetByDeclarationNumberWithVehiclesAsync(
        string declarationNumber,
        CancellationToken cancellationToken) =>
        _db.Declarations.Include(d => d.Vehicles)
            .FirstOrDefaultAsync(d => d.DeclarationNumber == declarationNumber, cancellationToken);

    public async Task<MarkVehicleExitResult> MarkVehicleExitAsync(
        Guid vehicleId,
        Guid actorUserId,
        DateTimeOffset exitTime,
        CancellationToken cancellationToken)
    {
        await using var tx = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        try
        {
            var vehicle = await _db.Vehicles
                .Include(v => v.Declaration)
                .ThenInclude(d => d!.Vehicles)
                .FirstOrDefaultAsync(v => v.Id == vehicleId, cancellationToken);

            if (vehicle?.Declaration is null)
            {
                await tx.RollbackAsync(cancellationToken);
                return new MarkVehicleExitNotFound();
            }

            if (vehicle.ExitMarked)
            {
                var winnerName = await WinnerNameAsync(vehicle.ExitConfirmedByUserId, cancellationToken);
                await tx.RollbackAsync(cancellationToken);
                return new MarkVehicleExitConflict(winnerName, vehicle.ExitTime ?? exitTime);
            }

            vehicle.MarkExit(actorUserId, exitTime);
            vehicle.Declaration.RecomputeStatus();
            await _db.SaveChangesAsync(cancellationToken);
            await tx.CommitAsync(cancellationToken);
            return new MarkVehicleExitApplied(
                vehicle.Id,
                vehicle.ExitTime!.Value,
                vehicle.Declaration.Id,
                vehicle.Declaration.Status.ToString());
        }
        catch
        {
            await tx.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task<UnExitVehicleResult> UnExitVehicleAsync(Guid vehicleId, CancellationToken cancellationToken)
    {
        var vehicle = await _db.Vehicles
            .Include(v => v.Declaration)
            .ThenInclude(d => d!.Vehicles)
            .FirstOrDefaultAsync(v => v.Id == vehicleId, cancellationToken);

        if (vehicle?.Declaration is null)
        {
            return new UnExitVehicleNotFound();
        }

        if (!vehicle.ExitMarked)
        {
            return new UnExitVehicleInvalidState("Vehicle is not marked as exited.");
        }

        vehicle.UnExit();
        vehicle.Declaration.RecomputeStatus();
        await _db.SaveChangesAsync(cancellationToken);
        return new UnExitVehicleApplied(vehicle.Id, vehicle.Declaration.Id, vehicle.Declaration.Status.ToString());
    }

    private async Task<string> WinnerNameAsync(Guid? exitConfirmedByUserId, CancellationToken cancellationToken)
    {
        if (exitConfirmedByUserId is null)
        {
            return "?";
        }

        var name = await _db.Users.AsNoTracking()
            .Where(u => u.Id == exitConfirmedByUserId)
            .Select(u => u.FullName)
            .FirstOrDefaultAsync(cancellationToken);
        return string.IsNullOrWhiteSpace(name) ? "?" : name;
    }
}
