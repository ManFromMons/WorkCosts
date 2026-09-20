using Microsoft.EntityFrameworkCore;
using WorkCosts.Data;
using WorkCosts.Helpers;
using WorkCosts.Models;
using Xunit;

namespace WorkCosts.Tests;

public sealed class GarageJobIntervalPersistenceTests : IAsyncLifetime
{
    private string _root = null!;
    private WorkCostsDbContext _db = null!;

    public async Task InitializeAsync()
    {
        _root = Path.Combine(Path.GetTempPath(), "workcosts_gj_interval_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
        var options = new DbContextOptionsBuilder<WorkCostsDbContext>()
            .UseSqlite($"Data Source={Path.Combine(_root, "workcosts.db")}")
            .Options;
        _db = new WorkCostsDbContext(options);
        await _db.Database.MigrateAsync();
        await DbInitializer.SeedAsync(_db);
    }

    public Task DisposeAsync()
    {
        _db.Dispose();
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        try
        {
            if (Directory.Exists(_root))
            {
                Directory.Delete(_root, recursive: true);
            }
        }
        catch
        {
        }

        return Task.CompletedTask;
    }

    [Fact]
    public async Task ItemOfWorkCommands_CreateListLatestDelete()
    {
        var job = await GarageJobCommands.CreateAsync(_db, "Log");
        var older = await ItemOfWorkCommands.CreateAsync(_db, job.Id, London(2025, 1, 1), 10_000);
        var newer = await ItemOfWorkCommands.CreateAsync(_db, job.Id, London(2026, 1, 1), 20_000);
        Assert.NotNull(older);
        Assert.NotNull(newer);
        var latest = await ItemOfWorkCommands.GetLatestAsync(_db, job.Id);
        Assert.Equal(newer!.Id, latest!.Id);
        var listed = await ItemOfWorkCommands.ListAsync(_db, job.Id);
        Assert.Equal([newer.Id, older!.Id], listed.Select(i => i.Id).ToList());
        Assert.Equal(ItemOfWorkDeleteResult.Success, await ItemOfWorkCommands.TryDeleteAsync(_db, older.Id));
        Assert.Equal(ItemOfWorkDeleteResult.NotFound, await ItemOfWorkCommands.TryDeleteAsync(_db, older.Id));
    }

    [Fact]
    public async Task ItemOfWorkCommands_RejectsNegativeMiles()
    {
        var job = await GarageJobCommands.CreateAsync(_db, "Neg");
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            ItemOfWorkCommands.CreateAsync(_db, job.Id, London(2026, 1, 1), -1));
    }

    [Fact]
    public async Task ItemOfWorkCommands_Create_UnknownGarageJob_ReturnsNull()
    {
        Assert.Null(await ItemOfWorkCommands.CreateAsync(_db, Guid.NewGuid(), London(2026, 1, 1), 1));
    }

    [Fact]
    public async Task GarageJobCommands_Update_PersistsIntervalAnchorDate()
    {
        var job = await GarageJobCommands.CreateAsync(_db, "Anchor");
        var date = new DateOnly(2024, 6, 15);
        Assert.True(await GarageJobCommands.UpdateAsync(
            _db,
            job.Id,
            "Anchor",
            GarageJobTargetKind.Car,
            string.Empty,
            string.Empty,
            0,
            GarageJobRepeatCombine.WhicheverFirst,
            date));
        var loaded = await GarageJobCommands.GetByIdAsync(_db, job.Id);
        Assert.Equal(date, loaded!.IntervalAnchorDate);
    }

    [Fact]
    public async Task GarageJobCommands_TryDeleteAsync_HasCompletions_DoesNotDelete()
    {
        var job = await GarageJobCommands.CreateAsync(_db, "Keep");
        await ItemOfWorkCommands.CreateAsync(_db, job.Id, London(2026, 1, 1), 100);
        Assert.Equal(GarageJobDeleteResult.HasCompletions, await GarageJobCommands.TryDeleteAsync(_db, _root, job.Id));
        Assert.True(await _db.GarageJobs.AnyAsync(g => g.Id == job.Id));
        Assert.True(await _db.ItemsOfWork.AnyAsync(i => i.GarageJobId == job.Id));
    }

    [Fact]
    public async Task EvaluateAsync_UsesLatestItemAndAnchor()
    {
        var job = await GarageJobCommands.CreateAsync(_db, "Eval");
        await GarageJobCommands.UpdateAsync(
            _db,
            job.Id,
            "Eval",
            GarageJobTargetKind.Car,
            string.Empty,
            string.Empty,
            180,
            GarageJobRepeatCombine.WhicheverFirst,
            new DateOnly(2020, 1, 1));
        await GarageJobCommands.ReplaceRepeatConditionsAsync(
            _db,
            job.Id,
            [new GarageJobRepeatConditionInput(GarageJobRepeatKind.TimePeriod, 12, (int)GarageJobTimeUnit.Months)]);
        await ItemOfWorkCommands.CreateAsync(_db, job.Id, London(2025, 1, 1), 1_000);
        await ItemOfWorkCommands.CreateAsync(_db, job.Id, London(2026, 1, 1), 2_000);
        var asOf = London(2026, 6, 1);
        var result = await GarageJobDueEvaluator.EvaluateAsync(_db, job.Id, asOf, currentOdometerMiles: 2_500);
        Assert.NotNull(result);
        Assert.Equal(GarageJobDueStatus.NotDue, result!.Status);
        Assert.True(result.HasCompletion);
        Assert.Equal(GarageJobLondonTime.Add(London(2026, 1, 1), 12, GarageJobTimeUnit.Months), result.NextDueAt);
        Assert.Null(await GarageJobDueEvaluator.EvaluateAsync(_db, Guid.NewGuid(), asOf, 0));
    }

