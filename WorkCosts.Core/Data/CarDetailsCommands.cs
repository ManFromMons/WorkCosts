using Microsoft.EntityFrameworkCore;
using WorkCosts.Models;

namespace WorkCosts.Data;

public enum CarDetailsWriteStatus
{
    Saved,
    NotFound,
    MissingField,
    YearOutOfRange,
    DuplicateType,
}

public sealed record CarDetailsWriteResult(CarDetailsWriteStatus Status, string? Detail, CarDetails? Type)
{
    public bool Saved => Status == CarDetailsWriteStatus.Saved;
}

public sealed record CarDetailsInput(
    string Make,
    string Model,
    string ModelNumber,
    int Year,
    string EngineType);

public enum CarDetailsDeleteResult
{
    NotFound,
    Deleted,
    InUse,
}

public static class CarDetailsCommands
{
    public const int MaxMakeLength = 120;
    public const int MaxModelLength = 120;
    public const int MaxModelNumberLength = 32;
    public const int MaxEngineTypeLength = 200;
    public const int MinModelYear = CarCommands.MinModelYear;

    public static int MaxModelYear(DateTimeOffset now) => CarCommands.MaxModelYear(now);

    public static string NormalizeTypeKey(string make, string modelNumber, int year, string engineType) =>
        $"{make.Trim().ToUpperInvariant()}|{modelNumber.Trim().ToUpperInvariant()}|{year}|{engineType.Trim().ToUpperInvariant()}";

    public static async Task<CarDetailsWriteResult> CreateAsync(
        WorkCostsDbContext db,
        CarDetailsInput input,
        CancellationToken cancellationToken = default,
        DateTimeOffset? utcNow = null)
    {
        var now = utcNow ?? DateTimeOffset.Now;
        var invalid = Validate(input, now);
        if (invalid is not null)
        {
            return invalid;
        }

        var typeKey = NormalizeTypeKey(input.Make, input.ModelNumber, input.Year, input.EngineType);
        if (await db.CarDetails.AnyAsync(t => t.TypeKey == typeKey, cancellationToken))
        {
            return Fail(CarDetailsWriteStatus.DuplicateType, "A car type with this make, model number, year, and engine already exists.");
        }

        var entity = new CarDetails
        {
            Make = input.Make.Trim(),
            Model = input.Model.Trim(),
            ModelNumber = input.ModelNumber.Trim(),
            Year = input.Year,
            EngineType = input.EngineType.Trim(),
            TypeKey = typeKey,
        };
        db.CarDetails.Add(entity);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            db.Entry(entity).State = EntityState.Detached;
            return Fail(CarDetailsWriteStatus.DuplicateType, "A car type with this make, model number, year, and engine already exists.");
        }

