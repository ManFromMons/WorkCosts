# Feature: Car image sources

- **Id:** `docs/features/18-car-image-sources.md`
- **Seq:** 18
- **Depends-on:** `11-cars`
- **Status:** ready-for-agent
- **PR:** none
- **Windows:** Core + WinUI (same Add Car sheet and car editor; no new page)
- **Related screens:** `docs/screens/cars.md`
- **Related code:** `CarImageSearch`, `CarImageChooser`, `CarImageStore`, `ChromiumPageLoader`, `ProductImagePicker.ChooseFromCandidatesAsync`

Replaces the image-search order in [11-cars.md](11-cars.md). Fields, sheet, chooser, and soft-delete stay as Seq 11 left them.

## Objectives

- Car photo search tries sources in this order: **Wikimedia Commons**, then the **manufacturer press site** when the make is BMW, Jaguar, or Mercedes-Benz, then the existing **Bing Images** then **Google Images** path.
- A Commons or press hit counts only when the **model number** appears on that hit (filename, URL, or adjacent text). Unrelated photos from a press front page are not candidates.
- Query stays **`{Make} {ModelNumber}`** (example `BMW E60`). Year is not in the query.
- **Out of scope:** New fields, VIN lookup, FastCarCheck, a paid studio API (imagin.studio), media-site login, GNOME/iPad UI, changing the chooser into a new control.

## User requirements

- Search images on Add Car and on the car editor uses the new order. Status text names the source: “Searching Commons…”, “Searching BMW press…”, “Searching Jaguar press…”, “Searching Mercedes-Benz press…”, “Searching Bing…”, “Searching Google…”.
- One usable photo still applies with no grid. Several still open `ProductImagePicker.ChooseFromCandidatesAsync`. Local PNG/JPEG/WebP pick stays available.
- Makes other than BMW, Jaguar, and Mercedes-Benz skip the press step (Commons, then Bing, then Google).
- Make match is case-insensitive: `BMW`; `Jaguar`; `Mercedes`, `Mercedes-Benz`, or a make that starts with `Mercedes`.
- Press search URLs (query is `Uri.EscapeDataString` of `{Make} {ModelNumber}`):
  - BMW: `https://www.press.bmwgroup.com/global/photo/search?q={query}`
  - Jaguar: `https://media.jaguar.com/en/search?q={query}`
  - Mercedes-Benz: `https://media.mercedes-benz.com/en/search?query={query}`
- When a press page offers both a low-resolution and a high-resolution file for the same photo, keep the low-resolution file.
- Car photos, from search or from a local file, may be up to **4 MB**. PNG, JPEG, and WebP only. The Seq 11 limit of 512 KB is replaced for car photos.
- Empty / error / cancel: no hit on a source falls through to the next source. All sources empty, or every download rejected: the sheet says to try again or choose a file. Save stays disabled until a photo is chosen. Esc and Enter stay as Seq 11. Chromium, when used, stays outside any ContentDialog.

## Layout

- Same Cars page, Add sheet, and editor. No new regions.
- Regular list beside detail; compact stack. Primary Add stays trailing.
- Sheets: Add Car and the existing image chooser. No WebView2 inside a blocking dialog.

## Workflow

1. User chooses Search images with make and model number filled.
2. Load the Commons API for `{Make} {ModelNumber}`. HttpClient. Keep JPEG/PNG/WebP whose title or URL contains the model number, up to 12, each at most 4 MB.
3. If that list is empty and the make is BMW, Jaguar, or Mercedes-Benz, load that make’s press search URL. HttpClient first. If the response is a challenge or contains no model-number image, load the same URL in Chromium outside a dialog. Keep only images that mention the model number. Prefer low-resolution when both sizes exist.
4. If that list is still empty, load Bing Images, then Google Images, using the Seq 11 helpers. Same challenge rule as today.
5. One image applies. Several open the chooser. Cancel leaves the previous photo in place.
6. Save writes `{dataRoot}/images/cars/{carId}.{ext}` as today.

## Technical design

| Need | Reuse | Create |
| :--- | :--- | :--- |
| Order and URL builders | `CarImageSearch.FindImageUrlsAsync`, `BingImagesUrl`, `GoogleImagesUrl`, `BuildQuery` | Commons URL, press URL by make, Commons JSON extract, press HTML extract |
| Fetch | `CarImageChooser` HttpClient then `ChromiumPageLoader` | Status text per source. Commons is HttpClient only |
| Download limit | `CarImageSearch.DownloadCandidatesAsync`, `CarImageStore` | Raise `CarImageStore.MaxImageBytes` to 4 MB |
| Chooser | `ProductImagePicker.ChooseFromCandidatesAsync` | none |
| Wiring | Static helpers. `App.Database` unchanged. No DI container | none |

- **Commons request:** `https://commons.wikimedia.org/w/api.php?action=query&format=json&generator=search&gsrnamespace=6&gsrlimit=12&prop=imageinfo&iiprop=url|mime|size&gsrsearch={query}`
  User-Agent: `WillIDIY/1.0 (personal offline catalogue; https://github.com/ManFromMons/WorkCosts)`. Read `query.pages.*.title` and `imageinfo[0]` (`url`, `mime`, `size`). Mime must be `image/jpeg`, `image/png`, or `image/webp`.
- **Data:** Same car image file. No schema change. No new BLOB.
- **Ports:** EF and the search helpers stay in Core so Swift can call the same URL order later. This story does not build GNOME or iPad UI.

## Tests

`WorkCosts.Tests`. Fixtures only. No live network.

- `CarImageSearch_UsesCommonsBeforePressAndBing` — Commons JSON with an E60 JPEG is the only request.
- `CarImageSearch_PressOnlyForBmwJaguarMercedes` — BMW, Jaguar, and Mercedes-Benz get their press URL; `Ford` does not.
- `CarImageSearch_PressKeepsImagesThatMentionModelNumber` — a press fixture with an unrelated JPEG and one `e60.jpg` returns only the E60 file; low-res wins over high-res for the same stem.
- `CarImageSearch_UsesBingThenGoogle_WhenEarlierSourcesEmpty` — empty Commons, empty press, then the existing Bing-then-Google fixtures.
- `CarImageStore_RejectsOversizeAndUnknownType` — oversize is above 4 MB, not 512 KB.

## Open questions

(none)

## Accepted defaults

- Feature id `18-car-image-sources`; Seq **18**; Depends-on `11-cars`.
- Commons, then press for three makes, then Bing, then Google.
- Model-number token required for Commons and press. Bing/Google stay unfiltered, as in Seq 11.
- No Commons licence filter. The user stores one chosen file locally.
- Car photo cap is 4 MB for search and for local pick.
- Prefer the low-resolution press file when both sizes are present.
- Paid studio APIs are out of scope.

## Implementation notes for an agent

1. Extend `CarImageSearch` so `FindImageUrlsAsync` walks Commons, optional press, then Bing, then Google. Keep the existing Bing/Google URL helpers.
2. Teach `CarImageChooser` the new status strings. Chromium only for press, Bing, and Google, and only outside a dialog. Commons stays HttpClient.
3. Set `CarImageStore.MaxImageBytes` to 4 MB and update the sheet copy that still says 512 KB.
4. Add a Commons JSON fixture and one trimmed press HTML fixture that includes both an unrelated image and an E60 low-res/high-res pair. Reuse the Bing/Google fixtures for the last step.
5. Update the search sentence in `docs/screens/cars.md`.
6. Do not: add fields, call VIN sites, embed WebView2 in a ContentDialog, or send live requests from tests.
