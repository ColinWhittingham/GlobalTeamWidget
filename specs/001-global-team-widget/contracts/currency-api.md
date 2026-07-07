# Contract: Currency API (Frankfurter)

**Provider**: Frankfurter (ECB data)
**Base URL**: `https://api.frankfurter.dev`
**Auth**: None required
**Rate limit**: None
**Update frequency**: Daily on ECB business days (~33 major currencies)

---

## Request

```
GET https://api.frankfurter.dev/latest?from={baseCurrency}&to={quoteCurrency}
```

Example: `GET https://api.frankfurter.dev/latest?from=GBP&to=JPY`

---

## Response

```json
{
  "base": "GBP",
  "date": "2026-06-24",
  "rates": {
    "JPY": 194.32
  }
}
```

---

## Currency availability

Frankfurter covers ~33 ECB-tracked currencies. If the tile's `CurrencyCode` is not in the supported set, the API returns a 404 or empty rates object.

**Supported currencies** (representative list): AUD, BGN, BRL, CAD, CHF, CNY, CZK, DKK, EUR, GBP, HKD, HUF, IDR, ILS, INR, ISK, JPY, KRW, MXN, MYR, NOK, NZD, PHP, PLN, RON, SEK, SGD, THB, TRY, USD, ZAR.

**When unavailable**: Set `CurrencyRate.IsAvailable = false`; display `"N/A"` on the tile with a tooltip noting the currency is not supported by the data source.

---

## Error handling

| Scenario | Behaviour |
|----------|-----------|
| HTTP 404 (unsupported currency) | Set `IsAvailable = false`; show "N/A" on tile |
| HTTP non-200 | Log error; retain last cached rate; set `IsStale = true` |
| Network timeout (>10s) | Same as non-200 |
| Base = Quote (same currency) | Skip API call; display rate as `1.00` |
