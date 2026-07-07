# Global Team Widget

A native Windows 11 Widget Board provider for distributed team leads. Displays up to 8 configurable location tiles, each showing at a glance: local time, weather, currency exchange rate, a working-hours status indicator, and a 7-day non-working-day calendar strip.

![Widget screenshot](GlobalTeamWidget/Assets/WidgetScreenshot.png)

## Features

- **Local time** — refreshes every minute, handles non-whole-hour UTC offsets (IST, NPT, etc.)
- **Weather** — current conditions in both °C and °F via [Open-Meteo](https://open-meteo.com)
- **Currency rate** — exchange rate against a configurable global base currency via [open.er-api.com](https://open.er-api.com) (160+ currencies)
- **RAG working-hours indicator** — Green (in hours), Amber (within 60 min of start/end), Red (outside hours or public holiday)
- **7-day non-working-day strip** — marks weekends and public holidays per country via [Nager.Date](https://date.nager.at)
- **Offline resilience** — last known data shown with staleness timestamp for up to 24 hours without connectivity
- **Tile manager UI** — WPF settings window for adding, editing, and removing tiles; also launchable directly without the widget board

## Requirements

| Requirement | Version |
|---|---|
| Windows | 11 Build 22000 (21H2) or later |
| .NET SDK | 10.0 |
| Visual Studio | 2022 17.8+ with *Windows App SDK / WinUI 3* and *.NET Desktop Development* workloads |
| Architecture | x64 |

## Build & Install

```powershell
# 1. Open the solution
#    File → Open → GlobalTeamWidget.sln

# 2. Set startup project to GlobalTeamWidget (single-project MSIX build)

# 3. Build and deploy (F5)
#    Sideloads the MSIX package and registers the widget COM server

# 4. Add the widget
#    Win + W → click "+" → search "Global Team Widget" → Add
```

The widget registers as an out-of-process COM server (`--com-server` argument). When launched directly (without that argument) it opens the Tile Manager window instead.

## Running Tests

```bash
dotnet test GlobalTeamWidget.Tests/GlobalTeamWidget.Tests.csproj
```

## Project Structure

```
GlobalTeamWidget/           # Widget provider — WPF/.NET 10 WinExe
├── Models/                 # LocationTile, WeatherSnapshot, CurrencyRate, PublicHoliday
├── Services/               # Weather, Currency, Holiday, Timezone, Geocoding, Cache, Config
├── Widget/                 # IWidgetProvider COM server + AdaptiveCardBuilder
├── UI/                     # WPF settings/tile-manager windows
└── Assets/

GlobalTeamWidget.Tests/     # xUnit + Moq unit tests
```

## External APIs

All APIs are free with no API key required.

| Data | Provider | Notes |
|---|---|---|
| Weather | [Open-Meteo](https://open-meteo.com) | No key, global coverage |
| Geocoding | Open-Meteo Geocoding API | City → lat/lon lookup |
| Currency | [open.er-api.com](https://open.er-api.com) | 160+ currencies, daily refresh |
| Public holidays | [Nager.Date](https://date.nager.at) | 100+ countries |

## Configuration

Tile configuration is stored in `Windows.Storage.ApplicationData.LocalSettings` and persists across widget restarts. Weather, currency, and holiday data is cached as JSON in `LocalFolder` for offline operation.

Each tile stores: display name, IANA timezone ID, ISO 4217 currency code, country code (for holidays), working hours start/end, and working days (any subset of Mon–Sun).

The global base currency defaults to the currency detected from the system locale on first launch and can be changed in the Tile Manager settings.

## Known Limitations

- Currencies unavailable from open.er-api.com display "N/A" (very rare; the provider covers 160+ currencies)
- Countries not supported by Nager.Date show weekends only on the calendar strip, with a note icon
- Exchange rates update once daily (ECB business days); rates may appear unchanged over weekends
- Maximum of 8 tiles per widget instance
- Single-user; configuration is not synced across devices
