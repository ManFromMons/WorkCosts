using Microsoft.EntityFrameworkCore;

namespace WorkCosts.Data;

public static class ProductCommands
{
    /// <summary>
    /// Deletes a product and every join that references it (job assignments, equivalents, work-job lines).
    /// </summary>
    public static async Task<bool> DeleteAsync(
        WorkCostsDbContext db,
        Guid productId,
        CancellationToken cancellationToken = default)
    {
        var exists = await db.Products
            .AsNoTracking()
            .AnyAsync(p => p.Id == productId, cancellationToken);
        if (!exists)
        {
            return false;
        }

        await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);
        await db.WorkJobItems
            .Where(item => item.ProductId == productId)
            .ExecuteDeleteAsync(cancellationToken);
        await db.ProductJobs
            .Where(link => link.ProductId == productId)
            .ExecuteDeleteAsync(cancellationToken);
        await db.ProductEquivalents
            .Where(link => link.ProductId == productId || link.EquivalentProductId == productId)
            .ExecuteDeleteAsync(cancellationToken);
        await db.Products
            .Where(product => product.Id == productId)
            .ExecuteDeleteAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);
        return true;
    }
}
