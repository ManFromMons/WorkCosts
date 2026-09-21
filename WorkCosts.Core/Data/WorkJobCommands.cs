using Microsoft.EntityFrameworkCore;
using WorkCosts.Models;

namespace WorkCosts.Data;

public static class WorkJobCommands
{
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
}
