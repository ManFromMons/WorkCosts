using Microsoft.EntityFrameworkCore;
using WorkCosts.Models;

namespace WorkCosts.Data;

public enum ItemOfWorkDeleteResult
{
    NotFound,
    Success,
}

public static class ItemOfWorkCommands
{
    public static async Task<ItemOfWork?> CreateAsync(
        WorkCostsDbContext db,
        Guid garageJobId,
        DateTimeOffset occurredAt,
        int? odometerMiles,
        CancellationToken cancellationToken = default)
    {
        if (odometerMiles < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(odometerMiles));
        }

        if (!await db.GarageJobs.AnyAsync(g => g.Id == garageJobId, cancellationToken))
        {
            return null;
        }

        var entity = new ItemOfWork
        {
            GarageJobId = garageJobId,
            OccurredAt = occurredAt,
            OdometerMiles = odometerMiles,
        };
        db.ItemsOfWork.Add(entity);
        await db.SaveChangesAsync(cancellationToken);
        return entity;
    }

    public static async Task<ItemOfWork?> GetLatestAsync(
        WorkCostsDbContext db,
        Guid garageJobId,
        CancellationToken cancellationToken = default)
    {
        var items = await db.ItemsOfWork
            .AsNoTracking()
            .Where(i => i.GarageJobId == garageJobId)
            .ToListAsync(cancellationToken);
        return items
            .OrderByDescending(i => i.OccurredAt.UtcDateTime)
            .ThenByDescending(i => i.Id)
            .FirstOrDefault();
    }

    public static async Task<List<ItemOfWork>> ListAsync(
        WorkCostsDbContext db,
        Guid garageJobId,
        CancellationToken cancellationToken = default)
    {
        var items = await db.ItemsOfWork
            .AsNoTracking()
            .Where(i => i.GarageJobId == garageJobId)
            .ToListAsync(cancellationToken);
        return items
            .OrderByDescending(i => i.OccurredAt.UtcDateTime)
            .ThenByDescending(i => i.Id)
            .ToList();
    }

    public static async Task<ItemOfWorkDeleteResult> TryDeleteAsync(
        WorkCostsDbContext db,
        Guid itemId,
        CancellationToken cancellationToken = default)
    {
        var entity = await db.ItemsOfWork.FirstOrDefaultAsync(i => i.Id == itemId, cancellationToken);
        if (entity is null)
        {
            return ItemOfWorkDeleteResult.NotFound;
        }

        db.ItemsOfWork.Remove(entity);
        await db.SaveChangesAsync(cancellationToken);
        return ItemOfWorkDeleteResult.Success;
    }
}