    [Fact]
    public async Task Rollup_EmptyComposition_ZeroTotals_EchoesParentDuration()
    {
        var job = await GarageJobCommands.CreateAsync(_db, "Empty");
        await GarageJobCommands.UpdateAsync(
            _db,
            job.Id,
            "Empty",
            GarageJobTargetKind.Car,
            string.Empty,
            string.Empty,
            90,
            GarageJobRepeatCombine.WhicheverFirst,
            null);
        var rollup = await GarageJobRollupCalculator.ComputeAsync(_db, job.Id);
        Assert.NotNull(rollup);
        Assert.Empty(rollup!.Parts);
        Assert.Equal(0m, rollup.GarageCostGbp);
        Assert.Equal(0m, rollup.DiyPartsCostGbp);
        Assert.Equal(0, rollup.ReferencedDurationMinutes);
        Assert.Equal(90, rollup.ParentDurationMinutes);
    }

    [Fact]
    public async Task Rollup_SumsReferencedGaragePriceAndDuration()
    {
        var garageJob = await GarageJobCommands.CreateAsync(_db, "Sum");
        var a = new Job { Name = "Pads", GaragePrice = 80.10m, DurationMinutes = 40 };
        var b = new Job { Name = "Rotors", GaragePrice = 120.20m, DurationMinutes = 50 };
        _db.Jobs.AddRange(a, b);
        await _db.SaveChangesAsync();
        await GarageJobCommands.ReplaceReferencedJobsAsync(_db, garageJob.Id, [a.Id, b.Id]);
        var rollup = await GarageJobRollupCalculator.ComputeAsync(_db, garageJob.Id);
        Assert.Equal(200.30m, rollup!.GarageCostGbp);
        Assert.Equal(90, rollup.ReferencedDurationMinutes);
    }

    [Fact]
    public async Task Rollup_MergesProductJobsQuantityOnePerLink()
    {
        var garageJob = await GarageJobCommands.CreateAsync(_db, "Merge");
        var product = await AddProductAsync("Pad set", 12.50m);
        var a = new Job { Name = "Left", GaragePrice = 10m, DurationMinutes = 10 };
        var b = new Job { Name = "Right", GaragePrice = 10m, DurationMinutes = 10 };
        _db.Jobs.AddRange(a, b);
        await _db.SaveChangesAsync();
        _db.ProductJobs.AddRange(
            new ProductJob { JobId = a.Id, ProductId = product },
            new ProductJob { JobId = b.Id, ProductId = product });
        await _db.SaveChangesAsync();
        await GarageJobCommands.ReplaceReferencedJobsAsync(_db, garageJob.Id, [a.Id, b.Id]);
        var rollup = await GarageJobRollupCalculator.ComputeAsync(_db, garageJob.Id);
        var line = Assert.Single(rollup!.Parts);
        Assert.Equal(2, line.Quantity);
        Assert.Equal(25.00m, line.LineTotal);
        Assert.Equal(25.00m, rollup.DiyPartsCostGbp);
    }

    [Fact]
    public async Task Rollup_AddsRequiredProductQuantity_SameProductSums()
    {
        var garageJob = await GarageJobCommands.CreateAsync(_db, "Req");
        var product = await AddProductAsync("Oil", 8.00m);
        var job = new Job { Name = "Oil change", GaragePrice = 50m, DurationMinutes = 30 };
        _db.Jobs.Add(job);
        await _db.SaveChangesAsync();
        _db.ProductJobs.Add(new ProductJob { JobId = job.Id, ProductId = product });
        await _db.SaveChangesAsync();
        await GarageJobCommands.ReplaceReferencedJobsAsync(_db, garageJob.Id, [job.Id]);
        await GarageJobCommands.AddRequiredProductAsync(_db, garageJob.Id, product, 3);
        var rollup = await GarageJobRollupCalculator.ComputeAsync(_db, garageJob.Id);
        var line = Assert.Single(rollup!.Parts);
        Assert.Equal(4, line.Quantity);
        Assert.Equal(32.00m, rollup.DiyPartsCostGbp);
    }

    [Fact]
    public async Task Rollup_DoesNotIncludeIsAllJobsUnlessLinked()
    {
        var garageJob = await GarageJobCommands.CreateAsync(_db, "AllJobs");
        var extra = new Product
        {
            Name = "Ratchet",
            CategoryId = DbInitializer.ToolsId,
            UnitCost = 5m,
            IsAllJobs = true,
        };
        _db.Products.Add(extra);
        await _db.SaveChangesAsync();
        var rollup = await GarageJobRollupCalculator.ComputeAsync(_db, garageJob.Id);
        Assert.Empty(rollup!.Parts);
    }

    [Fact]
    public async Task Rollup_UnknownGarageJob_ReturnsNull()
    {
        Assert.Null(await GarageJobRollupCalculator.ComputeAsync(_db, Guid.NewGuid()));
    }

    private async Task<Guid> AddProductAsync(string name, decimal unitCost)
    {
        var product = new Product { Name = name, CategoryId = DbInitializer.ToolsId, UnitCost = unitCost };
        _db.Products.Add(product);
        await _db.SaveChangesAsync();
        return product.Id;
    }

    private static DateTimeOffset London(int year, int month, int day) =>
        GarageJobLondonTime.Convert(
            new DateTimeOffset(
                TimeZoneInfo.ConvertTimeToUtc(
                    new DateTime(year, month, day, 12, 0, 0, DateTimeKind.Unspecified),
                    GarageJobLondonTime.TimeZone),
                TimeSpan.Zero));
}
