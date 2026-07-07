# Contract: Weather API (Open-Meteo)

**Provider**: Open-Meteo
**Base URL**: `https://api.open-meteo.com/v1/forecast`
**Auth**: None required
**Rate limit**: None (non-commercial use)

---

## Request

```
GET https://api.open-meteo.com/v1/forecast
  ?latitude={lat}
  &longitude={lon}
  &current=temperature_2m,weathercode
  &temperature_unit=celsius
  &timezone=auto
```

Latitude/longitude are resolved from the tile's city name at configuration time (one-time geocoding, not per-refresh).

**Geocoding** (one-time, at tile save): Use the Open-Meteo geocoding endpoint:
```
GET https://geocoding-api.open-meteo.com/v1/search?name={cityName}&count=1
```
Store `latitude`, `longitude`, and `timezone` from the result on the `LocationTile`.

---

## Response (relevant fields)

```json
{
  "current": {
    "temperature_2m": 18.4,
    "weathercode": 2
  }
}
```

## WMO Weather Code → Condition Label mapping (subset)

| Code | Label |
|------|-------|
| 0 | Clear sky |
| 1–3 | Partly cloudy |
| 45, 48 | Fog |
| 51–57 | Drizzle |
| 61–67 | Rain |
| 71–77 | Snow |
| 80–82 | Rain showers |
| 95 | Thunderstorm |
| 96, 99 | Thunderstorm with hail |

Full mapping: https://open-meteo.com/en/docs#weathervariables

---

## Error handling

| Scenario | Behaviour |
|----------|-----------|
| HTTP non-200 | Log error; retain last cached snapshot; set `IsStale = true` |
| Network timeout (>10s) | Same as non-200 |
| City not found in geocoding | Display "Location not found" on tile; block tile save |
