# Tasks: Global Team Widget

**Input**: Design documents from `specs/001-global-team-widget/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/

**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no shared dependencies)
- **[Story]**: Which user story this task belongs to (US1–US4)

---

## Phase 1: Setup

**Purpose**: Create solution, projects, and shared infrastructure

- [x] T001 Create solution `GlobalTeamWidget.sln` with three projects: `GlobalTeamWidget` (.NET 10 Console App), `GlobalTeamWidget.Package` (MSIX packaging), `GlobalTeamWidget.Tests` (xUnit) per plan.md structure
- [x] T002 Add NuGet packages to `GlobalTeamWidget/GlobalTeamWidget.csproj`: `Microsoft.WindowsAppSDK` 1.6, `TimeZoneConverter`, `System.Text.Json` (built-in)
- [x] T003 Add NuGet packages to `GlobalTeamWidget.Tests/GlobalTeamWidget.Tests.csproj`: `xUnit`, `Moq`, project reference to `GlobalTeamWidget`
- [x] T004 [P] Configure `GlobalTeamWidget.Package/Package.appxmanifest` with widget provider COM registration, `uap3:AppExtension` name `"com.microsoft.windows.widgets"`, and widget definition (display name, description, icon paths)
- [x] T005 [P] Configure `GlobalTeamWidget/Program.cs` as out-of-process COM server entry point: register `GlobalTeamWidgetProvider` class factory and block until host exits
- [x] T006 [P] Add placeholder widget icon assets to `GlobalTeamWidget/Assets/`: `WidgetIcon.scale-100.png` and `StoreLogo.scale-100.png` (16×16 and 44×44 minimum)

**Checkpoint**: Solution builds, MSIX deploys, and widget appears in the widget board (empty/placeholder state)

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Core infrastructure required by every user story — configuration persistence, caching, and HTTP plumbing

**⚠️ CRITICAL**: No user story work can begin until this phase is complete

- [x] T007 Implement `GlobalSettings` model in `GlobalTeamWidget/Models/GlobalSettings.cs`: `BaseCurrencyCode` (string, ISO 4217), `DataRefreshIntervalMinutes` (int, default 15)
- [x] T008 Implement `IConfigurationService` interface and `ConfigurationService` in `GlobalTeamWidget/Services/ConfigurationService.cs`: read/write all `gtw_*` LocalSettings keys per `contracts/configuration-schema.md`; implement `LoadGlobalSettings`, `SaveGlobalSettings`, `GetTileIds`, `LoadTile`, `SaveTile`, `RemoveTile`; detect and default `BaseCurrencyCode` from system locale on first run
- [x] T009 [P] Implement JSON cache file helpers in `GlobalTeamWidget/Services/CacheService.cs`: `ReadCacheAsync<T>`, `WriteCacheAsync<T>` using `ApplicationData.LocalFolder`; paths follow `contracts/configuration-schema.md` (`cache/weather_{id}.json`, `cache/currency_{id}.json`, `cache/holidays_{cc}_{year}.json`)
- [x] T010 [P] Configure `HttpClient` factory in `GlobalTeamWidget/Program.cs`: named clients for `"weather"`, `"currency"`, and `"holidays"` with 10-second timeout and `User-Agent` header; register as singletons for DI

**Checkpoint**: Configuration round-trips correctly (save a tile, restart, tile re-loads). Cache files appear in LocalFolder.

---

## Phase 3: User Story 1 — View Location Dashboard at a Glance (Priority: P1) 🎯 MVP

**Goal**: A user opens the widget and immediately sees time, weather, currency, and working-hours RAG status for all configured tiles without any interaction.

**Independent Test**: Configure two tiles (e.g. London + Tokyo). Open the widget board. Verify both tiles display current local time, weather in °C and °F, currency exchange rate, and a correctly coloured RAG indicator — no clicks required. Verify a tile configured for a current public holiday shows Red.

### Implementation

- [x] T011 [P] [US1] Implement `LocationTile` model in `GlobalTeamWidget/Models/LocationTile.cs`: all fields from data-model.md (`Id`, `DisplayName`, `IanaTimezone`, `CurrencyCode`, `WorkHoursStart`, `WorkHoursEnd`, `WorkingDays`, `CountryCode`, `DisplayOrder`)
- [x] T012 [P] [US1] Implement `WeatherSnapshot` model in `GlobalTeamWidget/Models/WeatherSnapshot.cs`: all fields from data-model.md including derived `IsStale` (true if `FetchedAt` > 15 minutes ago)
- [x] T013 [P] [US1] Implement `CurrencyRate` model in `GlobalTeamWidget/Models/CurrencyRate.cs`: all fields from data-model.md including `IsAvailable` and derived `IsStale`
- [x] T014 [P] [US1] Implement `PublicHoliday` model in `GlobalTeamWidget/Models/PublicHoliday.cs`: `CountryCode`, `Date` (DateOnly), `Name`
- [x] T015 [US1] Implement `ITimezoneService` and `TimezoneService` in `GlobalTeamWidget/Services/TimezoneService.cs`: `GetLocalTime(ianaTimezone)` returns current `DateTimeOffset` in that zone using `TimeZoneConverter.TZConvert`; handle non-whole-hour offsets (IST, NPT, etc.)
- [x] T016 [US1] Implement `IWeatherService` and `WeatherService` in `GlobalTeamWidget/Services/WeatherService.cs`: `GetWeatherAsync(tileId, lat, lon)` calls Open-Meteo per `contracts/weather-api.md`; map WMO code to condition label; cache result to `CacheService`; return last cache on HTTP error with `IsStale = true`
- [x] T017 [US1] Implement `ICurrencyService` and `CurrencyService` in `GlobalTeamWidget/Services/CurrencyService.cs`: `GetRateAsync(tileId, quoteCurrency, baseCurrency)` calls Frankfurter per `contracts/currency-api.md`; set `IsAvailable = false` on 404; cache result; handle same-currency case (rate = 1.00) without API call
- [x] T018 [US1] Implement `IHolidayService` and `HolidayService` in `GlobalTeamWidget/Services/HolidayService.cs`: `GetHolidaysAsync(countryCode, year)` calls Nager.Date per `contracts/holidays-api.md`; cache per country+year to `CacheService`; return empty list (not error) for unsupported countries
- [x] T019 [US1] Implement `WorkingHoursStatus` computation in `GlobalTeamWidget/Widget/AdaptiveCardBuilder.cs`: apply RAG logic from data-model.md (public holiday → Red; in-hours working day → Green; within 60 min of boundary → Amber; otherwise → Red)
- [x] T020 [US1] Implement `NonWorkingDayStrip` computation in `GlobalTeamWidget/Widget/AdaptiveCardBuilder.cs`: build 7-day array (today + 6) marking each day as WorkingDay, Weekend, or PublicHoliday using `HolidayService` and tile's `WorkingDays`
- [x] T021 [US1] Create Adaptive Cards 1.5 dashboard template in `GlobalTeamWidget/Widget/Cards/dashboard-template.json`: tile grid layout showing display name, local time, weather (°C / °F), currency rate, RAG colour block, and 7-day strip; use `Action.Execute` with verb `"open-edit"` on tile click; include verb `"refresh"` button
- [x] T022 [US1] Implement `GlobalTeamWidgetProvider` in `GlobalTeamWidget/Widget/GlobalTeamWidgetProvider.cs`: implement `IWidgetProvider`; handle `CreateWidget`, `DeleteWidget`, `OnActionInvoked` (dispatch on `open-edit`, `refresh` verbs), `OnWidgetContextChanged`; on `refresh` or timer fire, fetch all services for each tile and call `UpdateWidget` with rebuilt card JSON
- [x] T023 [US1] Wire all services and the background refresh timer (default 15-minute interval, configurable via `GlobalSettings`) into `GlobalTeamWidgetProvider` via constructor injection in `GlobalTeamWidget/Program.cs`

**Checkpoint**: Widget displays a live dashboard for configured tiles. RAG indicator changes colour correctly. Weather and currency show real data. Refresh works.

---

## Phase 4: User Story 2 — Configure a Location Tile (Priority: P2)

**Goal**: A user clicks a tile to open an edit panel and configures city, timezone, currency, country, and working hours; changes persist and the tile updates immediately.

**Independent Test**: Click a tile. Set city to "Tokyo", timezone "Asia/Tokyo", currency "JPY", country "JP", hours 09:00–18:00 Mon–Fri. Save. Tile shows Tokyo time, JPY rate, correct RAG status. Close and reopen widget — configuration persists.

### Implementation

- [x] T024 [US2] Implement `IGeocodingService` and `GeocodingService` in `GlobalTeamWidget/Services/GeocodingService.cs`: `SearchCityAsync(name)` calls Open-Meteo geocoding endpoint per `contracts/weather-api.md`; return `latitude`, `longitude`, and suggested IANA timezone; show "Location not found" if no result
- [x] T025 [US2] Implement `SettingsWindow.xaml` in `GlobalTeamWidget/UI/SettingsWindow.xaml`: WinUI 3 window with fields for display name (TextBox, max 30 chars), city search (TextBox + search button wired to `GeocodingService`), timezone (ComboBox, pre-populated from geocoding result, user-editable), currency code (TextBox, ISO 4217), country code (TextBox, ISO 3166-1 alpha-2), work hours start/end (TimePicker), working days (CheckBox group Mon–Sun); "Save" and "Cancel" buttons
- [x] T026 [US2] Implement `SettingsWindow.xaml.cs` in `GlobalTeamWidget/UI/SettingsWindow.xaml.cs`: validate all fields (work end > work start, at least one working day, non-empty display name); on Save, call `ConfigurationService.SaveTile` and raise `TileConfigured` event; on Cancel, close without saving; pre-populate fields when editing an existing tile
- [x] T027 [US2] Handle `open-edit` verb in `GlobalTeamWidgetProvider.OnActionInvoked` in `GlobalTeamWidget/Widget/GlobalTeamWidgetProvider.cs`: extract `tileId` from action data; launch `SettingsWindow` on the UI thread; subscribe to `TileConfigured` event; on event, trigger immediate data refresh for the tile and call `UpdateWidget` with rebuilt card
- [x] T028 [US2] Implement `IWidgetProvider2.OnCustomizationRequested` in `GlobalTeamWidget/Widget/GlobalTeamWidgetProvider.cs`: launch `SettingsWindow` for the widget's first tile (or a tile-picker if multiple) when the user selects "Customise" from the widget board context menu

**Checkpoint**: Full configure-tile round trip works. Geocoding auto-populates timezone. Edited tile reflects new values immediately. Reopening settings shows saved values.

---

## Phase 5: User Story 3 — Review Non-Working Days (Priority: P2)

**Goal**: The 7-day strip on each tile clearly distinguishes working days, weekends, and public holidays; public holiday names are visible; unsupported countries degrade gracefully.

**Independent Test**: Configure a tile for a country with a known public holiday in the next 7 days. Verify the holiday day is visually distinct from a weekend day. Configure a tile for a country not supported by Nager.Date — verify weekends still show and a note indicator is visible.

### Implementation

- [x] T029 [US3] Enhance `dashboard-template.json` in `GlobalTeamWidget/Widget/Cards/dashboard-template.json`: visually differentiate the three non-working-day strip states — working day (default), weekend (grey tint), public holiday (amber tint); add holiday name as a sub-label on holiday cells
- [x] T030 [US3] Update `AdaptiveCardBuilder` in `GlobalTeamWidget/Widget/AdaptiveCardBuilder.cs`: pass `NonWorkingDay.Reason` and holiday `Name` into the card data object so the template can render the correct visual state and label per day cell
- [x] T031 [US3] Implement next-year holiday pre-fetch in `GlobalTeamWidget/Services/HolidayService.cs`: from 1 December onwards, automatically fetch and cache the following year's holiday data for each tile's country during the regular refresh cycle
- [x] T032 [US3] Implement unsupported-country fallback indicator in `GlobalTeamWidget/Widget/AdaptiveCardBuilder.cs`: when `HolidayService` returns an empty list due to an unsupported country (not a network error), include a small info icon in the card JSON with tooltip text "Public holiday data unavailable for this country"

**Checkpoint**: Strip correctly distinguishes all three day types. Holiday names appear. Unsupported-country tiles show the note icon. Next-year data pre-fetches silently in December.

---

## Phase 6: User Story 4 — Manage Location Tiles (Priority: P3)

**Goal**: User can add tiles up to the 8-tile maximum and remove existing tiles; the dashboard updates cleanly after each change.

**Independent Test**: Add 8 tiles; verify all appear. Attempt a 9th; verify rejection message. Remove one tile; verify it disappears and the remaining tiles render without gaps.

### Implementation

- [x] T033 [US4] Enforce 8-tile maximum in `GlobalTeamWidget/Services/ConfigurationService.cs`: `SaveTile` raises an exception (or returns a failure result) when `GetTileIds().Count >= 8`; add `GetRemainingSlots()` helper
- [x] T034 [US4] Build empty-state and "add tile" prompt card in `GlobalTeamWidget/Widget/AdaptiveCardBuilder.cs`: when tile count is 0, generate a card with "Add your first location" message and `Action.Execute` verb `"open-edit"` with no `tileId`; when tile count is 1–7, include an "Add location" cell in the dashboard grid; when tile count is 8, omit the add cell
- [x] T035 [US4] Add "Remove this location" button to `SettingsWindow.xaml` / `SettingsWindow.xaml.cs` in `GlobalTeamWidget/UI/`: show a confirmation prompt; on confirm, call `ConfigurationService.RemoveTile(tileId)` and raise `TileRemoved` event
- [x] T036 [US4] Implement tile `DisplayOrder` recompaction in `GlobalTeamWidget/Services/ConfigurationService.cs`: after `RemoveTile`, re-assign `DisplayOrder` values 0…N-1 to remaining tiles to eliminate gaps
- [x] T037 [US4] Handle `TileRemoved` event and `remove-tile` verb in `GlobalTeamWidget/Widget/GlobalTeamWidgetProvider.cs`: cancel the tile's refresh subscription, invalidate its cache files via `CacheService`, and call `UpdateWidget` with a fully rebuilt dashboard card

**Checkpoint**: Add/remove cycle works end-to-end. 8-tile cap is enforced with a clear message. Dashboard re-renders without gaps after removal.

---

## Phase 7: Polish & Cross-Cutting Concerns

**Purpose**: Offline resilience, staleness display, global base currency UI, and final validation

- [x] T038 [P] Implement staleness indicator rendering in `GlobalTeamWidget/Widget/AdaptiveCardBuilder.cs`: when `WeatherSnapshot.IsStale` or `CurrencyRate.IsStale` is true, append a "last updated HH:mm" sub-label to the affected tile field in the card JSON
- [x] T039 [P] Implement offline detection in `GlobalTeamWidget/Services/WeatherService.cs`, `CurrencyService.cs`, and `HolidayService.cs`: catch `HttpRequestException` / `TaskCanceledException`; serve cached data; local clock continues to tick independently of network state (time display is never stale)
- [x] T040 [P] Add base currency picker to `GlobalTeamWidget/UI/SettingsWindow.xaml` and `SettingsWindow.xaml.cs`: a separate "Global Settings" section (or second tab) with a currency code field defaulting to system locale; on save, call `ConfigurationService.SaveGlobalSettings` and trigger a full currency refresh across all tiles
- [x] T041 [P] Add structured logging throughout all services in `GlobalTeamWidget/`: use `Microsoft.Extensions.Logging` writing to Windows Event Log under source `"GlobalTeamWidget"`; log API errors, cache hits/misses, and widget lifecycle events at appropriate levels
- [x] T042 Run all eight scenarios in `specs/001-global-team-widget/quickstart.md` end-to-end and resolve any failures

---

## Dependencies & Execution Order

### Phase Dependencies

- **Phase 1 (Setup)**: No dependencies — start immediately
- **Phase 2 (Foundational)**: Depends on Phase 1 — **blocks all user story phases**
- **Phase 3 (US1)**: Depends on Phase 2 — first user story, no story dependencies
- **Phase 4 (US2)**: Depends on Phase 2 — can proceed in parallel with Phase 3 after Phase 2 completes; requires `ConfigurationService` from Phase 2
- **Phase 5 (US3)**: Depends on Phase 3 (requires `HolidayService` and `AdaptiveCardBuilder` from US1)
- **Phase 6 (US4)**: Depends on Phase 3 (requires `AdaptiveCardBuilder`) and Phase 4 (requires `SettingsWindow`)
- **Phase 7 (Polish)**: Depends on all story phases

### User Story Dependencies

- **US1 (P1)**: Can start after Foundational — no story dependencies
- **US2 (P2)**: Can start after Foundational — independent of US1 (separate files), but share `ConfigurationService`
- **US3 (P2)**: Depends on US1 — extends `HolidayService` and `AdaptiveCardBuilder` built in US1
- **US4 (P3)**: Depends on US1 (AdaptiveCardBuilder) and US2 (SettingsWindow)

### Within Each Phase

- Models (T011–T014) before services (T015–T018)
- Services before widget provider wiring (T022–T023)
- `GeocodingService` (T024) before `SettingsWindow` (T025–T026)

### Parallel Opportunities

- T004, T005, T006 can run in parallel (different files, all in Phase 1)
- T009, T010 can run in parallel (Phase 2)
- T011, T012, T013, T014 can all run in parallel (Phase 3 models, separate files)
- T015, T016, T017, T018 can run in parallel after models are complete
- T019, T020 can run in parallel (both in AdaptiveCardBuilder but different methods)
- T038, T039, T040, T041 can all run in parallel (Phase 7, separate files/concerns)

---

## Parallel Example: Phase 3 (US1)

```
# Run in parallel — no shared dependencies:
T011: LocationTile model          → GlobalTeamWidget/Models/LocationTile.cs
T012: WeatherSnapshot model       → GlobalTeamWidget/Models/WeatherSnapshot.cs
T013: CurrencyRate model          → GlobalTeamWidget/Models/CurrencyRate.cs
T014: PublicHoliday model         → GlobalTeamWidget/Models/PublicHoliday.cs

