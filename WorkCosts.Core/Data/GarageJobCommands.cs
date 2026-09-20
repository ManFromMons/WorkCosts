using Microsoft.EntityFrameworkCore;
using WorkCosts.Helpers;
using WorkCosts.Models;
using WorkCosts.Services;

namespace WorkCosts.Data;

public enum GarageJobDeleteResult
{
    NotFound,
    Success,
}

public sealed record GarageJobRepeatConditionInput(
    GarageJobRepeatKind Kind,
    int Amount,
    int Unit);

public sealed record GarageJobRequiredProductInput(
    Guid ProductId,
    short Quantity);

public static class GarageJobCommands
{
    public static async Task<GarageJob> CreateAsync(
        WorkCostsDbContext db,
        string name,
        CancellationToken cancellationToken = default)
    {
        ValidateName(name);

        var entity = new GarageJob
        {
            Name = name.Trim(),
            RepeatCombine = GarageJobRepeatCombine.WhicheverFirst,
        };
        db.GarageJobs.Add(entity);
        await db.SaveChangesAsync(cancellationToken);
        return entity;
    }

    public static Task<GarageJob?> GetByIdAsync(
        WorkCostsDbContext db,
        Guid garageJobId,
        CancellationToken cancellationToken = default) =>
        db.GarageJobs
            .AsNoTracking()
            .Include(g => g.RepeatConditions)
            .FirstOrDefaultAsync(g => g.Id == garageJobId, cancellationToken);

    public static Task<List<GarageJob>> ListAsync(
        WorkCostsDbContext db,
        CancellationToken cancellationToken = default) =>
        db.GarageJobs
            .AsNoTracking()
            .OrderBy(g => g.Name)
            .ToListAsync(cancellationToken);

    public static async Task<bool> UpdateAsync(
        WorkCostsDbContext db,
        Guid garageJobId,
        string name,
        GarageJobTargetKind targetKind,
        string targetLabel,
        string description,
        int durationMinutes,
        GarageJobRepeatCombine repeatCombine,
        CancellationToken cancellationToken = default)
    {
        ValidateName(name);
        if (durationMinutes < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(durationMinutes));
        }

        var entity = await db.GarageJobs.FirstOrDefaultAsync(g => g.Id == garageJobId, cancellationToken);
        if (entity is null)
        {
            return false;
        }

        entity.Name = name.Trim();
        entity.TargetKind = targetKind;
        entity.TargetLabel = targetLabel?.Trim() ?? string.Empty;
        entity.Description = description?.Trim() ?? string.Empty;
        entity.DurationMinutes = durationMinutes;
        entity.RepeatCombine = repeatCombine;
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public static async Task<bool> ReplaceRepeatConditionsAsync(
        WorkCostsDbContext db,
        Guid garageJobId,
        IReadOnlyList<GarageJobRepeatConditionInput> conditions,
        CancellationToken cancellationToken = default)
    {
        foreach (var item in conditions)
        {
            if (!GarageJobRepeatValidation.IsValidAmount(item.Amount)
                || !GarageJobRepeatValidation.IsValidUnitForKind(item.Kind, item.Unit))
            {
                throw new ArgumentException("Invalid repeat condition.", nameof(conditions));
            }
        }

        var exists = await db.GarageJobs.AnyAsync(g => g.Id == garageJobId, cancellationToken);
        if (!exists)
        {
            return false;
        }

        await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);
        await db.GarageJobRepeatConditions
            .Where(c => c.GarageJobId == garageJobId)
            .ExecuteDeleteAsync(cancellationToken);
        db.ChangeTracker.Clear();

        var sort = 0;
        foreach (var item in conditions)
        {
            db.GarageJobRepeatConditions.Add(new GarageJobRepeatCondition
            {
                GarageJobId = garageJobId,
                Kind = item.Kind,
                Amount = item.Amount,
                Unit = item.Unit,
                SortOrder = sort++,
            });
        }

