# Research: Global Team Widget

**Phase**: 0 — Pre-Design Research
**Date**: 2026-06-25
**Feature**: specs/001-global-team-widget/spec.md

---

## Decision 1: Widget Technology Stack

**Decision**: C# (.NET 10.0) with Windows App SDK 1.6, implementing `IWidgetProvider` + `IWidgetProvider2`, packaged as MSIX.

**Rationale**: Windows 11 Widget Board only accepts registered MSIX-packaged widget providers. The Windows App SDK 1.6 is the latest stable release and includes `IWidgetProvider2` (added in 1.4) which enables the customise action — needed for tile edit flow. A .NET 10 Console App is the recommended host for the out-of-process COM server.

**Alternatives considered**:
- PWA widget: Supported via Edge/PWA registration but limited to simpler card layouts; no rich in-process logic for data aggregation across 8 tiles.
- C++/WinRT: Supported but significantly higher development complexity for no tangible gain in this scenario.

---

## Decision 2: Adaptive Cards Version and Interaction Model

**Decision**: Adaptive Cards schema version 1.5 with `Action.Execute` for all interactive elements.

**Rationale**: The Windows 11 Widget Board renderer targets Adaptive Cards 1.5. `Action.Submit` is not supported in the widget board — all actions must use `Action.Execute` with a `verb` property that is handled by the `IWidgetProvider.OnActionInvoked` callback. The `Input.Text`, `Input.ChoiceSet`, and `Input.Toggle` elements are supported.

**Key constraint**: The edit panel cannot be rendered as an inline Adaptive Card input form due to layout complexity across 8 tiles. The recommended pattern is to trigger a `verb: "open-edit"` action from the tile, which causes the provider to launch a companion WinUI 3 settings window for rich configuration UI. The widget dashboard (read-only tiles) is rendered in Adaptive Cards.

**Alternatives considered**:
- Rendering the edit form entirely inside an Adaptive Card: Possible for simple cases but the 8-tile grid + working hours configuration exceeds the widget board's usable canvas.

---

## Decision 3: Weather API

**Decision**: Open-Meteo (`https://api.open-meteo.com/v1/forecast`)

**Rationale**: No API key required, no rate limits for non-commercial use, global coverage, returns current temperature and weather condition code, supports both metric and imperial. Simple REST JSON response.

**Alternatives considered**:
- WeatherAPI.com: Requires free API key signup; 1M calls/month free tier is sufficient but adds onboarding friction.
- Visual Crossing: Requires API key; better for commercial use if needed later.

---

## Decision 4: Currency Exchange Rate API

**Decision**: Frankfurter (`https://api.frankfurter.dev/latest`)

**Rationale**: No API key required, no rate limits, ~33 major currencies (ECB data), open-source. Sufficient for the primary use case of a team widget (major world currencies). Updated daily on ECB business days.

**Alternatives considered**:
- ExchangeRate-API: 161+ currencies but requires a free API key; 1,500 requests/month free limit risks exhaustion with 8 tiles refreshing frequently.
- fawazahmed0/exchange-api (GitHub CDN): 200+ currencies, no auth, but community-maintained with no SLA.

**Limitation**: Frankfurter covers ~33 ECB-tracked currencies. If a configured tile uses a currency not in that set (e.g., VND, MMK), the widget will display "rate unavailable" and note the limitation to the user.

---

## Decision 5: Public Holidays API

**Decision**: Nager.Date (`https://date.nager.at/api/v3/PublicHolidays/{year}/{countryCode}`)

**Rationale**: No API key required, no rate limits, 100+ countries, CORS-enabled, open-source. Covers years 1900–2099. ISO 3166-1 alpha-2 country codes. Returns holiday name, date, and whether it is a public holiday.

**Alternatives considered**:
- Abstract Holidays API: 200+ countries but requires an API key.
- API Ninjas Holidays: Requires a free account API key; coverage only 2005–2039.

---

## Decision 6: Timezone Handling

**Decision**: .NET 10.0 built-in `TimeZoneInfo` + `TimeZoneConverter` NuGet package.

**Rationale**: .NET 10 natively supports IANA timezone IDs and provides `TimeZoneInfo.TryConvertWindowsIdToIanaId()`. `TimeZoneConverter` adds safety for edge cases and simplifies bidirectional Windows ↔ IANA conversion. NodaTime was evaluated but is heavier and unnecessary when .NET 10 handles the core requirement.

**Alternatives considered**:
- NodaTime: More powerful, but introduces a significant dependency and learning curve for what is essentially timezone ID translation and offset calculation.

---

## Decision 7: Configuration Persistence

**Decision**: `Windows.Storage.ApplicationData.LocalSettings` for structured tile configuration, supplemented by a local JSON cache file for offline weather/currency/holiday data.

**Rationale**: `LocalSettings` is the idiomatic per-user, per-app settings store for MSIX-packaged Windows apps. It survives updates and is isolated per user. JSON cache files in `LocalFolder` provide the offline resilience required by SC-006 (24-hour offline operation).

**Alternatives considered**:
- Pure JSON file: Works but requires manual serialization and lacks the per-key atomic write guarantee of LocalSettings.
- SQLite: Overkill for configuration data at this scale.

---

## Decision 8: Test Framework

**Decision**: xUnit + Moq

**Rationale**: xUnit is the standard test framework for .NET 10 projects. Moq provides interface mocking needed to isolate service unit tests from external APIs.
