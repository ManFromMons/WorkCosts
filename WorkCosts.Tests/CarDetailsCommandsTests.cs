using Microsoft.EntityFrameworkCore;
using WorkCosts.Data;
using WorkCosts.Models;
using Xunit;

namespace WorkCosts.Tests;

public sealed class CarDetailsCommandsTests : IAsyncLifetime
{
    private string _root = null!;
    private WorkCostsDbContext _db = null!;

    public async Task InitializeAsync()
    {
        _root = Path.Combine(Path.GetTempPath(), "workcosts_cardetails_" + Guid.NewGuid().ToString("N"));
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
    public async Task CarDetailsCommands_CreateListUpdateDelete()
    {
        var created = await CarDetailsCommands.CreateAsync(_db, Sample());
        Assert.Equal(CarDetailsWriteStatus.Saved, created.Status);
        Assert.Equal("BMW", created.Type!.Make);
        Assert.Equal("545", created.Type.Model);
        Assert.Equal("E60", created.Type.ModelNumber);
        Assert.Equal(2004, created.Type.Year);
        Assert.Null(created.Type.EndYear);

        var listed = await CarDetailsCommands.ListAsync(_db);
        Assert.Contains(listed, type => type.Id == created.Type.Id);

        var loaded = await CarDetailsCommands.GetAsync(_db, created.Type.Id);
        Assert.Equal("E60", loaded!.ModelNumber);

        var updated = await CarDetailsCommands.UpdateAsync(
            _db,
            created.Type.Id,
            Sample() with { Model = "535" });
        Assert.True(updated.Saved);
        Assert.Equal("535", updated.Type!.Model);
        Assert.Equal("E60", updated.Type.ModelNumber);

        Assert.Equal(CarDetailsDeleteResult.Deleted, await CarDetailsCommands.TryDeleteAsync(_db, created.Type.Id));
        Assert.Null(await CarDetailsCommands.GetAsync(_db, created.Type.Id));
        Assert.Equal(CarDetailsDeleteResult.NotFound, await CarDetailsCommands.TryDeleteAsync(_db, Guid.NewGuid()));
    }

    [Fact]
    public async Task CarDetailsCommands_RejectsDuplicateMakeModelModelNumberYear()
    {
        Assert.True((await CarDetailsCommands.CreateAsync(_db, Sample())).Saved);
        var before = await _db.CarDetails.CountAsync();
        var duplicate = await CarDetailsCommands.CreateAsync(
            _db,
            Sample() with { Make = "bmw", Model = "545", ModelNumber = "e60" });
        Assert.Equal(CarDetailsWriteStatus.DuplicateType, duplicate.Status);
        Assert.Equal(before, await _db.CarDetails.CountAsync());

        var otherModel = await CarDetailsCommands.CreateAsync(_db, Sample() with { Model = "530" });
        Assert.True(otherModel.Saved);
    }

    [Fact]
    public async Task CarDetailsCommands_AllowsMissingEndYear()
    {
        var created = await CarDetailsCommands.CreateAsync(_db, Sample() with { EndYear = null });
        Assert.True(created.Saved);
        Assert.Null(created.Type!.EndYear);
    }

    [Fact]
    public async Task CarDetailsCommands_RejectsEndYearBeforeYear()
    {
        var result = await CarDetailsCommands.CreateAsync(_db, Sample() with { EndYear = 2003 });
        Assert.Equal(CarDetailsWriteStatus.YearOutOfRange, result.Status);
    }

    [Fact]
    public async Task CarDetailsCommands_Create_DoesNotRequireEngine()
    {
        var created = await CarDetailsCommands.CreateAsync(_db, new CarDetailsInput("BMW", "545", "E60", 2004));
        Assert.True(created.Saved);
        Assert.Null(created.Type!.EndYear);
    }

    [Fact]
    public void NormalizeTypeKey_DoesNotIncludeEngine()
    {
        Assert.Equal(
            "BMW|5 SERIES (E60)|E60|2004",
            CarDetailsCommands.NormalizeTypeKey("bmw", "5 series (E60)", "e60", 2004));
    }

    [Fact]
    public async Task CarDetailsCommands_TryDelete_InUseByCar_Restrict()
    {
        var type = (await CarDetailsCommands.CreateAsync(_db, Sample())).Type!;
        await CreateCarAsync(type.Id);
        Assert.Equal(CarDetailsDeleteResult.InUse, await CarDetailsCommands.TryDeleteAsync(_db, type.Id));
        Assert.NotNull(await CarDetailsCommands.GetAsync(_db, type.Id));
    }

    [Fact]
    public async Task CarDetailsCommands_TryDelete_InUseByJobJunction_Restrict()
    {
        var type = (await CarDetailsCommands.CreateAsync(_db, Sample())).Type!;
        var job = await _db.Jobs.AsNoTracking().FirstAsync();
        Assert.True(await CarDetailsCommands.ReplaceJobCarDetailsAsync(_db, job.Id, [type.Id]));
        Assert.Equal(CarDetailsDeleteResult.InUse, await CarDetailsCommands.TryDeleteAsync(_db, type.Id));
        Assert.NotNull(await CarDetailsCommands.GetAsync(_db, type.Id));
    }

    [Fact]
    public async Task CarCommands_Update_PersistsOptionalCarDetailsId()
    {
        var type = (await CarDetailsCommands.CreateAsync(_db, Sample())).Type!;
        var car = await CreateCarAsync(null);
        Assert.Null(car.CarDetailsId);

        var updated = await CarCommands.UpdateAsync(
            _db,
            _root,
            car.Id,
            SampleCar() with { CarDetailsId = type.Id },
            replacementImage: null,
            replacementContentType: null);
        Assert.True(updated.Saved);
        Assert.Equal(type.Id, updated.Car!.CarDetailsId);

        var unknown = await CarCommands.UpdateAsync(
            _db,
            _root,
            car.Id,
            SampleCar() with { CarDetailsId = Guid.NewGuid() },
            replacementImage: null,
            replacementContentType: null);
        Assert.Equal(CarWriteStatus.UnknownCarDetails, unknown.Status);
        Assert.Equal(type.Id, (await CarCommands.GetAsync(_db, car.Id))!.CarDetailsId);

        var cleared = await CarCommands.UpdateAsync(
            _db,
            _root,
            car.Id,
            SampleCar(),
            replacementImage: null,
            replacementContentType: null);
        Assert.True(cleared.Saved);
        Assert.Null(cleared.Car!.CarDetailsId);
    }

    [Fact]
    public async Task JobCarDetails_Replace_DedupesAndOrders()
    {
        var first = (await CarDetailsCommands.CreateAsync(_db, Sample())).Type!;
        var second = (await CarDetailsCommands.CreateAsync(_db, Sample() with { Model = "530" })).Type!;
        var job = await _db.Jobs.AsNoTracking().FirstAsync();

        Assert.False(await CarDetailsCommands.ReplaceJobCarDetailsAsync(_db, Guid.NewGuid(), [first.Id]));
        Assert.False(await CarDetailsCommands.ReplaceJobCarDetailsAsync(_db, job.Id, [first.Id, Guid.NewGuid()]));
        Assert.Empty(await CarDetailsCommands.ListJobCarDetailsAsync(_db, job.Id));

        Assert.True(await CarDetailsCommands.ReplaceJobCarDetailsAsync(
            _db,
            job.Id,
            [second.Id, first.Id, second.Id, first.Id]));
        var links = await CarDetailsCommands.ListJobCarDetailsAsync(_db, job.Id);
        Assert.Equal(2, links.Count);
        Assert.Equal(second.Id, links[0].CarDetailsId);
        Assert.Equal(0, links[0].SortOrder);
        Assert.Equal(first.Id, links[1].CarDetailsId);
        Assert.Equal(1, links[1].SortOrder);
    }

    [Fact]
    public async Task GarageJobCommands_Update_SnapshotsCarDetailsId()
    {
        var type = (await CarDetailsCommands.CreateAsync(_db, Sample())).Type!;
        var car = await CreateCarAsync(type.Id);
        var garageJob = await GarageJobCommands.CreateAsync(_db, "Service");

        Assert.True(await GarageJobCommands.UpdateAsync(
            _db,
            garageJob.Id,
            "Service",
            GarageJobTargetKind.Car,
            "daily",
            "",
            60,
            GarageJobRepeatCombine.WhicheverFirst,
            null,
            car.Id,
            setCarId: true));
        Assert.Equal(type.Id, (await GarageJobCommands.GetByIdAsync(_db, garageJob.Id))!.CarDetailsId);

        var later = (await CarDetailsCommands.CreateAsync(_db, Sample() with { Model = "530" })).Type!;
        var moved = await CarCommands.UpdateAsync(
            _db,
            _root,
            car.Id,
            SampleCar() with { CarDetailsId = later.Id },
            replacementImage: null,
            replacementContentType: null);
        Assert.True(moved.Saved);
        Assert.Equal(type.Id, (await GarageJobCommands.GetByIdAsync(_db, garageJob.Id))!.CarDetailsId);

        Assert.True(await GarageJobCommands.UpdateAsync(
            _db,
            garageJob.Id,
            "Service",
            GarageJobTargetKind.Car,
            "daily",
            "",
            60,
            GarageJobRepeatCombine.WhicheverFirst,
            null));
        Assert.Equal(later.Id, (await GarageJobCommands.GetByIdAsync(_db, garageJob.Id))!.CarDetailsId);
    }

    [Fact]
    public async Task ItemOfWork_Create_SnapshotsCarDetailsId_FromCar()
    {
        var type = (await CarDetailsCommands.CreateAsync(_db, Sample())).Type!;
        var car = await CreateCarAsync(type.Id);
        var garageJob = await GarageJobCommands.CreateAsync(_db, "Service");

        var none = await ItemOfWorkCommands.CreateAsync(_db, garageJob.Id, DateTimeOffset.Now, 1000);
        Assert.Null(none!.CarDetailsId);

        var item = await ItemOfWorkCommands.CreateAsync(_db, garageJob.Id, DateTimeOffset.Now, 1100, car.Id);
        Assert.Equal(type.Id, item!.CarDetailsId);
        Assert.Equal(car.Id, item.CarId);
    }

    [Fact]
    public async Task ItemOfWork_Snapshot_DoesNotFollowLaterCarTypeChange()
    {
        var type = (await CarDetailsCommands.CreateAsync(_db, Sample())).Type!;
        var later = (await CarDetailsCommands.CreateAsync(_db, Sample() with { Model = "530" })).Type!;
        var car = await CreateCarAsync(type.Id);
        var garageJob = await GarageJobCommands.CreateAsync(_db, "Service");
        var item = await ItemOfWorkCommands.CreateAsync(_db, garageJob.Id, DateTimeOffset.Now, 1200, car.Id);

        await CarCommands.UpdateAsync(
            _db,
            _root,
            car.Id,
            SampleCar() with { CarDetailsId = later.Id },
            replacementImage: null,
            replacementContentType: null);

        Assert.Equal(type.Id, (await ItemOfWorkCommands.GetLatestAsync(_db, garageJob.Id))!.CarDetailsId);
        Assert.Equal(later.Id, (await CarCommands.GetAsync(_db, car.Id))!.CarDetailsId);
    }

    private async Task<Car> CreateCarAsync(Guid? typeId)
    {
        await using var image = new MemoryStream(CarCommandsTests.PngBytes());
        var result = await CarCommands.CreateAsync(
            _db,
            _root,
            SampleCar() with { CarDetailsId = typeId },
            image,
            "image/png");
        Assert.True(result.Saved);
        return result.Car!;
    }

    private static CarDetailsInput Sample() =>
        new("BMW", "545", "E60", 2004);

    private static CarInput SampleCar() =>
        new("the daily", "BMW", "545", "E60", "4.4 V8", "AB12 CDE", 2004, "WBA12345678901234");
}
