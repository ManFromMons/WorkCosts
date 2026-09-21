using Microsoft.EntityFrameworkCore;
using WorkCosts.Models;
using WorkCosts.Services;

namespace WorkCosts.Data;

public enum CarWriteStatus
{
    Saved,
    NotFound,
    MissingField,
    YearOutOfRange,
    MissingImage,
    DuplicateVrm,
    ImageRejected,
}

public sealed record CarWriteResult(CarWriteStatus Status, string? Detail, Car? Car)
{
    public bool Saved => Status == CarWriteStatus.Saved;
}

public sealed record CarInput(
    string Name,
    string Make,
    string Model,
    string ModelNumber,
    string EngineType,
    string Vrm,
    int Year,
    string Vin,
    string VehicleOrderJson = "");

public enum CarDeleteResult
{
    NotFound,
    Deleted,
}

public static class CarCommands
{
    public const int MinModelYear = 1900;
    public const int MaxNameLength = 200;
    public const int MaxMakeLength = 120;
    public const int MaxModelLength = 120;
    public const int MaxModelNumberLength = 32;
    public const int MaxEngineTypeLength = 200;
    public const int MaxVrmLength = 16;
    public const int MaxVinLength = 17;

    public static int MaxModelYear(DateTimeOffset now) => now.Year + 1;

    public static string NormalizeVrm(string vrm)
    {
        var chars = vrm.Where(c => !char.IsWhiteSpace(c)).ToArray();
        return new string(chars).ToUpperInvariant();
    }

    public static async Task<CarWriteResult> CreateAsync(
        WorkCostsDbContext db,
        string dataRoot,
        CarInput input,
        Stream? image,
        string? imageContentType,
        CancellationToken cancellationToken = default,
        DateTimeOffset? utcNow = null)
    {
        var now = utcNow ?? DateTimeOffset.Now;
        var invalid = Validate(input, now, requireImage: true, hasImage: image is not null);
        if (invalid is not null)
        {
            return invalid;
        }

        var vrmKey = NormalizeVrm(input.Vrm);
        if (await ActiveVrmExistsAsync(db, vrmKey, exceptId: null, cancellationToken))
        {
            return Fail(CarWriteStatus.DuplicateVrm, "A car with this registration already exists.");
        }

        var id = Guid.NewGuid();
        try
        {
            await CarImageStore.WriteAsync(dataRoot, id, image!, imageContentType!, cancellationToken);
        }
        catch (ArgumentException)
        {
            return Fail(CarWriteStatus.ImageRejected, "Choose a PNG, JPEG, or WebP photo up to 512 KB.");
        }

        var entity = new Car
        {
            Id = id,
            Name = input.Name.Trim(),
            Make = input.Make.Trim(),
            Model = input.Model.Trim(),
            ModelNumber = input.ModelNumber.Trim(),
            EngineType = input.EngineType.Trim(),
            Vrm = input.Vrm.Trim(),
            VrmKey = vrmKey,
            Year = input.Year,
            Vin = input.Vin.Trim(),
            ImageRelativePath = CarImageStore.RelativePathFor(id, imageContentType!),
            ImageContentType = NormalizeContentType(imageContentType!),
            VehicleOrderJson = string.Empty,
            UpdatedAt = now,
            DeletedAt = null,
        };
        db.Cars.Add(entity);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            CarImageStore.DeleteIfExists(dataRoot, entity.ImageRelativePath);
            db.Entry(entity).State = EntityState.Detached;
            return Fail(CarWriteStatus.DuplicateVrm, "A car with this registration already exists.");
        }

