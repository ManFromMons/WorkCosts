using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using WorkCosts.Models;

namespace WorkCosts.Data;

public sealed record CarDetailsJsonRow(
    Guid Id,
    string Make,
    string Model,
    string ModelNumber,
    int Year,
    int? EndYear);

public static class CarDetailsJsonSeed
{
    public const string EmbeddedName = "WorkCosts.Data.car-details.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    public static IReadOnlyList<CarDetailsJsonRow> Read(Stream stream)
    {
        try
        {
            var rows = JsonSerializer.Deserialize<List<CarDetailsJsonRow>>(stream, JsonOptions);
            return rows ?? [];
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException("car-details.json is not valid JSON.", ex);
        }
    }

    public static Stream? OpenEmbedded() =>
        typeof(CarDetailsJsonSeed).Assembly.GetManifestResourceStream(EmbeddedName);

    public static async Task SeedCarDetailsAsync(
        WorkCostsDbContext db,
        Stream? source = null,
        CancellationToken cancellationToken = default)
    {
        var owned = false;
        var stream = source;
        if (stream is null)
        {
            stream = OpenEmbedded();
            if (stream is null)
            {
                return;
            }

            owned = true;
        }

        try
        {
            IReadOnlyList<CarDetailsJsonRow> rows;
            try
            {
                rows = Read(stream);
            }
            catch (InvalidOperationException)
            {
                return;
            }

            await UpsertAsync(db, rows, cancellationToken);
        }
        finally
        {
            if (owned)
            {
                await stream.DisposeAsync();
            }
        }
    }

    public static async Task UpsertAsync(
        WorkCostsDbContext db,
        IReadOnlyList<CarDetailsJsonRow> rows,
        CancellationToken cancellationToken = default)
    {
        var changed = false;
        foreach (var row in rows)
        {
            var typeKey = CarDetailsCommands.NormalizeTypeKey(row.Make, row.Model, row.ModelNumber, row.Year);
            var existing = await db.CarDetails.FirstOrDefaultAsync(t => t.Id == row.Id, cancellationToken);
            if (existing is not null)
            {
                existing.Make = row.Make.Trim();
                existing.Model = row.Model.Trim();
                existing.ModelNumber = row.ModelNumber.Trim();
                existing.Year = row.Year;
                existing.EndYear = row.EndYear;
                existing.TypeKey = typeKey;
                changed = true;
                continue;
            }

            if (await db.CarDetails.AnyAsync(t => t.TypeKey == typeKey, cancellationToken))
            {
                continue;
            }

            db.CarDetails.Add(new CarDetails
            {
                Id = row.Id,
                Make = row.Make.Trim(),
                Model = row.Model.Trim(),
                ModelNumber = row.ModelNumber.Trim(),
                Year = row.Year,
                EndYear = row.EndYear,
                TypeKey = typeKey,
            });
            changed = true;
        }

        if (changed)
        {
            await db.SaveChangesAsync(cancellationToken);
        }
    }
}
