# Implementation Plan: Global Team Widget

**Branch**: `001-global-team-widget` | **Date**: 2026-06-25 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `specs/001-global-team-widget/spec.md`

## Summary

A native Windows 11 Widget Board provider that displays up to 8 configurable location tiles, each showing local time, weather (°C/°F), currency exchange rate, a RAG working-hours indicator (factoring in public holidays), and a 7-day non-working-day strip. Built in C# (.NET 8) with Windows App SDK 1.6, packaged as MSIX. External data is fetched from Open-Meteo (weather), Frankfurter (currency), and Nager.Date (holidays) — all free, no API key required. Tile configuration is persisted in Windows LocalSettings; a companion WinUI 3 window handles the rich edit UI triggered from the widget.

## Technical Context

**Language/Version**: C# / .NET 10.0

**Primary Dependencies**:
- `Microsoft.WindowsAppSDK` 1.6 (IWidgetProvider, IWidgetProvider2, WinUI 3)
- `TimeZoneConverter` NuGet (IANA ↔ Windows timezone conversion)
- `System.Text.Json` (built-in, JSON serialisation)
- `xUnit` + `Moq` (testing)

**Storage**: `Windows.Storage.ApplicationData.LocalSettings` (tile configuration) + JSON cache files in `LocalFolder` (offline weather/currency/holiday data)

**Testing**: xUnit + Moq

**Target Platform**: Windows 11 (Build 22000+), MSIX-packaged, .NET 10.0

**Project Type**: Desktop widget provider (out-of-process COM server) + companion WinUI 3 settings window

**Performance Goals**: External data refresh ≤ 15 minutes; local clock updates every 60 seconds; widget card render ≤ 500ms on action invoke

**Constraints**: Offline-capable for 24 hours using cached data; Frankfurter covers ~33 major currencies (unavailable currencies shown as "N/A"); MSIX packaging required for widget board registration

**Scale/Scope**: Single user, up to 8 tiles, up to 3 external API calls per tile per refresh cycle

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

No project constitution principles are defined (constitution.md contains only the template skeleton). No gates to evaluate. Constitution should be populated before implementation begins if project governance principles are required.

**Post-design re-check**: No violations identified. Three-project structure (provider + package + tests) is the minimum required by MSIX widget architecture, not an avoidable complexity.

## Project Structure

### Documentation (this feature)

```text
specs/001-global-team-widget/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output
├── quickstart.md        # Phase 1 output
├── contracts/           # Phase 1 output
│   ├── widget-actions.md
│   ├── configuration-schema.md
│   ├── weather-api.md
│   ├── currency-api.md
│   └── holidays-api.md
└── tasks.md             # Phase 2 output (/speckit-tasks)
```

### Source Code (repository root)

```text
GlobalTeamWidget/                        # Widget provider — .NET 8 Console App
├── GlobalTeamWidget.csproj
├── Program.cs                           # COM server entry point + WinUI 3 host
├── Models/
│   ├── LocationTile.cs
│   ├── WeatherSnapshot.cs
│   ├── CurrencyRate.cs
│   └── PublicHoliday.cs
├── Services/
│   ├── IWeatherService.cs
│   ├── WeatherService.cs
│   ├── ICurrencyService.cs
│   ├── CurrencyService.cs
│   ├── IHolidayService.cs
│   ├── HolidayService.cs
│   ├── ITimezoneService.cs
│   ├── TimezoneService.cs
│   ├── IConfigurationService.cs
│   └── ConfigurationService.cs
├── Widget/
│   ├── GlobalTeamWidgetProvider.cs      # IWidgetProvider + IWidgetProvider2
│   ├── AdaptiveCardBuilder.cs           # Builds Adaptive Cards 1.5 JSON
│   └── Cards/
│       └── dashboard-template.json
├── UI/
│   ├── SettingsWindow.xaml              # WinUI 3 tile edit window
│   └── SettingsWindow.xaml.cs
└── Assets/
    ├── WidgetIcon.scale-100.png
    └── StoreLogo.scale-100.png

GlobalTeamWidget.Package/                # MSIX packaging project
├── GlobalTeamWidget.Package.wapproj
└── Package.appxmanifest

GlobalTeamWidget.Tests/                  # xUnit + Moq test project
├── GlobalTeamWidget.Tests.csproj
├── Services/
│   ├── WeatherServiceTests.cs
│   ├── CurrencyServiceTests.cs
│   ├── HolidayServiceTests.cs
│   └── ConfigurationServiceTests.cs
└── Widget/
    └── AdaptiveCardBuilderTests.cs
```

**Structure Decision**: Provider project + MSIX packaging project + test project. The companion settings window lives inside the provider project (not a separate executable) to avoid cross-process communication overhead. The widget provider doubles as the WinUI 3 host when the settings window is triggered via `Action.Execute` with verb `open-edit`.

## Complexity Tracking

No constitution violations identified. Three-project layout is the minimum mandated by Windows App SDK MSIX widget architecture.
