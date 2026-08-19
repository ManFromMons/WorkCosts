using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using WorkCosts.Data;

namespace WorkCosts;

/// <summary>Used by dotnet-ef to create migrations at design time.</summary>
public sealed class WorkCostsDbContextFactory : IDesignTimeDbContextFactory<WorkCostsDbContext>
{
    public WorkCostsDbContext CreateDbContext(string[] args)
    {
        var folder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "WorkCosts");
        Directory.CreateDirectory(folder);
        var dbPath = Path.Combine(folder, "workcosts.db");

        var options = new DbContextOptionsBuilder<WorkCostsDbContext>()
            .UseSqlite($"Data Source={dbPath}")
            .Options;

        return new WorkCostsDbContext(options);
    }
}