        return new CarDetailsWriteResult(CarDetailsWriteStatus.Saved, null, entity);
    }

    public static Task<CarDetails?> GetAsync(
        WorkCostsDbContext db,
        Guid typeId,
        CancellationToken cancellationToken = default) =>
        db.CarDetails.AsNoTracking().FirstOrDefaultAsync(t => t.Id == typeId, cancellationToken);

    public static Task<List<CarDetails>> ListAsync(
        WorkCostsDbContext db,
        CancellationToken cancellationToken = default) =>
        db.CarDetails.AsNoTracking()
            .OrderBy(t => t.Make)
            .ThenBy(t => t.ModelNumber)
            .ThenBy(t => t.Year)
            .ThenBy(t => t.EngineType)
            .ThenBy(t => t.Id)
            .ToListAsync(cancellationToken);

    public static async Task<CarDetailsWriteResult> UpdateAsync(
        WorkCostsDbContext db,
        Guid typeId,
        CarDetailsInput input,
        CancellationToken cancellationToken = default,
        DateTimeOffset? utcNow = null)
    {
        var now = utcNow ?? DateTimeOffset.Now;
        var invalid = Validate(input, now);
        if (invalid is not null)
        {
            return invalid;
        }

        var entity = await db.CarDetails.FirstOrDefaultAsync(t => t.Id == typeId, cancellationToken);
        if (entity is null)
        {
            return Fail(CarDetailsWriteStatus.NotFound, "This car type no longer exists.");
        }

        var typeKey = NormalizeTypeKey(input.Make, input.ModelNumber, input.Year, input.EngineType);
        if (await db.CarDetails.AnyAsync(t => t.TypeKey == typeKey && t.Id != typeId, cancellationToken))
        {
            return Fail(CarDetailsWriteStatus.DuplicateType, "A car type with this make, model number, year, and engine already exists.");
        }

        entity.Make = input.Make.Trim();
        entity.Model = input.Model.Trim();
        entity.ModelNumber = input.ModelNumber.Trim();
        entity.Year = input.Year;
        entity.EngineType = input.EngineType.Trim();
        entity.TypeKey = typeKey;

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            await db.Entry(entity).ReloadAsync(cancellationToken);
            return Fail(CarDetailsWriteStatus.DuplicateType, "A car type with this make, model number, year, and engine already exists.");
        }

        return new CarDetailsWriteResult(CarDetailsWriteStatus.Saved, null, entity);
    }

    public static async Task<CarDetailsDeleteResult> TryDeleteAsync(
        WorkCostsDbContext db,
        Guid typeId,
        CancellationToken cancellationToken = default)
    {
        var entity = await db.CarDetails.FirstOrDefaultAsync(t => t.Id == typeId, cancellationToken);
        if (entity is null)
        {
            return CarDetailsDeleteResult.NotFound;
        }

        if (await IsInUseAsync(db, typeId, cancellationToken))
        {
            return CarDetailsDeleteResult.InUse;
        }

        db.CarDetails.Remove(entity);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            return CarDetailsDeleteResult.InUse;
        }

        return CarDetailsDeleteResult.Deleted;
    }

    public static async Task<bool> ReplaceJobCarDetailsAsync(
        WorkCostsDbContext db,
        Guid jobId,
        IReadOnlyList<Guid> typeIdsInOrder,
        CancellationToken cancellationToken = default)
    {
        if (!await db.Jobs.AnyAsync(j => j.Id == jobId, cancellationToken))
        {
            return false;
        }

        var ordered = new List<Guid>();
        var seen = new HashSet<Guid>();
        foreach (var id in typeIdsInOrder)
        {
            if (seen.Add(id))
            {
                ordered.Add(id);
            }
        }

        if (ordered.Count > 0)
        {
            var found = await db.CarDetails.CountAsync(t => ordered.Contains(t.Id), cancellationToken);
            if (found != ordered.Count)
            {
                return false;
            }
        }

        await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);
        await db.JobCarDetails
            .Where(link => link.JobId == jobId)
            .ExecuteDeleteAsync(cancellationToken);
        db.ChangeTracker.Clear();

        var sort = 0;
        foreach (var typeId in ordered)
        {
            db.JobCarDetails.Add(new JobCarDetails
            {
                JobId = jobId,
                CarDetailsId = typeId,
                SortOrder = sort++,
            });
        }

        await db.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);
        return true;
    }

    public static Task<List<JobCarDetails>> ListJobCarDetailsAsync(
        WorkCostsDbContext db,
        Guid jobId,
        CancellationToken cancellationToken = default) =>
        db.JobCarDetails.AsNoTracking()
            .Where(link => link.JobId == jobId)
            .OrderBy(link => link.SortOrder)
            .ThenBy(link => link.CarDetailsId)
            .ToListAsync(cancellationToken);

    private static async Task<bool> IsInUseAsync(
        WorkCostsDbContext db,
        Guid typeId,
        CancellationToken cancellationToken) =>
        await db.Cars.AnyAsync(c => c.CarDetailsId == typeId, cancellationToken)
        || await db.JobCarDetails.AnyAsync(link => link.CarDetailsId == typeId, cancellationToken)
        || await db.GarageJobs.AnyAsync(g => g.CarDetailsId == typeId, cancellationToken)
        || await db.ItemsOfWork.AnyAsync(i => i.CarDetailsId == typeId, cancellationToken);

    private static CarDetailsWriteResult? Validate(CarDetailsInput input, DateTimeOffset now)
    {
        var missing = FirstMissing(input);
        if (missing is not null)
        {
            return Fail(CarDetailsWriteStatus.MissingField, missing);
        }

        if (input.Year < MinModelYear || input.Year > MaxModelYear(now))
        {
            return Fail(
                CarDetailsWriteStatus.YearOutOfRange,
                $"Model year must be from {MinModelYear} to {MaxModelYear(now)}.");
        }

        return null;
    }

    private static string? FirstMissing(CarDetailsInput input)
    {
        if (IsBlank(input.Make, MaxMakeLength))
        {
            return FieldMessage("Make", input.Make, MaxMakeLength);
        }

        if (IsBlank(input.Model, MaxModelLength))
        {
            return FieldMessage("Model", input.Model, MaxModelLength);
        }

        if (IsBlank(input.ModelNumber, MaxModelNumberLength))
        {
            return FieldMessage("Model number", input.ModelNumber, MaxModelNumberLength);
        }

        if (IsBlank(input.EngineType, MaxEngineTypeLength))
        {
            return FieldMessage("Engine type", input.EngineType, MaxEngineTypeLength);
        }

        return null;
    }

    private static bool IsBlank(string? value, int max) =>
        string.IsNullOrWhiteSpace(value) || value.Trim().Length > max;

    private static string FieldMessage(string label, string? value, int max)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return $"{label} is required.";
        }

        return $"{label} must be at most {max} characters.";
    }

    private static bool IsUniqueViolation(DbUpdateException ex)
    {
        var message = ex.InnerException?.Message ?? ex.Message;
        return message.Contains("UNIQUE", StringComparison.OrdinalIgnoreCase);
    }

    private static CarDetailsWriteResult Fail(CarDetailsWriteStatus status, string detail) =>
        new(status, detail, null);
}
