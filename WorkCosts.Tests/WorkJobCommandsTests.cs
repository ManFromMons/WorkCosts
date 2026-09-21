using Microsoft.EntityFrameworkCore;
using WorkCosts.Data;
using WorkCosts.Models;
using Xunit;

namespace WorkCosts.Tests;

public sealed class WorkJobCommandsTests : IAsyncLifetime
{
    private string _root = null!;
    private WorkCostsDbContext _db = null!;

    public async Task InitializeAsync()
    {
        _root = Path.Combine(Path.GetTempPath(), "workcosts_workjob_" + Guid.NewGuid().ToString("N"));
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
    public async Task Definition_CreateListUpdateDelete_UnknownJob_NoWrite()
    {
        Assert.Null(await WorkJobCommands.CreateDefinitionAsync(_db, Guid.NewGuid(), "Oil change"));
        Assert.Equal(0, await _db.WorkJobs.CountAsync());

        var created = await WorkJobCommands.CreateDefinitionAsync(_db, DbInitializer.OilServiceJobId, "  Drain oil  ");
        Assert.NotNull(created);
        Assert.True(created!.IsDefinition);
        Assert.Equal("Drain oil", created.Title);
        Assert.Equal(0, created.SortOrder);
        Assert.Null(created.CarId);

        var listed = await WorkJobCommands.ListDefinitionsAsync(_db, DbInitializer.OilServiceJobId);
        Assert.Single(listed);
        Assert.Equal(created.Id, listed[0].Id);

        var updated = await WorkJobCommands.UpdateDefinitionAsync(_db, created.Id, "Replace filter", 4);
        Assert.NotNull(updated);
        Assert.Equal("Replace filter", updated!.Title);
        Assert.Equal(4, updated.SortOrder);

        Assert.Null(await WorkJobCommands.UpdateDefinitionAsync(_db, Guid.NewGuid(), "Nope"));
        Assert.Null(await WorkJobCommands.CreateDefinitionAsync(_db, DbInitializer.OilServiceJobId, "   "));
        Assert.Equal(WorkJobDeleteResult.NotFound, await WorkJobCommands.TryDeleteDefinitionAsync(_db, Guid.NewGuid()));

        Assert.Equal(WorkJobDeleteResult.Deleted, await WorkJobCommands.TryDeleteDefinitionAsync(_db, created.Id));
        Assert.Empty(await WorkJobCommands.ListDefinitionsAsync(_db, DbInitializer.OilServiceJobId));
    }

    [Fact]
    public async Task Definition_Create_AssignsSortOrder_AfterExisting()
    {
        var first = await WorkJobCommands.CreateDefinitionAsync(_db, DbInitializer.OilServiceJobId, "First");
        var second = await WorkJobCommands.CreateDefinitionAsync(_db, DbInitializer.OilServiceJobId, "Second");
        Assert.Equal(0, first!.SortOrder);
        Assert.Equal(1, second!.SortOrder);

        var listed = await WorkJobCommands.ListDefinitionsAsync(_db, DbInitializer.OilServiceJobId);
        Assert.Equal([first.Id, second.Id], listed.Select(w => w.Id).ToArray());
    }

    [Fact]
    public async Task Definition_Delete_RemovesItems_LeavesCopiedInstances()
    {
        var product = await AddProductAsync("Filter", 12.50m);
        var definition = await WorkJobCommands.CreateDefinitionAsync(_db, DbInitializer.OilServiceJobId, "Filter");
        var item = await WorkJobCommands.AddDefinitionItemAsync(_db, definition!.Id, product.Id, 2);
        Assert.NotNull(item);

        var copies = await WorkJobCommands.CopyDefinitionsToInstancesAsync(_db, DbInitializer.OilServiceJobId);
        Assert.NotNull(copies);
        Assert.Single(copies!);
        var instanceId = copies[0].Id;
        var instanceItemId = copies[0].Items.Single().Id;

        Assert.Equal(WorkJobDeleteResult.Deleted, await WorkJobCommands.TryDeleteDefinitionAsync(_db, definition.Id));
        Assert.False(await _db.WorkJobs.AnyAsync(w => w.Id == definition.Id));
        Assert.False(await _db.WorkJobItems.AnyAsync(i => i.Id == item!.Id));
        Assert.True(await _db.WorkJobs.AnyAsync(w => w.Id == instanceId && !w.IsDefinition));
        Assert.True(await _db.WorkJobItems.AnyAsync(i => i.Id == instanceItemId));
    }

    [Fact]
    public async Task DefinitionItem_AddUpdateRemove_UnknownProduct_NoWrite()
    {
        var definition = await WorkJobCommands.CreateDefinitionAsync(_db, DbInitializer.OilServiceJobId, "Oil");
        Assert.Null(await WorkJobCommands.AddDefinitionItemAsync(_db, definition!.Id, Guid.NewGuid(), 1));
        Assert.Equal(0, await _db.WorkJobItems.CountAsync());

        var product = await AddProductAsync("Oil", 8.25m);
        var added = await WorkJobCommands.AddDefinitionItemAsync(_db, definition.Id, product.Id, 3);
        Assert.NotNull(added);
        Assert.Equal(3, added!.Quantity);
        Assert.Equal(8.25m, added.UnitCostSnapshot);

        Assert.Null(await WorkJobCommands.AddDefinitionItemAsync(_db, definition.Id, product.Id, 0));
        var updated = await WorkJobCommands.UpdateDefinitionItemQuantityAsync(_db, added.Id, 5);
        Assert.Equal(5, updated!.Quantity);
        Assert.Null(await WorkJobCommands.UpdateDefinitionItemQuantityAsync(_db, added.Id, 0));

        Assert.Equal(WorkJobDeleteResult.Deleted, await WorkJobCommands.RemoveDefinitionItemAsync(_db, added.Id));
        Assert.Equal(0, await _db.WorkJobItems.CountAsync());
        Assert.Equal(WorkJobDeleteResult.NotFound, await WorkJobCommands.RemoveDefinitionItemAsync(_db, added.Id));
    }

    [Fact]
    public async Task DefinitionItem_DuplicateProduct_NoWrite()
    {
        var definition = await WorkJobCommands.CreateDefinitionAsync(_db, DbInitializer.OilServiceJobId, "Oil");
        var product = await AddProductAsync("Oil", 8.25m);
        Assert.NotNull(await WorkJobCommands.AddDefinitionItemAsync(_db, definition!.Id, product.Id, 1));
        Assert.Null(await WorkJobCommands.AddDefinitionItemAsync(_db, definition.Id, product.Id, 2));
        Assert.Equal(1, await _db.WorkJobItems.CountAsync());
    }

    [Fact]
    public async Task Copy_AllDefinitions_ClonesTitleAndItems_NewIds()
    {
        var product = await AddProductAsync("Oil", 8.25m);
        var definition = await WorkJobCommands.CreateDefinitionAsync(_db, DbInitializer.OilServiceJobId, "Drain");
        await WorkJobCommands.AddDefinitionItemAsync(_db, definition!.Id, product.Id, 2);

        var copies = await WorkJobCommands.CopyDefinitionsToInstancesAsync(_db, DbInitializer.OilServiceJobId);
        Assert.NotNull(copies);
        var copy = Assert.Single(copies!);
        Assert.NotEqual(definition.Id, copy.Id);
        Assert.False(copy.IsDefinition);
        Assert.Equal(0, copy.SortOrder);
        Assert.Equal("Drain", copy.Title);
        Assert.Equal(DbInitializer.OilServiceJobId, copy.JobId);
        Assert.Null(copy.CarId);
        var copyItem = Assert.Single(copy.Items);
        Assert.NotEqual((await _db.WorkJobItems.SingleAsync(i => i.WorkJobId == definition.Id)).Id, copyItem.Id);
        Assert.Equal(product.Id, copyItem.ProductId);
        Assert.Equal((short)2, copyItem.Quantity);
        Assert.Equal(8.25m, copyItem.UnitCostSnapshot);
    }

    [Fact]
    public async Task Copy_Subset_OnlySelected_InGivenOrder()
    {
        var a = await WorkJobCommands.CreateDefinitionAsync(_db, DbInitializer.OilServiceJobId, "A");
        var b = await WorkJobCommands.CreateDefinitionAsync(_db, DbInitializer.OilServiceJobId, "B");
        var c = await WorkJobCommands.CreateDefinitionAsync(_db, DbInitializer.OilServiceJobId, "C");

        var copies = await WorkJobCommands.CopyDefinitionsToInstancesAsync(
            _db,
            DbInitializer.OilServiceJobId,
            [c!.Id, a!.Id]);
        Assert.NotNull(copies);
        Assert.Equal(["C", "A"], copies.Select(w => w.Title).ToArray());
        Assert.DoesNotContain(copies, w => w.Title == "B");
        Assert.True(copies.All(w => !w.IsDefinition));
        _ = b;
    }

    [Fact]
    public async Task Copy_Subset_ForeignOrInstanceId_NoWrite()
    {
        var definition = await WorkJobCommands.CreateDefinitionAsync(_db, DbInitializer.OilServiceJobId, "Drain");
        var other = await WorkJobCommands.CreateDefinitionAsync(_db, DbInitializer.BrakePadsJobId, "Pads");
        var instance = new WorkJob
        {
            JobId = DbInitializer.OilServiceJobId,
            Title = "Instance",
            IsDefinition = false,
        };
        _db.WorkJobs.Add(instance);
        await _db.SaveChangesAsync();
        var before = await _db.WorkJobs.CountAsync();

        Assert.Null(await WorkJobCommands.CopyDefinitionsToInstancesAsync(
            _db,
            DbInitializer.OilServiceJobId,
            [definition!.Id, other!.Id]));
        Assert.Null(await WorkJobCommands.CopyDefinitionsToInstancesAsync(
            _db,
            DbInitializer.OilServiceJobId,
            [instance.Id]));
        Assert.Null(await WorkJobCommands.CopyDefinitionsToInstancesAsync(
            _db,
            DbInitializer.OilServiceJobId,
            [definition.Id, definition.Id]));
        Assert.Null(await WorkJobCommands.CopyDefinitionsToInstancesAsync(_db, Guid.NewGuid()));
        Assert.Equal(before, await _db.WorkJobs.CountAsync());
    }

    [Fact]
    public async Task Copy_EmptyDefinitions_EmptyResult()
    {
        var copies = await WorkJobCommands.CopyDefinitionsToInstancesAsync(_db, DbInitializer.OilServiceJobId);
        Assert.NotNull(copies);
        Assert.Empty(copies!);
    }

    [Fact]
    public async Task Copy_SkipsItem_WhenProductMissing()
    {
        var keep = await AddProductAsync("Keep", 1m);
        var gone = await AddProductAsync("Gone", 2m);
        var definition = await WorkJobCommands.CreateDefinitionAsync(_db, DbInitializer.OilServiceJobId, "Mix");
        await WorkJobCommands.AddDefinitionItemAsync(_db, definition!.Id, keep.Id, 1);
        await WorkJobCommands.AddDefinitionItemAsync(_db, definition.Id, gone.Id, 1);

        await _db.Database.ExecuteSqlRawAsync("PRAGMA foreign_keys = OFF;");
        await _db.Database.ExecuteSqlRawAsync("DELETE FROM Products WHERE Id = {0};", gone.Id);
        await _db.Database.ExecuteSqlRawAsync("PRAGMA foreign_keys = ON;");

        var copies = await WorkJobCommands.CopyDefinitionsToInstancesAsync(_db, DbInitializer.OilServiceJobId);
        var copy = Assert.Single(copies!);
        var item = Assert.Single(copy.Items);
        Assert.Equal(keep.Id, item.ProductId);
    }

    [Fact]
    public async Task InstanceList_OmitsDefinitions()
    {
        await WorkJobCommands.CreateDefinitionAsync(_db, DbInitializer.OilServiceJobId, "Recipe");
        _db.WorkJobs.Add(new WorkJob
        {
            JobId = DbInitializer.OilServiceJobId,
            Title = "Doing it",
            IsDefinition = false,
        });
        await _db.SaveChangesAsync();

        var instances = await WorkJobCommands.ListInstancesAsync(_db);
        Assert.Single(instances);
        Assert.Equal("Doing it", instances[0].Title);
        Assert.False(instances[0].IsDefinition);
    }

    private async Task<Product> AddProductAsync(string name, decimal unitCost)
    {
        var product = new Product { Name = name, CategoryId = DbInitializer.ToolsId, UnitCost = unitCost };
        _db.Products.Add(product);
        await _db.SaveChangesAsync();
        return product;
    }
}
