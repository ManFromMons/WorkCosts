using Microsoft.EntityFrameworkCore;
using WorkCosts.Data;
using WorkCosts.Helpers;
using Xunit;

namespace WorkCosts.Tests;

public class DatabaseInitializationTests
{
    [Fact]
    public async Task InitializeDatabase_FromScratch_AppliesAllMigrationsAndSeedsSuccessfully()
    {
        var tempDbPath = Path.Combine(Path.GetTempPath(), $"workcosts_test_{Guid.NewGuid():N}.db");
        try
        {
            var options = new DbContextOptionsBuilder<WorkCostsDbContext>()
                .UseSqlite($"Data Source={tempDbPath}")
                .Options;

            await using var db = new WorkCostsDbContext(options);
            await db.Database.MigrateAsync();
            await DbInitializer.SeedAsync(db);

            var categories = await db.Categories.ToListAsync();
            Assert.Equal(4, categories.Count);
            Assert.Contains(categories, c => c.Name == "Tools");
            Assert.Contains(categories, c => c.Name == "Garage");
            Assert.Contains(categories, c => c.Name == "Consumables");
            Assert.Contains(categories, c => c.Name == "Parts");

            var jobs = await db.Jobs.ToListAsync();
            Assert.Equal(7, jobs.Count);
            Assert.Contains(jobs, j => j.Name == "Air-Con");
            Assert.Contains(jobs, j => j.Name == "Oil Service");
        }
        finally
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            if (File.Exists(tempDbPath))
            {
                File.Delete(tempDbPath);
            }
        }
    }

    [Fact]
    public void DurationHelper_ParsesAndFormatsDuration()
    {
        Assert.True(DurationHelper.TryParse("1:30", out var minutes));
        Assert.Equal(90, minutes);
        Assert.Equal("1:30", DurationHelper.ToDisplay(90));
    }

    [Fact]
    public void ProductEquivalentHelper_OrdersPairPredictably()
    {
        var guidA = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var guidB = Guid.Parse("22222222-2222-2222-2222-222222222222");

        var (left, right) = ProductEquivalentHelper.OrderPair(guidB, guidA);
        Assert.Equal(guidA, left);
        Assert.Equal(guidB, right);
    }
}
