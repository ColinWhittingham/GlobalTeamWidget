# Contract: Public Holidays API (Nager.Date)

**Provider**: Nager.Date
**Base URL**: `https://date.nager.at/api/v3`
**Auth**: None required
**Rate limit**: None
**Coverage**: 100+ countries, years 1900–2099

---

## Request

```
GET https://date.nager.at/api/v3/PublicHolidays/{year}/{countryCode}
```

Example: `GET https://date.nager.at/api/v3/PublicHolidays/2026/JP`

Country code must be ISO 3166-1 alpha-2 uppercase (e.g. `JP`, `GB`, `DE`).

---

## Response

```json
[
  {
    "date": "2026-01-01",
    "localName": "元日",
    "name": "New Year's Day",
    "countryCode": "JP",
    "fixed": true,
    "global": true,
    "counties": null,
    "launchYear": null,
    "types": ["Public"]
  }
]
```

The widget uses only `date` and `name` from each entry. Only entries where `types` contains `"Public"` are treated as non-working days.

---

## Caching strategy

Holiday data is fetched **once per country per year** and cached to `LocalFolder/cache/holidays_{countryCode}_{year}.json`. The cache for the current year is considered valid until 30 days after the year ends (to handle late-published corrections). The next year's data is pre-fetched from 1 December onwards.

---

## Error handling

| Scenario | Behaviour |
|----------|-----------|
| HTTP 404 (country not supported) | Cache an empty holidays list; show weekends only on calendar strip; add a note icon indicating holiday data is unavailable for this country |
| HTTP non-200 | Log error; use last cached data if available; if no cache, use weekends only |
| Network timeout (>10s) | Same as non-200 |