        await db.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);
        return true;
    }

    public static Task<List<GarageJobRequiredProduct>> GetRequiredProductsAsync(
        WorkCostsDbContext db,
        Guid garageJobId,
        CancellationToken cancellationToken = default) =>
        db.GarageJobRequiredProducts
            .AsNoTracking()
            .Where(link => link.GarageJobId == garageJobId)
            .OrderBy(link => link.SortOrder)
            .ToListAsync(cancellationToken);

    public static async Task<bool> AddRequiredProductAsync(
        WorkCostsDbContext db,
        Guid garageJobId,
        Guid productId,
        short quantity = 1,
        CancellationToken cancellationToken = default)
    {
        if (quantity < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(quantity));
        }

        if (!await db.GarageJobs.AnyAsync(g => g.Id == garageJobId, cancellationToken))
        {
            return false;
        }

        if (!await db.Products.AnyAsync(p => p.Id == productId, cancellationToken))
        {
            return false;
        }

        if (await db.GarageJobRequiredProducts.AnyAsync(
                link => link.GarageJobId == garageJobId && link.ProductId == productId,
                cancellationToken))
        {
            return false;
        }

        var maxOrder = await db.GarageJobRequiredProducts
            .Where(link => link.GarageJobId == garageJobId)
            .Select(link => (int?)link.SortOrder)
            .MaxAsync(cancellationToken) ?? -1;

        db.GarageJobRequiredProducts.Add(new GarageJobRequiredProduct
        {
            GarageJobId = garageJobId,
            ProductId = productId,
            Quantity = quantity,
            SortOrder = maxOrder + 1,
        });
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public static async Task<bool> RemoveRequiredProductAsync(
        WorkCostsDbContext db,
        Guid garageJobId,
        Guid productId,
        CancellationToken cancellationToken = default)
    {
        var deleted = await db.GarageJobRequiredProducts
            .Where(link => link.GarageJobId == garageJobId && link.ProductId == productId)
            .ExecuteDeleteAsync(cancellationToken);
        return deleted > 0;
    }

    public static async Task<bool> ReplaceRequiredProductsAsync(
        WorkCostsDbContext db,
        Guid garageJobId,
        IReadOnlyList<GarageJobRequiredProductInput> products,
        CancellationToken cancellationToken = default)
    {
        if (products.Select(p => p.ProductId).Distinct().Count() != products.Count)
        {
            throw new ArgumentException("Duplicate product ids.", nameof(products));
        }

        foreach (var item in products)
        {
            if (item.Quantity < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(products));
            }
        }

        if (!await db.GarageJobs.AnyAsync(g => g.Id == garageJobId, cancellationToken))
        {
            return false;
        }

        if (products.Count > 0)
        {
            var ids = products.Select(p => p.ProductId).ToList();
            var found = await db.Products.CountAsync(p => ids.Contains(p.Id), cancellationToken);
            if (found != ids.Count)
            {
                return false;
            }
        }

        await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);
        await db.GarageJobRequiredProducts
            .Where(link => link.GarageJobId == garageJobId)
            .ExecuteDeleteAsync(cancellationToken);
        db.ChangeTracker.Clear();

        var sort = 0;
        foreach (var item in products)
        {
            db.GarageJobRequiredProducts.Add(new GarageJobRequiredProduct
            {
                GarageJobId = garageJobId,
                ProductId = item.ProductId,
                Quantity = item.Quantity,
                SortOrder = sort++,
            });
        }

        await db.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);
        return true;
    }

    public static Task<List<GarageJobReferencedJob>> GetReferencedJobsAsync(
        WorkCostsDbContext db,
        Guid garageJobId,
        CancellationToken cancellationToken = default) =>
        db.GarageJobReferencedJobs
            .AsNoTracking()
            .Where(link => link.GarageJobId == garageJobId)
            .OrderBy(link => link.SortOrder)
            .ToListAsync(cancellationToken);

    public static async Task<bool> ReplaceReferencedJobsAsync(
        WorkCostsDbContext db,
        Guid garageJobId,
        IReadOnlyList<Guid> jobIdsInOrder,
        CancellationToken cancellationToken = default)
    {
        if (jobIdsInOrder.Distinct().Count() != jobIdsInOrder.Count)
        {
            throw new ArgumentException("Duplicate job ids.", nameof(jobIdsInOrder));
        }

        if (!await db.GarageJobs.AnyAsync(g => g.Id == garageJobId, cancellationToken))
        {
            return false;
        }

        if (jobIdsInOrder.Count > 0)
        {
            var found = await db.Jobs.CountAsync(j => jobIdsInOrder.Contains(j.Id), cancellationToken);
            if (found != jobIdsInOrder.Count)
            {
                return false;
            }
        }

        await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);
        await db.GarageJobReferencedJobs
            .Where(link => link.GarageJobId == garageJobId)
            .ExecuteDeleteAsync(cancellationToken);
        db.ChangeTracker.Clear();

        var sort = 0;
        foreach (var jobId in jobIdsInOrder)
        {
            db.GarageJobReferencedJobs.Add(new GarageJobReferencedJob
            {
                GarageJobId = garageJobId,
                JobId = jobId,
                SortOrder = sort++,
            });
        }

        await db.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);
        return true;
    }

    public static async Task<bool> SetIconAsync(
        WorkCostsDbContext db,
        string dataRoot,
        Guid garageJobId,
        Stream content,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        var entity = await db.GarageJobs.FirstOrDefaultAsync(g => g.Id == garageJobId, cancellationToken);
        if (entity is null)
        {
            return false;
        }

        GarageJobIconStore.DeleteIfExists(dataRoot, entity.IconRelativePath);

        await GarageJobIconStore.WriteAsync(dataRoot, garageJobId, content, contentType, cancellationToken);
        var ext = contentType.Trim().ToLowerInvariant() switch
        {
            "image/jpeg" => "jpg",
            "image/webp" => "webp",
            _ => "png",
        };
        entity.IconRelativePath = GarageJobIconStore.RelativePathFor(garageJobId, ext);
        entity.IconContentType = contentType.Trim();
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public static async Task<bool> ClearIconAsync(
        WorkCostsDbContext db,
        string dataRoot,
        Guid garageJobId,
        CancellationToken cancellationToken = default)
    {
        var entity = await db.GarageJobs.FirstOrDefaultAsync(g => g.Id == garageJobId, cancellationToken);
        if (entity is null)
        {
            return false;
        }

        GarageJobIconStore.DeleteIfExists(dataRoot, entity.IconRelativePath);
        entity.IconRelativePath = string.Empty;
        entity.IconContentType = string.Empty;
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public static async Task<GarageJobDeleteResult> TryDeleteAsync(
        WorkCostsDbContext db,
        string dataRoot,
        Guid garageJobId,
        CancellationToken cancellationToken = default)
    {
        var entity = await db.GarageJobs.FirstOrDefaultAsync(g => g.Id == garageJobId, cancellationToken);
        if (entity is null)
        {
            return GarageJobDeleteResult.NotFound;
        }

        var iconPath = entity.IconRelativePath;
        await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);
        db.GarageJobs.Remove(entity);
        await db.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);

        GarageJobIconStore.DeleteIfExists(dataRoot, iconPath);
        return GarageJobDeleteResult.Success;
    }

    private static void ValidateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Name is required.", nameof(name));
        }

        if (name.Trim().Length > 200)
        {
            throw new ArgumentException("Name is too long.", nameof(name));
        }
    }
}
