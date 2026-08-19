# Tests

`WorkCosts.Tests` is `net9.0` xUnit. Run `dotnet test WorkCosts.slnx --settings .runsettings`.

| Area | What it protects |
| :--- | :--- |
| `AmazonPageParserTests` / `AutodocPageParserTests` | Host detection and field extraction from snippets |
| `ProductPageFieldParserTests` | Shared field helpers |
| `ProductPageClientContractTests` | Parser fields ↔ `ProductPageClientValues` (mock parser, no network) |
| `DatabaseInitializationTests` | Migrate + seed on a temp SQLite file |

HTML under `Fixtures/*.snippet.html` is copied to output. Swift ports must pass the same assertions (same files).

Do not hit the live network in CI. Do not require WinUI. Parser tests must keep working on Mac agents with only the .NET SDK.