        return new CarWriteResult(CarWriteStatus.Saved, null, entity);
    }

    public static Task<Car?> GetAsync(
        WorkCostsDbContext db,
        Guid carId,
        CancellationToken cancellationToken = default) =>
        db.Cars.AsNoTracking().FirstOrDefaultAsync(c => c.Id == carId, cancellationToken);

    public static Task<List<Car>> ListActiveAsync(
        WorkCostsDbContext db,
        CancellationToken cancellationToken = default) =>
        db.Cars.AsNoTracking()
            .Where(c => c.DeletedAt == null)
            .OrderBy(c => c.Name)
            .ThenBy(c => c.Id)
            .ToListAsync(cancellationToken);

    public static async Task<CarWriteResult> UpdateAsync(
        WorkCostsDbContext db,
        string dataRoot,
        Guid carId,
        CarInput input,
        Stream? replacementImage,
        string? replacementContentType,
        CancellationToken cancellationToken = default,
        DateTimeOffset? utcNow = null)
    {
        var now = utcNow ?? DateTimeOffset.Now;
        var invalid = Validate(input, now, requireImage: false, hasImage: true);
        if (invalid is not null)
        {
            return invalid;
        }

        var entity = await db.Cars.FirstOrDefaultAsync(c => c.Id == carId, cancellationToken);
        if (entity is null || entity.DeletedAt is not null)
        {
            return Fail(CarWriteStatus.NotFound, "This car no longer exists.");
        }

        var vrmKey = NormalizeVrm(input.Vrm);
        if (await ActiveVrmExistsAsync(db, vrmKey, carId, cancellationToken))
        {
            return Fail(CarWriteStatus.DuplicateVrm, "A car with this registration already exists.");
        }

        string? newRelative = null;
        string? newContentType = null;
        byte[]? previousImage = null;
        if (replacementImage is not null)
        {
            if (string.IsNullOrWhiteSpace(replacementContentType))
            {
                return Fail(CarWriteStatus.ImageRejected, "Choose a PNG, JPEG, or WebP photo up to 512 KB.");
            }

            if (!string.IsNullOrWhiteSpace(entity.ImageRelativePath))
            {
                var previousFull = CarImageStore.GetFullPath(dataRoot, entity.ImageRelativePath);
                if (File.Exists(previousFull))
                {
                    previousImage = await File.ReadAllBytesAsync(previousFull, cancellationToken);
                }
            }

            try
            {
                await CarImageStore.WriteAsync(dataRoot, carId, replacementImage, replacementContentType, cancellationToken);
            }
            catch (ArgumentException)
            {
                return Fail(CarWriteStatus.ImageRejected, "Choose a PNG, JPEG, or WebP photo up to 512 KB.");
            }

            newContentType = NormalizeContentType(replacementContentType);
            newRelative = CarImageStore.RelativePathFor(carId, newContentType);
        }

        var previousPath = entity.ImageRelativePath;
        entity.Name = input.Name.Trim();
        entity.Make = input.Make.Trim();
        entity.Model = input.Model.Trim();
        entity.ModelNumber = input.ModelNumber.Trim();
        entity.EngineType = input.EngineType.Trim();
        entity.Vrm = input.Vrm.Trim();
        entity.VrmKey = vrmKey;
        entity.Year = input.Year;
        entity.Vin = input.Vin.Trim();
        entity.VehicleOrderJson = input.VehicleOrderJson ?? string.Empty;
        entity.UpdatedAt = now;
        if (newRelative is not null && newContentType is not null)
        {
            entity.ImageRelativePath = newRelative;
            entity.ImageContentType = newContentType;
        }

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            if (newRelative is not null
                && !string.Equals(previousPath, newRelative, StringComparison.OrdinalIgnoreCase))
            {
                CarImageStore.DeleteIfExists(dataRoot, newRelative);
            }

            if (previousImage is not null)
            {
                var previousFull = CarImageStore.GetFullPath(dataRoot, previousPath);
                await File.WriteAllBytesAsync(previousFull, previousImage, cancellationToken);
            }

            await db.Entry(entity).ReloadAsync(cancellationToken);
            return Fail(CarWriteStatus.DuplicateVrm, "A car with this registration already exists.");
        }

        if (newRelative is not null
            && !string.Equals(previousPath, newRelative, StringComparison.OrdinalIgnoreCase))
        {
            CarImageStore.DeleteIfExists(dataRoot, previousPath);
        }

        return new CarWriteResult(CarWriteStatus.Saved, null, entity);
    }

    public static async Task<CarDeleteResult> TryDeleteAsync(
        WorkCostsDbContext db,
        Guid carId,
        CancellationToken cancellationToken = default,
        DateTimeOffset? utcNow = null)
    {
        var entity = await db.Cars.FirstOrDefaultAsync(c => c.Id == carId, cancellationToken);
        if (entity is null)
        {
            return CarDeleteResult.NotFound;
        }

        var now = utcNow ?? DateTimeOffset.Now;
        entity.DeletedAt = now;
        entity.UpdatedAt = now;
        await db.SaveChangesAsync(cancellationToken);
        return CarDeleteResult.Deleted;
    }

    private static async Task<bool> ActiveVrmExistsAsync(
        WorkCostsDbContext db,
        string vrmKey,
        Guid? exceptId,
        CancellationToken cancellationToken) =>
        await db.Cars.AnyAsync(
            c => c.DeletedAt == null && c.VrmKey == vrmKey && (exceptId == null || c.Id != exceptId),
            cancellationToken);

    private static CarWriteResult? Validate(CarInput input, DateTimeOffset now, bool requireImage, bool hasImage)
    {
        var missing = FirstMissing(input);
        if (missing is not null)
        {
            return Fail(CarWriteStatus.MissingField, missing);
        }

        if (input.Year < MinModelYear || input.Year > MaxModelYear(now))
        {
            return Fail(
                CarWriteStatus.YearOutOfRange,
                $"Model year must be from {MinModelYear} to {MaxModelYear(now)}.");
        }

        if (requireImage && !hasImage)
        {
            return Fail(CarWriteStatus.MissingImage, "Choose a photo before saving.");
        }

        return null;
    }

    private static string? FirstMissing(CarInput input)
    {
        if (IsBlank(input.Name, MaxNameLength))
        {
            return FieldMessage("Nickname", input.Name, MaxNameLength);
        }

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

        if (IsBlank(input.Vrm, MaxVrmLength))
        {
            return FieldMessage("Registration", input.Vrm, MaxVrmLength);
        }

        if (IsBlank(input.Vin, MaxVinLength))
        {
            return FieldMessage("VIN", input.Vin, MaxVinLength);
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

    private static string NormalizeContentType(string contentType)
    {
        var normalized = contentType.Split(';', 2)[0].Trim().ToLowerInvariant();
        return normalized == "image/jpg" ? "image/jpeg" : normalized;
    }

    private static bool IsUniqueViolation(DbUpdateException ex)
    {
        var message = ex.InnerException?.Message ?? ex.Message;
        return message.Contains("UNIQUE", StringComparison.OrdinalIgnoreCase);
    }

    private static CarWriteResult Fail(CarWriteStatus status, string detail) =>
        new(status, detail, null);
}
