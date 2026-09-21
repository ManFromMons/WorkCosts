using Microsoft.EntityFrameworkCore;
using WorkCosts.Data;
using WorkCosts.Models;
using WorkCosts.Services;
using Xunit;

namespace WorkCosts.Tests;

public sealed class CarCommandsTests : IAsyncLifetime
{
    private string _root = null!;
    private WorkCostsDbContext _db = null!;

    public async Task InitializeAsync()
    {
        _root = Path.Combine(Path.GetTempPath(), "workcosts_cars_" + Guid.NewGuid().ToString("N"));
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
    public async Task CarCommands_CreateListGetUpdate_RequiresAllFields()
    {
        var created = await CreateSampleAsync();
        Assert.Equal(CarWriteStatus.Saved, created.Status);
        Assert.Equal("E60", created.Car!.ModelNumber);
        Assert.False(string.IsNullOrWhiteSpace(created.Car.ImageRelativePath));
        Assert.Equal("image/png", created.Car.ImageContentType);

        var listed = await CarCommands.ListActiveAsync(_db);
        Assert.Contains(listed, car => car.Id == created.Car.Id);

        var loaded = await CarCommands.GetAsync(_db, created.Car.Id);
        Assert.NotNull(loaded);
        Assert.Equal("the daily", loaded!.Name);
        Assert.Equal("BMW", loaded.Make);
        Assert.Equal("545", loaded.Model);
        Assert.Equal("4.4 V8", loaded.EngineType);
        Assert.Equal("AB12 CDE", loaded.Vrm);
        Assert.Equal(2004, loaded.Year);
        Assert.Equal("WBA12345678901234", loaded.Vin);

        var updated = await CarCommands.UpdateAsync(
            _db,
            _root,
            created.Car.Id,
            Sample() with { Name = "weekend" },
            replacementImage: null,
            replacementContentType: null);
        Assert.True(updated.Saved);
        Assert.Equal("weekend", updated.Car!.Name);
        Assert.Equal("E60", updated.Car.ModelNumber);
        Assert.Equal(created.Car.ImageRelativePath, updated.Car.ImageRelativePath);
        Assert.True(updated.Car.UpdatedAt >= created.Car.UpdatedAt);
    }

    [Fact]
    public async Task CarCommands_Create_EmptyVehicleOrderJson_AndUpdatedAt()
    {
        var before = DateTimeOffset.Now.AddSeconds(-2);
        var created = await CreateSampleAsync();
        var after = DateTimeOffset.Now.AddSeconds(2);

        Assert.True(created.Saved);
        Assert.Equal(string.Empty, created.Car!.VehicleOrderJson);
        Assert.Null(created.Car.DeletedAt);
        Assert.InRange(created.Car.UpdatedAt, before, after);
    }

    [Fact]
    public async Task CarCommands_RejectsMissingImage_OrEmptyNicknameMakeModelModelNumberEngineVrmVin()
    {
        var missingImage = await CarCommands.CreateAsync(_db, _root, Sample(), image: null, imageContentType: null);
        Assert.Equal(CarWriteStatus.MissingImage, missingImage.Status);

        await AssertRejectedAsync(Sample() with { Name = " " });
        await AssertRejectedAsync(Sample() with { Make = "" });
        await AssertRejectedAsync(Sample() with { Model = "  " });
        await AssertRejectedAsync(Sample() with { ModelNumber = "" });
        await AssertRejectedAsync(Sample() with { EngineType = "" });
        await AssertRejectedAsync(Sample() with { Vrm = "" });
        await AssertRejectedAsync(Sample() with { Vin = "" });
        Assert.Empty(await CarCommands.ListActiveAsync(_db));
    }

    [Fact]
    public async Task CarCommands_YearOutOfRange_Rejected()
    {
        var tooOld = await CreateSampleAsync(Sample() with { Year = 1899 });
        Assert.Equal(CarWriteStatus.YearOutOfRange, tooOld.Status);

        var tooNew = await CreateSampleAsync(Sample() with { Year = DateTime.Now.Year + 2 });
        Assert.Equal(CarWriteStatus.YearOutOfRange, tooNew.Status);
        Assert.Empty(await CarCommands.ListActiveAsync(_db));
    }

    [Fact]
    public async Task CarCommands_VrmUnique_AmongActive_IgnoresSpacesAndCase()
    {
        var first = await CreateSampleAsync();
        Assert.True(first.Saved);

        var lower = await CreateSampleAsync(Sample() with { Name = "other", Vrm = "ab12cde" });
        Assert.Equal(CarWriteStatus.DuplicateVrm, lower.Status);

        var spaced = await CreateSampleAsync(Sample() with { Name = "third", Vrm = "AB 12CDE" });
        Assert.Equal(CarWriteStatus.DuplicateVrm, spaced.Status);

        var different = await CreateSampleAsync(Sample() with { Name = "spare", Vrm = "XY99 ZZZ" });
        Assert.True(different.Saved);
        Assert.Equal(2, (await CarCommands.ListActiveAsync(_db)).Count);
    }

    [Fact]
    public async Task CarCommands_VrmUnique_AllowsReuse_AfterSoftDelete()
    {
        var first = await CreateSampleAsync();
        Assert.Equal(CarDeleteResult.Deleted, await CarCommands.TryDeleteAsync(_db, first.Car!.Id));

        var reuse = await CreateSampleAsync(Sample() with { Name = "again", Vrm = "ab12cde" });
        Assert.True(reuse.Saved);
        Assert.Equal(first.Car.VrmKey, reuse.Car!.VrmKey);
        Assert.NotEqual(first.Car.Id, reuse.Car.Id);
    }

    [Fact]
    public async Task CarCommands_TryDelete_SoftDeletes_SetsDeletedAtAndUpdatedAt_KeepsRowAndImage()
    {
        var created = await CreateSampleAsync();
        var relative = created.Car!.ImageRelativePath;
        var full = CarImageStore.GetFullPath(_root, relative);
        var beforeBytes = await File.ReadAllBytesAsync(full);

        var deleted = await CarCommands.TryDeleteAsync(_db, created.Car.Id);
        Assert.Equal(CarDeleteResult.Deleted, deleted);

        var row = await CarCommands.GetAsync(_db, created.Car.Id);
        Assert.NotNull(row);
        Assert.NotNull(row!.DeletedAt);
        Assert.Equal(row.DeletedAt, row.UpdatedAt);
        Assert.Equal(beforeBytes, await File.ReadAllBytesAsync(full));
        Assert.Equal(CarDeleteResult.NotFound, await CarCommands.TryDeleteAsync(_db, Guid.NewGuid()));
    }

    [Fact]
    public async Task CarCommands_List_OmitsSoftDeleted()
    {
        var kept = await CreateSampleAsync(Sample() with { Name = "kept", Vrm = "AA11 AAA" });
        var gone = await CreateSampleAsync(Sample() with { Name = "gone", Vrm = "BB22 BBB" });
        await CarCommands.TryDeleteAsync(_db, gone.Car!.Id);

        var listed = await CarCommands.ListActiveAsync(_db);
        Assert.Contains(listed, car => car.Id == kept.Car!.Id);
        Assert.DoesNotContain(listed, car => car.Id == gone.Car.Id);
    }

    [Fact]
    public async Task GarageJobCommands_Update_PersistsCarId_UnknownCar_NoWrite()
    {
        var garageJob = await GarageJobCommands.CreateAsync(_db, "Service");
        var car = await CreateSampleAsync();
        Assert.True(await GarageJobCommands.UpdateAsync(
            _db,
            garageJob.Id,
            "Renamed",
            GarageJobTargetKind.Car,
            "daily",
            "notes",
            30,
            GarageJobRepeatCombine.WhicheverFirst,
            intervalAnchorDate: null,
            carId: car.Car!.Id,
            setCarId: true));

        var loaded = await GarageJobCommands.GetByIdAsync(_db, garageJob.Id);
        Assert.Equal(car.Car.Id, loaded!.CarId);
        Assert.Equal("Renamed", loaded.Name);

        Assert.False(await GarageJobCommands.UpdateAsync(
            _db,
            garageJob.Id,
            "Nope",
            GarageJobTargetKind.Engine,
            "changed",
            "changed",
            90,
            GarageJobRepeatCombine.AllMustBeMet,
            new DateOnly(2020, 1, 1),
            carId: Guid.NewGuid(),
            setCarId: true));

        loaded = await GarageJobCommands.GetByIdAsync(_db, garageJob.Id);
        Assert.Equal("Renamed", loaded!.Name);
        Assert.Equal(car.Car.Id, loaded.CarId);
        Assert.Equal(GarageJobTargetKind.Car, loaded.TargetKind);
        Assert.Null(loaded.IntervalAnchorDate);
    }

    [Fact]
    public async Task WorkJob_PersistsCarId_Restrict_NoCascadeOnCarSoftDelete()
    {
        var template = await _db.Jobs.AsNoTracking().FirstAsync();
        var car = await CreateSampleAsync();
        var work = new WorkJob { JobId = template.Id, Title = "Pads" };
        _db.WorkJobs.Add(work);
        await _db.SaveChangesAsync();

        Assert.False(await WorkJobCommands.TrySetCarIdAsync(_db, work.Id, Guid.NewGuid()));
        Assert.Null((await _db.WorkJobs.AsNoTracking().FirstAsync(w => w.Id == work.Id)).CarId);

        Assert.True(await WorkJobCommands.TrySetCarIdAsync(_db, work.Id, car.Car!.Id));
        Assert.Equal(CarDeleteResult.Deleted, await CarCommands.TryDeleteAsync(_db, car.Car.Id));

        var still = await _db.WorkJobs.AsNoTracking().FirstAsync(w => w.Id == work.Id);
        Assert.Equal(car.Car.Id, still.CarId);
        Assert.NotNull((await CarCommands.GetAsync(_db, car.Car.Id))!.DeletedAt);
        AssertRestrict<WorkJob>();

        _db.ChangeTracker.Clear();
        _db.Cars.Remove(await _db.Cars.FirstAsync(c => c.Id == car.Car.Id));
        await Assert.ThrowsAnyAsync<DbUpdateException>(() => _db.SaveChangesAsync());
    }

    [Fact]
    public async Task ItemOfWork_PersistsCarId_Restrict_NoCascadeOnCarSoftDelete()
    {
        var garageJob = await GarageJobCommands.CreateAsync(_db, "Service");
        var car = await CreateSampleAsync();
        Assert.Null(await ItemOfWorkCommands.CreateAsync(
            _db,
            garageJob.Id,
            DateTimeOffset.Now,
            1000,
            Guid.NewGuid()));
        Assert.Empty(await ItemOfWorkCommands.ListAsync(_db, garageJob.Id));

        var item = await ItemOfWorkCommands.CreateAsync(
            _db,
            garageJob.Id,
            DateTimeOffset.Now,
            1000,
            car.Car!.Id);
        Assert.NotNull(item);
        Assert.Equal(car.Car.Id, item!.CarId);

        Assert.False(await ItemOfWorkCommands.TrySetCarIdAsync(_db, item.Id, Guid.NewGuid()));
        Assert.Equal(car.Car.Id, (await ItemOfWorkCommands.GetLatestAsync(_db, garageJob.Id))!.CarId);

        Assert.Equal(CarDeleteResult.Deleted, await CarCommands.TryDeleteAsync(_db, car.Car.Id));
        Assert.Equal(car.Car.Id, (await ItemOfWorkCommands.GetLatestAsync(_db, garageJob.Id))!.CarId);
        Assert.NotNull(await CarCommands.GetAsync(_db, car.Car.Id));
        AssertRestrict<ItemOfWork>();

        _db.ChangeTracker.Clear();
        _db.Cars.Remove(await _db.Cars.FirstAsync(c => c.Id == car.Car.Id));
        await Assert.ThrowsAnyAsync<DbUpdateException>(() => _db.SaveChangesAsync());
    }

    private void AssertRestrict<TEntity>() where TEntity : class
    {
        var foreignKey = _db.Model.FindEntityType(typeof(TEntity))!
            .GetForeignKeys()
            .Single(key => key.Properties.Any(property => property.Name == "CarId"));
        Assert.Equal(DeleteBehavior.Restrict, foreignKey.DeleteBehavior);
    }

    private async Task AssertRejectedAsync(CarInput input)
    {
        var result = await CreateSampleAsync(input);
        Assert.Equal(CarWriteStatus.MissingField, result.Status);
    }

    private Task<CarWriteResult> CreateSampleAsync() => CreateSampleAsync(Sample());

    private async Task<CarWriteResult> CreateSampleAsync(CarInput input)
    {
        await using var image = new MemoryStream(PngBytes());
        return await CarCommands.CreateAsync(_db, _root, input, image, "image/png");
    }

    private static CarInput Sample() =>
        new("the daily", "BMW", "545", "E60", "4.4 V8", "AB12 CDE", 2004, "WBA12345678901234");

    internal static byte[] PngBytes() => Convert.FromHexString(
        "89504E470D0A1A0A0000000D49484452000000010000000108060000001F15C4890000000A49444154789C63000100000500010D0A2DB40000000049454E44AE426082");
}

public sealed class CarImageStoreTests
{
    [Fact]
    public async Task CarImageStore_WriteRead_PngJpegWebp()
    {
        var root = Path.Combine(Path.GetTempPath(), "workcosts_carimg_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            await RoundTripAsync(root, CarCommandsTests.PngBytes(), "image/png");
            await RoundTripAsync(root, [0xFF, 0xD8, 0xFF, 0xD9], "image/jpeg");
            await RoundTripAsync(root, WebpBytes(), "image/webp");
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task CarImageStore_RejectsOversizeAndUnknownType()
    {
        var root = Path.Combine(Path.GetTempPath(), "workcosts_carimg_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var oversize = new byte[CarImageStore.MaxImageBytes + 1];
            CarCommandsTests.PngBytes().CopyTo(oversize, 0);
            await Assert.ThrowsAsync<ArgumentException>(() =>
                CarImageStore.WriteAsync(root, Guid.NewGuid(), new MemoryStream(oversize), "image/png"));

            var gif = new byte[] { 0x47, 0x49, 0x46, 0x38, 0x39, 0x61 };
            await Assert.ThrowsAsync<ArgumentException>(() =>
                CarImageStore.WriteAsync(root, Guid.NewGuid(), new MemoryStream(gif), "image/gif"));

            Assert.False(Directory.Exists(Path.Combine(root, "images")));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static async Task RoundTripAsync(string root, byte[] bytes, string contentType)
    {
        var id = Guid.NewGuid();
        await using var stream = new MemoryStream(bytes);
        await CarImageStore.WriteAsync(root, id, stream, contentType);
        var relative = CarImageStore.RelativePathFor(id, contentType);
        Assert.StartsWith("images/cars/", relative);
        Assert.Equal(bytes, await CarImageStore.ReadAsync(root, relative));
    }

    private static byte[] WebpBytes() =>
    [
        0x52, 0x49, 0x46, 0x46, 0x0C, 0x00, 0x00, 0x00,
        0x57, 0x45, 0x42, 0x50, 0x00, 0x00, 0x00, 0x00,
    ];
}

public sealed class CarImageSearchTests
{
    [Fact]
    public void CarImageSearch_Query_IsMakeSpaceModelNumber()
    {
        Assert.Equal("BMW E60", CarImageSearch.BuildQuery("BMW", "E60"));
        Assert.Equal("BMW E60", CarImageSearch.BuildQuery("  BMW ", " E60 "));
        var bing = CarImageSearch.BingImagesUrl(CarImageSearch.BuildQuery("BMW", "E60"));
        Assert.Equal("https://www.bing.com/images/search?q=BMW%20E60", bing);
        Assert.DoesNotContain("2004", bing, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CarImageSearch_UsesBingThenGoogle_WhenBingHasNoImages()
    {
        var bingEmpty = await File.ReadAllTextAsync(Fixture("bing-no-images.snippet.html"));
        var bingHit = await File.ReadAllTextAsync(Fixture("bing-e60.snippet.html"));
        var google = await File.ReadAllTextAsync(Fixture("google-e60.snippet.html"));
        var calls = new List<string>();

        var urls = await CarImageSearch.FindImageUrlsAsync("BMW", "E60", (url, _) =>
        {
            calls.Add(url);
            var html = url.Contains("bing.com", StringComparison.OrdinalIgnoreCase) ? bingEmpty : google;
            return Task.FromResult(new CarImageSearch.Page(html, 200));
        });

        Assert.Equal(
            [
                "https://www.bing.com/images/search?q=BMW%20E60",
                "https://www.google.com/search?tbm=isch&q=BMW%20E60",
            ],
            calls);
        Assert.Equal(["https://images.example/bmw-e60.jpg"], urls);

        calls.Clear();
        var bingOnly = await CarImageSearch.FindImageUrlsAsync("BMW", "E60", (url, _) =>
        {
            calls.Add(url);
            return Task.FromResult(new CarImageSearch.Page(bingHit, 200));
        });
        Assert.Equal(["https://www.bing.com/images/search?q=BMW%20E60"], calls);
        Assert.Equal(["https://cdn.example/bmw-e60.jpg"], bingOnly);
    }

    private static string Fixture(string name) =>
        Path.Combine(AppContext.BaseDirectory, "Fixtures", name);
}
