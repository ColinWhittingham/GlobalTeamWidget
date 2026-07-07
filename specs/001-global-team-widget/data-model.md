# Data Model: Global Team Widget

**Phase**: 1 — Design
**Date**: 2026-06-25

---

## Entities

### LocationTile

The core configuration unit. One record per tile displayed in the widget.

| Field | Type | Constraints |
|-------|------|-------------|
| `Id` | `Guid` | Required; system-assigned on creation |
| `DisplayName` | `string` | Required; max 30 chars; user-provided city/location label |
| `IanaTimezone` | `string` | Required; valid IANA timezone ID (e.g. `"America/New_York"`) |
| `CurrencyCode` | `string` | Required; ISO 4217 code (e.g. `"JPY"`); 3 chars uppercase |
| `WorkHoursStart` | `TimeOnly` | Required; local time; defaults to `09:00` |
| `WorkHoursEnd` | `TimeOnly` | Required; local time; defaults to `17:00` |
| `WorkingDays` | `DayOfWeek[]` | Required; defaults to Mon–Fri; min 1 day |
| `CountryCode` | `string` | Required; ISO 3166-1 alpha-2 (e.g. `"JP"`); used for holiday lookup |
| `DisplayOrder` | `int` | Required; 0-based; determines tile position (0–7) |

**Validation rules**:
- `WorkHoursEnd` must be after `WorkHoursStart`
- `DisplayOrder` must be unique across all tiles
- Maximum 8 `LocationTile` records

**State transitions**: Created → Configured → (optional) Removed

---

### WeatherSnapshot

Cached weather data for a tile. Refreshed on a background schedule.

| Field | Type | Constraints |
|-------|------|-------------|
| `TileId` | `Guid` | FK → LocationTile.Id |
| `TemperatureCelsius` | `decimal` | One decimal place |
| `TemperatureFahrenheit` | `decimal` | Derived: `(C × 9/5) + 32`; stored for display without recalculation |
| `ConditionCode` | `int` | WMO weather code from Open-Meteo |
| `ConditionLabel` | `string` | Human-readable label derived from WMO code (e.g. `"Partly cloudy"`) |
| `FetchedAt` | `DateTimeOffset` | UTC timestamp of last successful API call |
| `IsStale` | `bool` | Derived: `true` if `FetchedAt` is older than 15 minutes |

---

### CurrencyRate

Cached exchange rate for a tile's configured currency against the global base currency.

| Field | Type | Constraints |
|-------|------|-------------|
| `TileId` | `Guid` | FK → LocationTile.Id |
| `QuoteCurrency` | `string` | ISO 4217; tile's configured currency |
| `BaseCurrency` | `string` | ISO 4217; user's global base currency setting |
| `Rate` | `decimal` | Up to 6 decimal places |
| `FetchedAt` | `DateTimeOffset` | UTC timestamp |
| `IsAvailable` | `bool` | `false` if Frankfurter does not cover the currency pair |
| `IsStale` | `bool` | Derived: `true` if `FetchedAt` is older than 15 minutes |

---

### PublicHoliday

Holiday data fetched from Nager.Date. Cached per country per year.

| Field | Type | Constraints |
|-------|------|-------------|
| `CountryCode` | `string` | ISO 3166-1 alpha-2 |
| `Date` | `DateOnly` | Calendar date |
| `Name` | `string` | Holiday name in English |
| `IsPublic` | `bool` | Always `true` (only public holidays returned by Nager.Date) |

**Cache scope**: Per country + year pair. Fetched once per year per country, stored in `LocalFolder`.

---

### WorkingHoursStatus

Derived/computed — not persisted. Calculated on demand for each tile.

| Field | Type | Notes |
|-------|------|-------|
| `TileId` | `Guid` | |
| `Status` | `enum: Green, Amber, Red` | Green = in hours, non-holiday; Amber = within 60 min of boundary; Red = out of hours or holiday |
| `Reason` | `string` | e.g. `"Public holiday: Midsommar"`, `"Outside working hours"`, `"Within working hours"` |
| `EvaluatedAt` | `DateTimeOffset` | UTC time of calculation |

**RAG logic**:
1. If today is a `PublicHoliday` for the tile's country → **Red** (reason: holiday name)
2. Else if current local time is within `WorkHoursStart`–`WorkHoursEnd` on a `WorkingDay` → **Green**
3. Else if current local time is within 60 minutes before `WorkHoursStart` or within 60 minutes after `WorkHoursEnd` on a `WorkingDay` → **Amber**
4. Otherwise → **Red**

---

### NonWorkingDayStrip

Derived/computed — not persisted. Built fresh for each dashboard render.

| Field | Type | Notes |
|-------|------|-------|
| `TileId` | `Guid` | |
| `Days` | `NonWorkingDay[7]` | Today + next 6 days |

**NonWorkingDay**:

| Field | Type | Notes |
|-------|------|-------|
| `Date` | `DateOnly` | |
| `IsNonWorking` | `bool` | `true` if weekend or public holiday |
| `Reason` | `enum: WorkingDay, Weekend, PublicHoliday` | |

---

### GlobalSettings

Single-record configuration for widget-wide preferences.

| Field | Type | Constraints |
|-------|------|-------------|
| `BaseCurrencyCode` | `string` | ISO 4217; defaults to system locale currency on first launch |
| `DataRefreshIntervalMinutes` | `int` | Defaults to 15; min 5, max 60 |

---

## Relationships

```text
GlobalSettings (1)
    └── governs base currency for all CurrencyRate records

LocationTile (1) ──── (1) WeatherSnapshot
LocationTile (1) ──── (1) CurrencyRate
LocationTile (1) ──── (derived) WorkingHoursStatus
LocationTile (1) ──── (derived) NonWorkingDayStrip
LocationTile (N) ──── PublicHoliday (via CountryCode, cached separately)
```
