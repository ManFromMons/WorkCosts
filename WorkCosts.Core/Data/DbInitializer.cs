using Microsoft.EntityFrameworkCore;
using WorkCosts.Models;

namespace WorkCosts.Data;

public static class DbInitializer
{
    public static readonly Guid ToolsId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    public static readonly Guid GarageId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    public static readonly Guid ConsumablesId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    public static readonly Guid PartsId = Guid.Parse("44444444-4444-4444-4444-444444444444");

    public static readonly Guid AirConJobId = Guid.Parse("a1111111-1111-4111-8111-111111111101");
    public static readonly Guid AllJobId = Guid.Parse("a1111111-1111-4111-8111-111111111102");
    public static readonly Guid BrakePadsJobId = Guid.Parse("a1111111-1111-4111-8111-111111111103");
    public static readonly Guid BrakeRotorsJobId = Guid.Parse("a1111111-1111-4111-8111-111111111104");
    public static readonly Guid OilServiceJobId = Guid.Parse("a1111111-1111-4111-8111-111111111105");
    public static readonly Guid CoolantServiceJobId = Guid.Parse("a1111111-1111-4111-8111-111111111106");
    public static readonly Guid SuspensionJobId = Guid.Parse("a1111111-1111-4111-8111-111111111107");

    public const int Hour = 60;

    public static async Task InitializeAsync(WorkCostsDbContext db)
    {
        await db.Database.MigrateAsync();
        await SeedAsync(db);
    }

    public static async Task SeedAsync(WorkCostsDbContext db)
    {
        await SeedCategoriesAsync(db);
        await SeedJobsAsync(db);
    }

    private static async Task SeedCategoriesAsync(WorkCostsDbContext db)
    {
        var seeds = new[]
        {
            new Category { Id = ToolsId, Name = "Tools" },
            new Category { Id = GarageId, Name = "Garage" },
            new Category { Id = ConsumablesId, Name = "Consumables" },
            new Category { Id = PartsId, Name = "Parts" }
        };

        foreach (var seed in seeds)
        {
            if (!await db.Categories.AnyAsync(c => c.Id == seed.Id || c.Name == seed.Name))
            {
                db.Categories.Add(seed);
            }
        }

        await db.SaveChangesAsync();
    }

    private static async Task SeedJobsAsync(WorkCostsDbContext db)
    {
        var seeds = new[]
        {
            new Job { Id = AirConJobId, Name = "Air-Con", GaragePrice = 1200m, DurationMinutes = 5 * Hour },
            new Job { Id = AllJobId, Name = "All", DurationMinutes = Hour },
            new Job { Id = BrakePadsJobId, Name = "Brake Pads", GaragePrice = 250m, DurationMinutes = 2 * Hour },
            new Job { Id = BrakeRotorsJobId, Name = "Brake Rotors", GaragePrice = 800m, DurationMinutes = 8 * Hour },
            new Job { Id = OilServiceJobId, Name = "Oil Service", GaragePrice = 150m, DurationMinutes = Hour },
            new Job { Id = CoolantServiceJobId, Name = "Coolant Service", GaragePrice = 100m, DurationMinutes = 2 * Hour },
            new Job { Id = SuspensionJobId, Name = "Suspension", GaragePrice = 2000m, DurationMinutes = 16 * Hour }
        };

        foreach (var seed in seeds)
        {
            if (!await db.Jobs.AnyAsync(j => j.Id == seed.Id || j.Name == seed.Name))
            {
                db.Jobs.Add(seed);
            }
        }

        await db.SaveChangesAsync();
    }
}
