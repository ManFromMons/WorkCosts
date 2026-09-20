using Microsoft.EntityFrameworkCore;
using WorkCosts.Data;
using WorkCosts.Models;

namespace WorkCosts.Helpers;

public sealed record GarageJobRollupLine(
    Guid ProductId,
    string Name,
    short Quantity,
    decimal UnitCost,
    decimal LineTotal);

public sealed record GarageJobRollup(
    IReadOnlyList<GarageJobRollupLine> Parts,
    decimal GarageCostGbp,
    decimal DiyPartsCostGbp,
    int ReferencedDurationMinutes,
    int ParentDurationMinutes);

public static class GarageJobRollupCalculator
{
    public static async Task<GarageJobRollup?> ComputeAsync(
        WorkCostsDbContext db,
        Guid garageJobId,
        CancellationToken cancellationToken = default)
    {
        var job = await db.GarageJobs
            .AsNoTracking()
            .Include(g => g.ReferencedJobs)
                .ThenInclude(link => link.Job!)
                    .ThenInclude(j => j.ProductJobs)
                        .ThenInclude(pj => pj.Product)
            .Include(g => g.RequiredProducts)
                .ThenInclude(link => link.Product)
            .FirstOrDefaultAsync(g => g.Id == garageJobId, cancellationToken);

        if (job is null)
        {
            return null;
        }

        var quantities = new Dictionary<Guid, short>();
        var names = new Dictionary<Guid, string>();
        var costs = new Dictionary<Guid, decimal>();
        var order = new List<Guid>();

        decimal garageCost = 0m;
        var referencedMinutes = 0;

        foreach (var link in job.ReferencedJobs.OrderBy(l => l.SortOrder))
        {
            var referenced = link.Job;
            if (referenced is null)
            {
                continue;
            }

            garageCost += referenced.GaragePrice;
            referencedMinutes += referenced.DurationMinutes;

            foreach (var productJob in referenced.ProductJobs)
            {
                var product = productJob.Product;
                if (product is null)
                {
                    continue;
                }

                Accumulate(product, 1, quantities, names, costs, order);
            }
        }

        foreach (var required in job.RequiredProducts.OrderBy(l => l.SortOrder))
        {
            var product = required.Product;
            if (product is null)
            {
                continue;
            }

            Accumulate(product, required.Quantity, quantities, names, costs, order);
        }

        var parts = new List<GarageJobRollupLine>(order.Count);
        decimal diy = 0m;
        foreach (var id in order)
        {
            var qty = quantities[id];
            var unit = costs[id];
            var line = RoundMoney(unit * qty);
            diy += line;
            parts.Add(new GarageJobRollupLine(id, names[id], qty, unit, line));
        }

        return new GarageJobRollup(
            parts,
            RoundMoney(garageCost),
            RoundMoney(diy),
            referencedMinutes,
            job.DurationMinutes);
    }

    private static void Accumulate(
        Product product,
        short quantity,
        Dictionary<Guid, short> quantities,
        Dictionary<Guid, string> names,
        Dictionary<Guid, decimal> costs,
        List<Guid> order)
    {
        if (quantities.TryGetValue(product.Id, out var existing))
        {
            quantities[product.Id] = (short)(existing + quantity);
            return;
        }

        quantities[product.Id] = quantity;
        names[product.Id] = product.Name;
        costs[product.Id] = product.UnitCost;
        order.Add(product.Id);
    }

    private static decimal RoundMoney(decimal value) =>
        Math.Round(value, 2, MidpointRounding.AwayFromZero);
}
