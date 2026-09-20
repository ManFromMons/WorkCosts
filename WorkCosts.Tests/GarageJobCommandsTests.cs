using Microsoft.EntityFrameworkCore;
using WorkCosts.Data;
using WorkCosts.Helpers;
using WorkCosts.Models;
using Xunit;

namespace WorkCosts.Tests;

public sealed class GarageJobCommandsTests : IAsyncLifetime
{
    private string _root = null!;
    private WorkCostsDbContext _db = null!;

    public async Task InitializeAsync()
    {
        _root = Path.Combine(Path.GetTempPath(), "workcosts_garagejob_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
        var dbPath = Path.Combine(_root, "workcosts.db");
        var options = new DbContextOptionsBuilder<WorkCostsDbContext>()
            .UseSqlite($"Data Source={dbPath}")
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
    public async Task Migration_AddGarageJobTables_DoesNotAlterJobsTable()
    {
        var columns = await GetJobsColumnNamesAsync();
        Assert.Contains("Name", columns);
        Assert.Contains("GaragePrice", columns);
        Assert.DoesNotContain("TargetKind", columns);
        Assert.Equal(5, columns.Count);
    }

    [Fact]
    public async Task GarageJobCommands_CreateAndUpdate_PersistsScalarsAndRepeatCombine()
    {
        var created = await GarageJobCommands.CreateAsync(_db, "Full service");
        Assert.Equal(GarageJobRepeatCombine.WhicheverFirst, created.RepeatCombine);

        Assert.True(await GarageJobCommands.UpdateAsync(
            _db,
            created.Id,
            "Full service",
            GarageJobTargetKind.Engine,
            "2.0 TDI",
            "Planned scope",
            120,
            GarageJobRepeatCombine.AllMustBeMet,
            intervalAnchorDate: null));

        var loaded = await GarageJobCommands.GetByIdAsync(_db, created.Id);
        Assert.NotNull(loaded);
        Assert.Equal(GarageJobTargetKind.Engine, loaded!.TargetKind);
        Assert.Equal(GarageJobRepeatCombine.AllMustBeMet, loaded.RepeatCombine);
    }

    [Fact]
    public async Task GarageJobCommands_ReplaceRepeatConditions_PersistsMultipleTimeAndDistance()
    {
        var job = await GarageJobCommands.CreateAsync(_db, "Intervals");
        Assert.True(await GarageJobCommands.ReplaceRepeatConditionsAsync(
            _db,
            job.Id,
            [
                new GarageJobRepeatConditionInput(GarageJobRepeatKind.TimePeriod, 12, (int)GarageJobTimeUnit.Months),
                new GarageJobRepeatConditionInput(GarageJobRepeatKind.Distance, 10_000, (int)GarageJobDistanceUnit.Miles),
            ]));

        var loaded = await _db.GarageJobRepeatConditions.Where(c => c.GarageJobId == job.Id).OrderBy(c => c.SortOrder).ToListAsync();
        Assert.Equal(2, loaded.Count);
    }

    [Fact]
    public async Task GarageJobCommands_SetIcon_WritesFileAndUpdatesPath_ClearAndDeleteRemoveFile()
    {
        var job = await GarageJobCommands.CreateAsync(_db, "Icon test");
        var png = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 0 };
        await using (var stream = new MemoryStream(png))
        {
            Assert.True(await GarageJobCommands.SetIconAsync(_db, _root, job.Id, stream, "image/png"));
        }

        var withIcon = await GarageJobCommands.GetByIdAsync(_db, job.Id);
        Assert.False(string.IsNullOrEmpty(withIcon!.IconRelativePath));

        Assert.True(await GarageJobCommands.ClearIconAsync(_db, _root, job.Id));
        await using (var stream = new MemoryStream(png))
        {
            await GarageJobCommands.SetIconAsync(_db, _root, job.Id, stream, "image/png");
        }

        Assert.Equal(GarageJobDeleteResult.Success, await GarageJobCommands.TryDeleteAsync(_db, _root, job.Id));
    }

    [Fact]
    public async Task GarageJobCommands_RequiredProducts_AddRemoveReorder()
    {
        var garageJob = await GarageJobCommands.CreateAsync(_db, "Parts");
        var productA = await AddProductAsync("Filter A");
        var productB = await AddProductAsync("Filter B");
        Assert.True(await GarageJobCommands.AddRequiredProductAsync(_db, garageJob.Id, productA, 2));
        Assert.True(await GarageJobCommands.ReplaceRequiredProductsAsync(
            _db,
            garageJob.Id,
            [new GarageJobRequiredProductInput(productB, 3), new GarageJobRequiredProductInput(productA, 1)]));
        var links = await GarageJobCommands.GetRequiredProductsAsync(_db, garageJob.Id);
        Assert.Equal(productB, links[0].ProductId);
        Assert.True(await GarageJobCommands.RemoveRequiredProductAsync(_db, garageJob.Id, productA));
    }

    [Fact]
    public async Task GarageJobCommands_TryDeleteAsync_CascadesConditionsAndProducts()
    {
        var garageJob = await GarageJobCommands.CreateAsync(_db, "Cascade");
        var product = await AddProductAsync("Oil");
        await GarageJobCommands.ReplaceRepeatConditionsAsync(
            _db,
            garageJob.Id,
            [new GarageJobRepeatConditionInput(GarageJobRepeatKind.TimePeriod, 6, (int)GarageJobTimeUnit.Months)]);
        await GarageJobCommands.AddRequiredProductAsync(_db, garageJob.Id, product);
        Assert.Equal(GarageJobDeleteResult.Success, await GarageJobCommands.TryDeleteAsync(_db, _root, garageJob.Id));
        Assert.False(await _db.GarageJobRequiredProducts.AnyAsync(c => c.GarageJobId == garageJob.Id));
    }

    [Fact]
    public async Task GarageJobCommands_ReplaceReferencedJobs_PersistsOrderAndDedupes()
    {
        var garageJob = await GarageJobCommands.CreateAsync(_db, "Composition");
        var jobs = await _db.Jobs.OrderBy(j => j.Name).Take(3).Select(j => j.Id).ToListAsync();
        Assert.Throws<ArgumentException>(() =>
            GarageJobCommands.ReplaceReferencedJobsAsync(_db, garageJob.Id, [jobs[0], jobs[0]]).GetAwaiter().GetResult());
        Assert.True(await GarageJobCommands.ReplaceReferencedJobsAsync(_db, garageJob.Id, jobs));
        var links = await GarageJobCommands.GetReferencedJobsAsync(_db, garageJob.Id);
        Assert.Equal(jobs, links.Select(l => l.JobId).ToList());
    }

    [Fact]
    public async Task ProductCommands_DeleteAsync_RemovesGarageJobRequiredProductLinks()
    {
        var garageJob = await GarageJobCommands.CreateAsync(_db, "Product delete");
        var product = await AddProductAsync("Gasket");
        await GarageJobCommands.AddRequiredProductAsync(_db, garageJob.Id, product);
        Assert.True(await ProductCommands.DeleteAsync(_db, product));
        Assert.False(await _db.GarageJobRequiredProducts.AnyAsync(l => l.ProductId == product));
    }

    [Fact]
    public async Task Deleting_Job_removes_GarageJobReferencedJobs_without_deleting_GarageJob()
    {
        var garageJob = await GarageJobCommands.CreateAsync(_db, "Parent");
        var template = new Job { Name = "Temp sub-work", GaragePrice = 10m, DurationMinutes = 30 };
        _db.Jobs.Add(template);
        await _db.SaveChangesAsync();
        await GarageJobCommands.ReplaceReferencedJobsAsync(_db, garageJob.Id, [template.Id]);
        _db.Jobs.Remove(template);
        await _db.SaveChangesAsync();
        Assert.True(await _db.GarageJobs.AnyAsync(g => g.Id == garageJob.Id));
        Assert.False(await _db.GarageJobReferencedJobs.AnyAsync(l => l.GarageJobId == garageJob.Id));
    }

    [Fact]
    public void GarageJobRepeatValidation_rejects_zero_and_negative_amounts()
    {
        Assert.False(GarageJobRepeatValidation.IsValidAmount(0));
        Assert.Throws<ArgumentException>(() =>
            GarageJobCommands.ReplaceRepeatConditionsAsync(
                _db,
                Guid.NewGuid(),
                [new GarageJobRepeatConditionInput(GarageJobRepeatKind.TimePeriod, 0, (int)GarageJobTimeUnit.Months)])
                .GetAwaiter().GetResult());
    }

    private async Task<Guid> AddProductAsync(string name)
    {
        var product = new Product { Name = name, CategoryId = DbInitializer.ToolsId, UnitCost = 9.99m };
        _db.Products.Add(product);
        await _db.SaveChangesAsync();
        return product.Id;
    }

    private async Task<List<string>> GetJobsColumnNamesAsync()
    {
        await using var connection = _db.Database.GetDbConnection();
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA table_info(Jobs);";
        await using var reader = await command.ExecuteReaderAsync();
        var names = new List<string>();
        while (await reader.ReadAsync())
        {
            names.Add(reader.GetString(1));
        }

        return names;
    }
}
