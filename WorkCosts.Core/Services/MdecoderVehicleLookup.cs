using System.Globalization;
using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using WorkCosts.Data;

namespace WorkCosts.Services;

public enum MdecoderLookupStatus
{
    SkippedEmptyVin,
    SkippedNonBmw,
    Ready,
    TimedOut,
    Cancelled,
    Failed,
}

public sealed record MdecoderScalars(
    string? Make,
    string? Model,
    string? ModelNumber,
    string? EngineType,
    int? Year);

public sealed record MdecoderLookupResult(
    MdecoderLookupStatus Status,
    string VehicleOrderJson,
    string Nickname,
    MdecoderScalars? Scalars,
    string StatusMessage,
    int AttemptCount,
    IReadOnlyList<string> OverwriteFields);

public static class MdecoderVehicleLookup
{
    /// <summary>
    /// Public decode links use GET https://www.mdecoder.com/decode/{vin}
    /// (e.g. bimmerpost threads). Planning HttpClient hit Cloudflare; try HTTP first, then Chromium.
    /// </summary>
    public const string DecodeUrlFormat = "https://www.mdecoder.com/decode/{0}";

    public static readonly TimeSpan RetryInterval = TimeSpan.FromSeconds(30);
    public static readonly TimeSpan Timeout = TimeSpan.FromMinutes(2);

    public const string RequestingStatus = "Requesting mdecoder…";
    public const string WaitingStatus = "Waiting, retrying in 30s…";
    public const string NonBmwStatus = "Use FastCarCheck later; this lookup is for BMW VINs.";
    public const string EmptyVinStatus = "Enter a VIN to look up.";
    public const string TimeoutStatus = "Not ready after 2 minutes.";
    public const string CancelledStatus = "Lookup cancelled.";
    public const string FailedStatus = "mdecoder did not return a usable decode.";

    public static readonly string[] BmwVinPrefixes = ["WBA", "WBS", "WBY", "5UX", "5YM"];

