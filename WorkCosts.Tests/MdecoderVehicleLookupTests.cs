using WorkCosts.Data;
using WorkCosts.Services;
using Xunit;

namespace WorkCosts.Tests;

public sealed class MdecoderVehicleLookupTests
{
    private const string SampleVin = "WBAWB33508P050351";
    private const string ExistingJson = "{\"keep\":\"me\"}";

    [Fact]
    public async Task MdecoderLookup_EmptyVin_DoesNotRequest()
    {
        var fetches = 0;
        var result = await MdecoderVehicleLookup.RunAsync(
            make: "BMW",
            vin: "  ",
            ExistingJson,
            existingNickname: "the daily",
            fetch: (_, _) =>
            {
                fetches++;
                return Task.FromResult(string.Empty);
            },
            delay: NoDelay,
            now: () => DateTimeOffset.Parse("2026-01-01T00:00:00Z"),
            existingModel: "545",
            existingModelNumber: "E60",
            existingEngineType: "4.4 V8",
            existingYear: 2004);

        Assert.Equal(0, fetches);
        Assert.Equal(MdecoderLookupStatus.SkippedEmptyVin, result.Status);
        Assert.Equal(ExistingJson, result.VehicleOrderJson);
        Assert.Equal("the daily", result.Nickname);
    }

    [Fact]
    public async Task MdecoderLookup_NonBmw_DoesNotRequest()
    {
        var fetches = 0;
        var result = await MdecoderVehicleLookup.RunAsync(
            make: "Ford",
            vin: "1HGCM82633A004352",
            ExistingJson,
            existingNickname: "the daily",
            fetch: (_, _) =>
            {
                fetches++;
                return Task.FromResult(string.Empty);
            },
            delay: NoDelay,
            now: () => DateTimeOffset.Parse("2026-01-01T00:00:00Z"));

        Assert.Equal(0, fetches);
        Assert.Equal(MdecoderLookupStatus.SkippedNonBmw, result.Status);
        Assert.Equal(ExistingJson, result.VehicleOrderJson);
        Assert.Equal(MdecoderVehicleLookup.NonBmwStatus, result.StatusMessage);
    }

    [Fact]
    public async Task MdecoderLookup_NotReady_RetriesAfter30s()
    {
        var clock = new FakeClock();
        var pages = new Queue<string>([await Fixture("mdecoder-wait.snippet.html"), await Fixture("mdecoder-ready.snippet.html")]);
        var delays = new List<TimeSpan>();
        var uris = new List<Uri>();

        var result = await MdecoderVehicleLookup.RunAsync(
            make: "BMW",
            vin: SampleVin,
            ExistingJson,
            existingNickname: "the daily",
            fetch: (uri, _) =>
            {
                uris.Add(uri);
                return Task.FromResult(pages.Dequeue());
            },
            delay: (span, _) =>
            {
                delays.Add(span);
                clock.Advance(span);
                return Task.CompletedTask;
            },
            now: () => clock.Now);

        Assert.Equal(2, uris.Count);
        Assert.Equal(new Uri("https://www.mdecoder.com/decode/WBAWB33508P050351"), uris[0]);
        Assert.Equal([MdecoderVehicleLookup.RetryInterval], delays);
        Assert.Equal(MdecoderLookupStatus.Ready, result.Status);
        Assert.NotEqual(ExistingJson, result.VehicleOrderJson);
    }

    [Fact]
    public async Task MdecoderLookup_TimeoutAt2Minutes_LeavesJsonUnchanged()
    {
        var clock = new FakeClock();
        var wait = await Fixture("mdecoder-wait.snippet.html");
        var fetches = 0;

        var result = await MdecoderVehicleLookup.RunAsync(
            make: "bmw",
            vin: SampleVin,
            ExistingJson,
            existingNickname: "the daily",
            fetch: (_, _) =>
            {
                fetches++;
                return Task.FromResult(wait);
            },
            delay: (span, _) =>
            {
                clock.Advance(span);
                return Task.CompletedTask;
            },
            now: () => clock.Now);

        Assert.Equal(MdecoderLookupStatus.TimedOut, result.Status);
        Assert.Equal(ExistingJson, result.VehicleOrderJson);
        Assert.Equal(MdecoderVehicleLookup.TimeoutStatus, result.StatusMessage);
        Assert.True(fetches >= 4);
        Assert.Equal("the daily", result.Nickname);
    }

