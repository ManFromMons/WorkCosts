using Microsoft.EntityFrameworkCore;
using WorkCosts.Models;

namespace WorkCosts.Data;

public enum WorkJobDeleteResult
{
    NotFound,
    Deleted,
}

public static class WorkJobCommands
{
    public const int MaxTitleLength = 200;

    public static async Task<bool> TrySetCarIdAsync(
        WorkCostsDbContext db,
        Guid workJobId,
        Guid? carId,
        CancellationToken cancellationToken = default)
    {
        var entity = await db.WorkJobs.FirstOrDefaultAsync(w => w.Id == workJobId, cancellationToken);
        if (entity is null)
        {
            return false;
        }

        if (carId is Guid id && !await db.Cars.AnyAsync(c => c.Id == id, cancellationToken))
        {
            return false;
        }

        entity.CarId = carId;
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public static async Task<WorkJob?> CreateDefinitionAsync(
        WorkCostsDbContext db,
        Guid jobId,
        string title,
        CancellationToken cancellationToken = default)
    {
        var normalized = NormalizeTitle(title);
        if (normalized is null)
        {
            return null;
        }

        if (!await db.Jobs.AnyAsync(j => j.Id == jobId, cancellationToken))
        {
            return null;
        }

        var maxSort = await db.WorkJobs
            .Where(w => w.JobId == jobId && w.IsDefinition)
            .Select(w => (int?)w.SortOrder)
            .MaxAsync(cancellationToken);
        var entity = new WorkJob
        {
            JobId = jobId,
            Title = normalized,
            IsDefinition = true,
            SortOrder = (maxSort ?? -1) + 1,
            CreatedAt = DateTimeOffset.Now,
            CarId = null,
        };
        db.WorkJobs.Add(entity);
        await db.SaveChangesAsync(cancellationToken);
        return entity;
    }

    public static async Task<List<WorkJob>> ListDefinitionsAsync(
        WorkCostsDbContext db,
        Guid jobId,
        CancellationToken cancellationToken = default)
    {
        return await db.WorkJobs
            .AsNoTracking()
            .Include(w => w.Items)
            .Where(w => w.JobId == jobId && w.IsDefinition)
            .OrderBy(w => w.SortOrder)
            .ThenBy(w => w.Id)
            .ToListAsync(cancellationToken);
    }

    public static async Task<List<WorkJob>> ListInstancesAsync(
        WorkCostsDbContext db,
        CancellationToken cancellationToken = default)
    {
        var instances = await db.WorkJobs
            .AsNoTracking()
            .Include(w => w.Items)
            .Where(w => !w.IsDefinition)
            .ToListAsync(cancellationToken);
        return instances
            .OrderByDescending(w => w.CreatedAt.UtcDateTime)
            .ThenBy(w => w.Id)
            .ToList();
    }

    public static async Task<WorkJob?> UpdateDefinitionAsync(
        WorkCostsDbContext db,
        Guid definitionId,
        string? title = null,
        int? sortOrder = null,
        CancellationToken cancellationToken = default)
    {
        var entity = await db.WorkJobs.FirstOrDefaultAsync(
            w => w.Id == definitionId && w.IsDefinition,
            cancellationToken);
        if (entity is null)
        {
            return null;
        }

        if (title is not null)
        {
            var normalized = NormalizeTitle(title);
            if (normalized is null)
            {
                return null;
            }

            entity.Title = normalized;
        }

        if (sortOrder is int order)
        {
            entity.SortOrder = order;
        }

        await db.SaveChangesAsync(cancellationToken);
        return entity;
    }

    public static async Task<WorkJobDeleteResult> TryDeleteDefinitionAsync(
        WorkCostsDbContext db,
        Guid definitionId,
        CancellationToken cancellationToken = default)
    {
        var entity = await db.WorkJobs.FirstOrDefaultAsync(
            w => w.Id == definitionId && w.IsDefinition,
            cancellationToken);
        if (entity is null)
        {
            return WorkJobDeleteResult.NotFound;
        }

        db.WorkJobs.Remove(entity);
        await db.SaveChangesAsync(cancellationToken);
        return WorkJobDeleteResult.Deleted;
    }

    public static async Task<WorkJobItem?> AddDefinitionItemAsync(
        WorkCostsDbContext db,
        Guid definitionId,
        Guid productId,
        short quantity,
        CancellationToken cancellationToken = default)
    {
        if (quantity < 1)
        {
            return null;
        }

        var definition = await db.WorkJobs.FirstOrDefaultAsync(
            w => w.Id == definitionId && w.IsDefinition,
            cancellationToken);
        if (definition is null)
        {
            return null;
        }

        var product = await db.Products.AsNoTracking().FirstOrDefaultAsync(p => p.Id == productId, cancellationToken);
        if (product is null)
        {
            return null;
        }

        var duplicate = await db.WorkJobItems.AnyAsync(
            i => i.WorkJobId == definitionId && i.ProductId == productId,
            cancellationToken);
        if (duplicate)
        {
            return null;
        }

        var item = new WorkJobItem
        {
            WorkJobId = definitionId,
            ProductId = productId,
            Quantity = quantity,
            UnitCostSnapshot = product.UnitCost,
        };
        db.WorkJobItems.Add(item);
        await db.SaveChangesAsync(cancellationToken);
        return item;
    }

    public static async Task<WorkJobItem?> UpdateDefinitionItemQuantityAsync(
        WorkCostsDbContext db,
        Guid itemId,
        short quantity,
        CancellationToken cancellationToken = default)
    {
        if (quantity < 1)
        {
            return null;
        }

        var item = await db.WorkJobItems
            .Include(i => i.WorkJob)
            .FirstOrDefaultAsync(i => i.Id == itemId, cancellationToken);
        if (item?.WorkJob is not { IsDefinition: true })
        {
            return null;
        }

        item.Quantity = quantity;
        await db.SaveChangesAsync(cancellationToken);
        return item;
    }

    public static async Task<WorkJobDeleteResult> RemoveDefinitionItemAsync(
        WorkCostsDbContext db,
        Guid itemId,
        CancellationToken cancellationToken = default)
    {
        var item = await db.WorkJobItems
            .Include(i => i.WorkJob)
            .FirstOrDefaultAsync(i => i.Id == itemId, cancellationToken);
        if (item?.WorkJob is not { IsDefinition: true })
        {
            return WorkJobDeleteResult.NotFound;
        }

        db.WorkJobItems.Remove(item);
        await db.SaveChangesAsync(cancellationToken);
        return WorkJobDeleteResult.Deleted;
    }

    public static async Task<List<WorkJob>?> CopyDefinitionsToInstancesAsync(
        WorkCostsDbContext db,
        Guid jobId,
        IReadOnlyList<Guid>? definitionIds = null,
        CancellationToken cancellationToken = default)
    {
        if (!await db.Jobs.AnyAsync(j => j.Id == jobId, cancellationToken))
        {
            return null;
        }

        List<WorkJob> source;
        if (definitionIds is { Count: > 0 })
        {
            if (definitionIds.Distinct().Count() != definitionIds.Count)
            {
                return null;
            }

            var loaded = await db.WorkJobs
                .Include(w => w.Items)
                .Where(w => definitionIds.Contains(w.Id))
                .ToListAsync(cancellationToken);
            if (loaded.Count != definitionIds.Count
                || loaded.Any(w => !w.IsDefinition || w.JobId != jobId))
            {
                return null;
            }

            var byId = loaded.ToDictionary(w => w.Id);
            source = definitionIds.Select(id => byId[id]).ToList();
        }
        else
        {
            source = await db.WorkJobs
                .Include(w => w.Items)
                .Where(w => w.JobId == jobId && w.IsDefinition)
                .OrderBy(w => w.SortOrder)
                .ThenBy(w => w.Id)
                .ToListAsync(cancellationToken);
        }

        if (source.Count == 0)
        {
            return [];
        }

        var productIds = source.SelectMany(w => w.Items).Select(i => i.ProductId).Distinct().ToList();
        var existingProducts = (await db.Products
                .AsNoTracking()
                .Where(p => productIds.Contains(p.Id))
                .Select(p => p.Id)
                .ToListAsync(cancellationToken))
            .ToHashSet();

        await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);
        var copies = new List<WorkJob>(source.Count);
        foreach (var definition in source)
        {
            var instance = new WorkJob
            {
                JobId = definition.JobId,
                Title = definition.Title,
                IsDefinition = false,
                SortOrder = 0,
                CreatedAt = DateTimeOffset.Now,
                CarId = null,
            };
            foreach (var item in definition.Items)
            {
                if (!existingProducts.Contains(item.ProductId))
                {
                    continue;
                }

                instance.Items.Add(new WorkJobItem
                {
                    ProductId = item.ProductId,
                    Quantity = item.Quantity,
                    UnitCostSnapshot = item.UnitCostSnapshot,
                });
            }

            db.WorkJobs.Add(instance);
            copies.Add(instance);
        }

        await db.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);
        return copies;
    }

    private static string? NormalizeTitle(string? title)
    {
        var trimmed = title?.Trim() ?? string.Empty;
        if (trimmed.Length is 0 or > MaxTitleLength)
        {
            return null;
        }

        return trimmed;
    }
}