# After models complete, run in parallel:
T015: TimezoneService             → GlobalTeamWidget/Services/TimezoneService.cs
T016: WeatherService              → GlobalTeamWidget/Services/WeatherService.cs
T017: CurrencyService             → GlobalTeamWidget/Services/CurrencyService.cs
T018: HolidayService              → GlobalTeamWidget/Services/HolidayService.cs
```

---

## Implementation Strategy

### MVP (US1 Only — Phases 1–3)

1. Complete Phase 1: Setup → widget registers and appears in board
2. Complete Phase 2: Foundational → config and cache infrastructure ready
3. Complete Phase 3: US1 → live dashboard with all tile data
4. **STOP AND VALIDATE**: Run Scenarios 1–4 from quickstart.md
5. Demo: widget shows real-time location data for configured tiles

### Incremental Delivery

1. Phase 1 + 2 → infrastructure foundation
2. + Phase 3 (US1) → read-only dashboard (MVP, demoed)
3. + Phase 4 (US2) → full configure/edit flow
4. + Phase 5 (US3) → richer 7-day strip with holiday names and fallbacks
5. + Phase 6 (US4) → add/remove tile management
6. + Phase 7 → offline resilience, staleness UI, logging, final QA

### Parallel Team Strategy

After Phase 2 completes:
- Developer A: Phase 3 (US1) — services and widget provider
- Developer B: Phase 4 (US2) — GeocodingService and SettingsWindow
- Both merge, then proceed to Phase 5 → Phase 6 → Phase 7

---

## Notes

- `[P]` tasks operate on different files with no shared incomplete dependencies — safe to parallelise
- `[US1]`–`[US4]` labels map each task to its user story for traceability
- No test tasks generated (not requested in spec); add TDD tasks before any phase if desired
- All external API calls use the named `HttpClient` instances registered in Phase 1 (T010)
- `Action.Submit` is not supported by the widget board — all card interactions use `Action.Execute`
- Commit after each checkpoint to preserve independently working increments
