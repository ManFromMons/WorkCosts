using System.Text;
using Microsoft.EntityFrameworkCore;
using WorkCosts.Data;
using Xunit;

namespace WorkCosts.Tests;

public sealed class CarDetailsJsonSeedTests : IAsyncLifetime
{
    private string _root = null!;
    private WorkCostsDbContext _db = null!;

    public async Task InitializeAsync()
    {
        _root = Path.Combine(Path.GetTempPath(), "workcosts_cardetails_seed_" + Guid.NewGuid().ToString("N"));
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
    public void CarDetailsJsonSeed_EmptyArray_ReturnsEmpty()
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("[]"));
        var rows = CarDetailsJsonSeed.Read(stream);
        Assert.Empty(rows);
    }

    [Fact]
    public async Task DbInitializer_EmptyCarDetailsJson_InsertsNothing()
    {
        Assert.Equal(0, await _db.CarDetails.CountAsync());
        Assert.NotNull(CarDetailsJsonSeed.OpenEmbedded());
        await DbInitializer.SeedCarDetailsAsync(_db);
        Assert.Equal(0, await _db.CarDetails.CountAsync());
    }

    [Fact]
    public async Task DbInitializer_KeepsUserAddedTypes_WhenJsonEmpty()
    {
        var created = await CarDetailsCommands.CreateAsync(
            _db,
            new CarDetailsInput("BMW", "545", "E60", 2004, "4.4 V8"));
        Assert.True(created.Saved);

        await DbInitializer.SeedCarDetailsAsync(_db);
        var listed = await CarDetailsCommands.ListAsync(_db);
        Assert.Single(listed);
        Assert.Equal(created.Type!.Id, listed[0].Id);
        Assert.Equal("E60", listed[0].ModelNumber);
    }

    [Fact]
    public void CarDetailsJsonSeed_Malformed_Throws()
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("{"));
        var ex = Assert.Throws<InvalidOperationException>(() => CarDetailsJsonSeed.Read(stream));
        Assert.Contains("car-details.json", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DbInitializer_MalformedCarDetailsJson_SkipsWithoutWiping()
    {
        var created = await CarDetailsCommands.CreateAsync(
            _db,
            new CarDetailsInput("BMW", "545", "E60", 2004, "4.4 V8"));
        Assert.True(created.Saved);

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("not-json"));
        await DbInitializer.SeedCarDetailsAsync(_db, stream);
        Assert.Single(await CarDetailsCommands.ListAsync(_db));
    }
}
