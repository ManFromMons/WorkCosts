using System.Net;
using System.Text;
using Microsoft.EntityFrameworkCore;
using WorkCosts.Data;
using Xunit;

namespace WorkCosts.Tests;

public sealed class CarDetailsCatalogueMapperTests : IAsyncLifetime
{
    private string _root = null!;
    private WorkCostsDbContext _db = null!;

    public async Task InitializeAsync()
    {
        _root = Path.Combine(Path.Combine(Path.GetTempPath(), "workcosts_catalogue_" + Guid.NewGuid().ToString("N")));
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
    public void CatalogueMapper_BmwE60_MapsModAndChassisAndYears()
    {
        var rows = CarDetailsCatalogueMapper.Map([Row("BMW", "5 series", "5 series (E60)", "2004", "2010")]);
        var row = Assert.Single(rows);
        Assert.Equal("BMW", row.Make);
        Assert.Equal("5 series (E60)", row.Model);
        Assert.Equal("E60", row.ModelNumber);
        Assert.Equal(2004, row.Year);
        Assert.Equal(2010, row.EndYear);
    }

    [Fact]
    public void CatalogueMapper_JaguarX350_KeepsSlashChassis()
    {
        var rows = CarDetailsCatalogueMapper.Map([Row("Jaguar", "XJ", "XJ III (X350/X358)", "2003", "2008")]);
        Assert.Equal("X350/X358", Assert.Single(rows).ModelNumber);
    }

    [Fact]
    public void CatalogueMapper_MercedesW204_MapsChassis()
    {
        var rows = CarDetailsCatalogueMapper.Map([Row("Mercedes", "C-class", "C-class (W204)", "2007", "2014")]);
        Assert.Equal("W204", Assert.Single(rows).ModelNumber);
    }

    [Fact]
    public void CatalogueMapper_NoParens_UsesFallbackModel()
    {
        var rows = CarDetailsCatalogueMapper.Map(
            [Row("Jaguar", "E-Pace", "E-Pace", "2017", "")],
            chassisLookup: (_, _) => null);
        Assert.Equal("E-Pace", Assert.Single(rows).ModelNumber);
    }

    [Fact]
    public void CatalogueMapper_NoParens_UsesWikidataWhenProvided()
    {
        var rows = CarDetailsCatalogueMapper.Map(
            [Row("Jaguar", "E-Pace", "E-Pace", "2017", "")],
            chassisLookup: (_, _) => "X540");
        Assert.Equal("X540", Assert.Single(rows).ModelNumber);
    }

    [Fact]
    public void CatalogueMapper_LongChassisList_TakesFirstCodeWhenOver32()
    {
        var mod = "1 series (" + new string('A', 40) + ")";
        var rows = CarDetailsCatalogueMapper.Map([Row("BMW", "1 series", mod, "2004", "2013")]);
        Assert.Equal(32, Assert.Single(rows).ModelNumber.Length);
    }

    [Fact]
    public void CatalogueMapper_SkipsBadStartYear()
    {
        var rows = CarDetailsCatalogueMapper.Map([Row("BMW", "5 series", "5 series (E60)", "not-a-year", "2010")]);
        Assert.Empty(rows);
    }

    [Fact]
    public void CatalogueMapper_AmgAndStandard_AreTwoTypes()
    {
        var rows = CarDetailsCatalogueMapper.Map(
        [
            Row("Mercedes", "A-class", "A-class (W177)", "2018", ""),
            Row("Mercedes", "A-class", "A-class AMG (W177)", "2018", ""),
        ]);
        Assert.Equal(2, rows.Count);
    }

    [Fact]
    public void GuidFromTypeKey_IsStable()
    {
        var key = CarDetailsCommands.NormalizeTypeKey("BMW", "5 series (E60)", "E60", 2004);
        Assert.Equal(CarDetailsCatalogueMapper.GuidFromTypeKey(key), CarDetailsCatalogueMapper.GuidFromTypeKey(key));
    }

    [Fact]
    public async Task CarDetailsJsonSeed_Upsert_OverwritesSameId()
    {
        var id = Guid.NewGuid();
        await CarDetailsJsonSeed.UpsertAsync(_db, [new CarDetailsJsonRow(id, "Homebrew", "Overwrite", "ABC", 1999, 2010)]);
        await CarDetailsJsonSeed.UpsertAsync(_db, [new CarDetailsJsonRow(id, "Homebrew", "Overwrite", "ABC", 1999, 2009)]);
        var loaded = await CarDetailsCommands.GetAsync(_db, id);
        Assert.Equal(2009, loaded!.EndYear);
    }

    [Fact]
    public async Task CarDetailsJsonSeed_KeepsUserType_WhenTypeKeyDiffers()
    {
        var created = await CarDetailsCommands.CreateAsync(_db, new CarDetailsInput("Homebrew", "Special", "XYZ", 2004));
        Assert.True(created.Saved);
        var seedId = Guid.NewGuid();
        await CarDetailsJsonSeed.UpsertAsync(_db, [new CarDetailsJsonRow(seedId, "Seedmake", "Seedmod", "ZZZ", 1998, 2010)]);
        Assert.NotNull(await CarDetailsCommands.GetAsync(_db, created.Type!.Id));
        Assert.NotNull(await CarDetailsCommands.GetAsync(_db, seedId));
    }

    [Fact]
    public async Task CarDetailsJsonSeed_SkipsInsert_WhenTypeKeyExistsWithOtherId()
    {
        var created = await CarDetailsCommands.CreateAsync(_db, new CarDetailsInput("Homebrew", "Special", "XYZ", 2004));
        Assert.True(created.Saved);
        var otherId = Guid.NewGuid();
        await CarDetailsJsonSeed.UpsertAsync(_db, [new CarDetailsJsonRow(otherId, "Homebrew", "Special", "XYZ", 2004, 2010)]);
        Assert.Null(await CarDetailsCommands.GetAsync(_db, otherId));
        Assert.Equal(created.Type!.Id, (await _db.CarDetails.SingleAsync(t => t.Id == created.Type.Id)).Id);
    }

    [Fact]
    public async Task DbInitializer_SeededCatalogue_InsertsMappedRows()
    {
        var row = CarDetailsCatalogueMapper.Map([Row("BMW", "5 series", "5 series (E60)", "2004", "2010")])[0];
        var json = $$"""[{"id":"{{row.Id}}","make":"BMW","model":"5 series (E60)","modelNumber":"E60","year":2004,"endYear":2010}]""";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
        await DbInitializer.SeedCarDetailsAsync(_db, stream);
        var loaded = await CarDetailsCommands.GetAsync(_db, row.Id);
        Assert.Equal("E60", loaded!.ModelNumber);
        Assert.Equal(2010, loaded.EndYear);
    }

    [Fact]
    public async Task WikidataChassisLookup_OnError_DoesNotThrowToCaller()
    {
        using var client = new HttpClient(new FailHandler()) { Timeout = TimeSpan.FromMilliseconds(20) };
        var chassis = await WikidataChassisLookup.TryGetChassisAsync("Jaguar", "E-Pace", client);
        Assert.Null(chassis);
        var rows = CarDetailsCatalogueMapper.Map(
            [Row("Jaguar", "E-Pace", "E-Pace", "2017", "")],
            chassisLookup: (brand, model) => WikidataChassisLookup.TryGetChassisAsync(brand, model, client).GetAwaiter().GetResult());
        Assert.Equal("E-Pace", Assert.Single(rows).ModelNumber);
    }

    [Fact]
    public async Task CarCommands_Create_StillRequiresEngineCode()
    {
        await using var image = new MemoryStream(CarCommandsTests.PngBytes());
        var result = await CarCommands.CreateAsync(
            _db,
            _root,
            new CarInput("the daily", "BMW", "545", "E60", "", "AB12 CDE", 2004, "WBA12345678901234"),
            image,
            "image/png");
        Assert.Equal(CarWriteStatus.MissingField, result.Status);
    }

    private static TiresaddictGenerationRow Row(string brand, string model, string mod, string start, string end) =>
        new()
        {
            Brand = brand,
            Model = model,
            Gen = "1",
            Mod = mod,
            StartYear = start,
            EndYear = end,
        };

    private sealed class FailHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
            {
                Content = new StringContent("no", Encoding.UTF8, "text/plain"),
            });
    }
}