    [Fact]
    public async Task MdecoderLookup_Ready_ReplacesVehicleOrderJson()
    {
        var ready = await Fixture("mdecoder-ready.snippet.html");
        var fetches = 0;
        var delays = 0;

        var result = await MdecoderVehicleLookup.RunAsync(
            make: string.Empty,
            vin: SampleVin,
            ExistingJson,
            existingNickname: "the daily",
            fetch: (_, _) =>
            {
                fetches++;
                return Task.FromResult(ready);
            },
            delay: (_, _) =>
            {
                delays++;
                return Task.CompletedTask;
            },
            now: () => DateTimeOffset.Parse("2026-01-01T00:00:00Z"));

        Assert.Equal(1, fetches);
        Assert.Equal(0, delays);
        Assert.Equal(MdecoderLookupStatus.Ready, result.Status);
        Assert.NotEqual(ExistingJson, result.VehicleOrderJson);
        Assert.Contains("\"source\":\"mdecoder.com\"", result.VehicleOrderJson, StringComparison.Ordinal);
        Assert.Contains(SampleVin, result.VehicleOrderJson, StringComparison.Ordinal);
        Assert.Contains("328i Convertible", result.VehicleOrderJson, StringComparison.Ordinal);
        Assert.Contains("S403A", result.VehicleOrderJson, StringComparison.Ordinal);
        Assert.Equal("BMW", result.Scalars!.Make);
        Assert.Equal("328i Convertible", result.Scalars.Model);
        Assert.Equal("E36", result.Scalars.ModelNumber);
        Assert.Equal("M52", result.Scalars.EngineType);
        Assert.Equal(1997, result.Scalars.Year);
    }

    [Fact]
    public async Task MdecoderLookup_DoesNotOverwriteNickname()
    {
        var ready = await Fixture("mdecoder-ready.snippet.html");
        var current = new CarInput(
            "the daily",
            "BMW",
            "545",
            "E60",
            "4.4 V8",
            "AB12 CDE",
            2004,
            SampleVin,
            ExistingJson);

        var result = await MdecoderVehicleLookup.RunAsync(
            current.Make,
            current.Vin,
            current.VehicleOrderJson,
            current.Name,
            fetch: (_, _) => Task.FromResult(ready),
            delay: NoDelay,
            now: () => DateTimeOffset.Parse("2026-01-01T00:00:00Z"),
            existingModel: current.Model,
            existingModelNumber: current.ModelNumber,
            existingEngineType: current.EngineType,
            existingYear: current.Year);

        var applied = MdecoderVehicleLookup.Apply(current, result.VehicleOrderJson, result.Scalars, overwriteFilled: true);
        Assert.Equal("the daily", result.Nickname);
        Assert.Equal("the daily", applied.Name);
        Assert.Equal(result.VehicleOrderJson, applied.VehicleOrderJson);
        Assert.NotEqual(ExistingJson, applied.VehicleOrderJson);
        Assert.Contains("model", result.OverwriteFields);
        Assert.Contains("year", result.OverwriteFields);

        var kept = MdecoderVehicleLookup.Apply(current, result.VehicleOrderJson, result.Scalars, overwriteFilled: false);
        Assert.Equal("the daily", kept.Name);
        Assert.Equal("545", kept.Model);
        Assert.Equal("E60", kept.ModelNumber);
        Assert.Equal(2004, kept.Year);
        Assert.Equal(result.VehicleOrderJson, kept.VehicleOrderJson);
    }

    [Fact]
    public async Task MdecoderLookup_Cancel_StopsPolling()
    {
        using var cts = new CancellationTokenSource();
        var wait = await Fixture("mdecoder-wait.snippet.html");
        var fetches = 0;

        var result = await MdecoderVehicleLookup.RunAsync(
            make: "BMW",
            vin: SampleVin,
            ExistingJson,
            existingNickname: "the daily",
            fetch: (_, _) =>
            {
                fetches++;
                return Task.FromResult(wait);
            },
            delay: (_, token) =>
            {
                cts.Cancel();
                token.ThrowIfCancellationRequested();
                return Task.CompletedTask;
            },
            now: () => DateTimeOffset.Parse("2026-01-01T00:00:00Z"),
            cancellationToken: cts.Token);

        Assert.Equal(1, fetches);
        Assert.Equal(MdecoderLookupStatus.Cancelled, result.Status);
        Assert.Equal(ExistingJson, result.VehicleOrderJson);
        Assert.Equal(MdecoderVehicleLookup.CancelledStatus, result.StatusMessage);
    }

    private static Task NoDelay(TimeSpan _, CancellationToken __) => Task.CompletedTask;

    private static Task<string> Fixture(string name) =>
        File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "Fixtures", name));

    private sealed class FakeClock
    {
        public DateTimeOffset Now { get; private set; } = DateTimeOffset.Parse("2026-01-01T00:00:00Z");

        public void Advance(TimeSpan span) => Now += span;
    }
}