    private static readonly HttpClient Http = CreateClient();

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false,
    };

    public static bool HasVin(string? vin) => !string.IsNullOrWhiteSpace(vin);

    public static bool LooksLikeBmw(string? make, string? vin)
    {
        if (string.IsNullOrWhiteSpace(vin))
        {
            return false;
        }

        if (string.Equals(make?.Trim(), "BMW", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var trimmed = vin.Trim();
        return BmwVinPrefixes.Any(prefix => trimmed.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
    }

    public static bool CanRequest(string? make, string? vin) => LooksLikeBmw(make, vin);

    public static Uri DecodeUri(string vin)
    {
        var clean = vin.Trim().ToUpperInvariant();
        return new Uri(string.Format(CultureInfo.InvariantCulture, DecodeUrlFormat, Uri.EscapeDataString(clean)));
    }

    public static bool NeedsChromium(string? html) =>
        string.IsNullOrWhiteSpace(html) || MdecoderPageParser.LooksLikeChallenge(html);

    public static async Task<string> FetchHttpAsync(Uri uri, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, uri);
        request.Headers.TryAddWithoutValidation("User-Agent", ProductImageService.UserAgent);
        request.Headers.TryAddWithoutValidation("Accept", "text/html,application/xhtml+xml;q=0.9,*/*;q=0.8");
        request.Headers.TryAddWithoutValidation("Accept-Language", "en-GB,en-US;q=0.9,en;q=0.8");
        using var response = await Http.SendAsync(request, cancellationToken);
        return await response.Content.ReadAsStringAsync(cancellationToken);
    }

    public static async Task<MdecoderLookupResult> RunAsync(
        string? make,
        string vin,
        string existingVehicleOrderJson,
        string existingNickname,
        Func<Uri, CancellationToken, Task<string>> fetch,
        Func<TimeSpan, CancellationToken, Task> delay,
        Func<DateTimeOffset> now,
        Action<string>? status = null,
        string? existingModel = null,
        string? existingModelNumber = null,
        string? existingEngineType = null,
        int? existingYear = null,
        CancellationToken cancellationToken = default)
    {
        var json = existingVehicleOrderJson ?? string.Empty;
        var nickname = existingNickname ?? string.Empty;

        if (!HasVin(vin))
        {
            return new MdecoderLookupResult(
                MdecoderLookupStatus.SkippedEmptyVin,
                json,
                nickname,
                Scalars: null,
                EmptyVinStatus,
                AttemptCount: 0,
                []);
        }

        if (!LooksLikeBmw(make, vin))
        {
            return new MdecoderLookupResult(
                MdecoderLookupStatus.SkippedNonBmw,
                json,
                nickname,
                Scalars: null,
                NonBmwStatus,
                AttemptCount: 0,
                []);
        }

        var uri = DecodeUri(vin);
        var started = now();
        var attempts = 0;

        try
        {
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                status?.Invoke(RequestingStatus);
                attempts++;
                var html = await fetch(uri, cancellationToken);
                var kind = MdecoderPageParser.Classify(html);
                if (kind == MdecoderPageKind.Ready)
                {
                    var decode = await MdecoderPageParser.ParseReadyAsync(html, cancellationToken);
                    if (decode is null)
                    {
                        return Fail(json, nickname, attempts);
                    }

                    var nextJson = ToVehicleOrderJson(decode);
                    var scalars = ToScalars(decode);
                    return new MdecoderLookupResult(
                        MdecoderLookupStatus.Ready,
                        nextJson,
                        nickname,
                        scalars,
                        "mdecoder decode applied.",
                        attempts,
                        OverwriteFieldNames(
                            make,
                            existingModel,
                            existingModelNumber,
                            existingEngineType,
                            existingYear,
                            scalars));
                }

                if (kind != MdecoderPageKind.Wait)
                {
                    return Fail(json, nickname, attempts);
                }

                if (now() - started >= Timeout)
                {
                    return new MdecoderLookupResult(
                        MdecoderLookupStatus.TimedOut,
                        json,
                        nickname,
                        Scalars: null,
                        TimeoutStatus,
                        attempts,
                        []);
                }

                status?.Invoke(WaitingStatus);
                await delay(RetryInterval, cancellationToken);
                if (now() - started >= Timeout)
                {
                    return new MdecoderLookupResult(
                        MdecoderLookupStatus.TimedOut,
                        json,
                        nickname,
                        Scalars: null,
                        TimeoutStatus,
                        attempts,
                        []);
                }
            }
        }
        catch (OperationCanceledException)
        {
            return new MdecoderLookupResult(
                MdecoderLookupStatus.Cancelled,
                json,
                nickname,
                Scalars: null,
                CancelledStatus,
                attempts,
                []);
        }
    }

    public static string ToVehicleOrderJson(MdecoderDecode decode) =>
        JsonSerializer.Serialize(
            new MdecoderOrderDocument(
                Source: "mdecoder.com",
                decode.Vin,
                decode.ProductionDate,
                decode.Type,
                decode.Model,
                decode.Steering,
                decode.Engine,
                decode.Transmission,
                decode.Color,
                decode.Upholstery,
                decode.Options),
            JsonOptions);

    public static MdecoderScalars ToScalars(MdecoderDecode decode) =>
        new(
            Make: "BMW",
            Model: decode.Model,
            ModelNumber: decode.Type,
            EngineType: decode.Engine,
            Year: ParseProductionYear(decode.ProductionDate));

    public static CarInput Apply(
        CarInput current,
        string vehicleOrderJson,
        MdecoderScalars? scalars,
        bool overwriteFilled)
    {
        var next = current with
        {
            Name = current.Name,
            VehicleOrderJson = vehicleOrderJson,
        };
        if (scalars is null)
        {
            return next;
        }

        return next with
        {
            Make = Pick(current.Make, scalars.Make, overwriteFilled),
            Model = Pick(current.Model, scalars.Model, overwriteFilled),
            ModelNumber = Pick(current.ModelNumber, scalars.ModelNumber, overwriteFilled),
            EngineType = Pick(current.EngineType, scalars.EngineType, overwriteFilled),
            Year = PickYear(current.Year, scalars.Year, overwriteFilled),
        };
    }

    public static IReadOnlyList<string> OverwriteFieldNames(
        string? make,
        string? model,
        string? modelNumber,
        string? engineType,
        int? year,
        MdecoderScalars scalars)
    {
        var fields = new List<string>();
        if (FilledDiffers(make, scalars.Make))
        {
            fields.Add("make");
        }

        if (FilledDiffers(model, scalars.Model))
        {
            fields.Add("model");
        }

        if (FilledDiffers(modelNumber, scalars.ModelNumber))
        {
            fields.Add("model number");
        }

        if (FilledDiffers(engineType, scalars.EngineType))
        {
            fields.Add("engine");
        }

        if (year is > 0 && scalars.Year is int incoming && incoming != year)
        {
            fields.Add("year");
        }

        return fields;
    }

    public static int? ParseProductionYear(string? productionDate)
    {
        if (string.IsNullOrWhiteSpace(productionDate))
        {
            return null;
        }

        if (DateTime.TryParse(productionDate, CultureInfo.GetCultureInfo("en-GB"), DateTimeStyles.AllowWhiteSpaces, out var uk)
            && uk.Year is >= 1970 and <= 2100)
        {
            return uk.Year;
        }

        if (DateTime.TryParse(productionDate, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out var invariant)
            && invariant.Year is >= 1970 and <= 2100)
        {
            return invariant.Year;
        }

        var slash = productionDate.Split('/', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (slash.Length >= 3 && int.TryParse(slash[^1], NumberStyles.None, CultureInfo.InvariantCulture, out var yearPart))
        {
            if (yearPart < 100)
            {
                yearPart += yearPart >= 70 ? 1900 : 2000;
            }

            return yearPart is >= 1970 and <= 2100 ? yearPart : null;
        }

        return null;
    }

    private static MdecoderLookupResult Fail(string json, string nickname, int attempts) =>
        new(
            MdecoderLookupStatus.Failed,
            json,
            nickname,
            Scalars: null,
            FailedStatus,
            attempts,
            []);

    private static string Pick(string current, string? incoming, bool overwriteFilled)
    {
        if (string.IsNullOrWhiteSpace(incoming))
        {
            return current;
        }

        if (string.IsNullOrWhiteSpace(current) || overwriteFilled)
        {
            return incoming;
        }

        return current;
    }

    private static int PickYear(int current, int? incoming, bool overwriteFilled)
    {
        if (incoming is not int year)
        {
            return current;
        }

        if (current <= 0 || overwriteFilled)
        {
            return year;
        }

        return current;
    }

    private static bool FilledDiffers(string? current, string? incoming) =>
        !string.IsNullOrWhiteSpace(current)
        && !string.IsNullOrWhiteSpace(incoming)
        && !string.Equals(current.Trim(), incoming.Trim(), StringComparison.OrdinalIgnoreCase);

    private static HttpClient CreateClient()
    {
        var handler = new SocketsHttpHandler
        {
            AutomaticDecompression = DecompressionMethods.All,
            AllowAutoRedirect = true,
            UseCookies = true,
            CookieContainer = new CookieContainer(),
        };
        var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(45) };
        return client;
    }

    private sealed record MdecoderOrderDocument(
        string Source,
        string Vin,
        string? ProductionDate,
        string? Type,
        string? Model,
        string? Steering,
        string? Engine,
        string? Transmission,
        string? Color,
        string? Upholstery,
        IReadOnlyList<MdecoderOption> Options);
}
