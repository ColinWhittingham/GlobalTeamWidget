# Contract: Configuration Schema

**Type**: Local persistence schema
**Storage**: `Windows.Storage.ApplicationData.LocalSettings` (structured values) + JSON files in `LocalFolder`

---

## LocalSettings Keys

All keys are prefixed with `gtw_` to avoid collisions.

### Global Settings

| Key | Type | Default | Notes |
|-----|------|---------|-------|
| `gtw_baseCurrency` | `string` | System locale currency | ISO 4217 code |
| `gtw_refreshIntervalMinutes` | `int` | `15` | Range: 5–60 |
| `gtw_tileCount` | `int` | `0` | Current number of configured tiles |
| `gtw_tileOrder` | `string` | `""` | Comma-separated list of tile GUIDs in display order |

### Per-Tile Settings

Key pattern: `gtw_tile_{guid}_{field}`

| Key Pattern | Type | Notes |
|-------------|------|-------|
| `gtw_tile_{id}_displayName` | `string` | Max 30 chars |
| `gtw_tile_{id}_ianaTimezone` | `string` | e.g. `"Asia/Tokyo"` |
| `gtw_tile_{id}_currencyCode` | `string` | ISO 4217 |
| `gtw_tile_{id}_countryCode` | `string` | ISO 3166-1 alpha-2 |
| `gtw_tile_{id}_workStart` | `string` | `"HH:mm"` format |
| `gtw_tile_{id}_workEnd` | `string` | `"HH:mm"` format |
| `gtw_tile_{id}_workingDays` | `string` | Comma-separated day numbers (0=Sun … 6=Sat) e.g. `"1,2,3,4,5"` |

---

## Cache Files (LocalFolder)

### Weather Cache

**Path**: `LocalFolder/cache/weather_{tileId}.json`

```json
{
  "tileId": "guid",
  "temperatureCelsius": 18.4,
  "temperatureFahrenheit": 65.1,
  "conditionCode": 2,
  "conditionLabel": "Partly cloudy",
  "fetchedAt": "2026-06-25T10:00:00Z"
}
```

### Currency Cache

**Path**: `LocalFolder/cache/currency_{tileId}.json`

```json
{
  "tileId": "guid",
  "quoteCurrency": "JPY",
  "baseCurrency": "GBP",
  "rate": 194.32,
  "isAvailable": true,
  "fetchedAt": "2026-06-25T10:00:00Z"
}
```

### Holiday Cache

**Path**: `LocalFolder/cache/holidays_{countryCode}_{year}.json`

```json
{
  "countryCode": "JP",
  "year": 2026,
  "fetchedAt": "2026-06-25T10:00:00Z",
  "holidays": [
    {
      "date": "2026-01-01",
      "name": "New Year's Day"
    }
  ]
}
```
