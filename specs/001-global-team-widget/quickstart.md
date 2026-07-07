# Quickstart Validation Guide: Global Team Widget

**Purpose**: Runnable end-to-end validation scenarios to confirm the feature works correctly
**Date**: 2026-06-25

---

## Prerequisites

- Windows 11 Build 22000 or later
- Visual Studio 2022 17.8+ with the following workloads:
  - Windows App SDK / WinUI 3 workload
  - .NET Desktop Development
- `.NET 10.0 SDK`
- Internet connection (for initial data fetch validation)

---

## Build & Install

```powershell
# 1. Open solution in Visual Studio
#    File → Open → GlobalTeamWidget.sln

# 2. Set startup project to GlobalTeamWidget.Package

# 3. Build and deploy (F5 or Deploy from Build menu)
#    This sideloads the MSIX package and registers the widget provider

# 4. Open the Windows 11 Widget Board
#    Click the Widgets button on the taskbar (or Win + W)
#    Click "+" → search for "Global Team Widget" → Add
```

---

## Scenario 1: First Launch — Empty State

**Goal**: Confirm the widget launches with an empty state prompt.

1. Add the widget to the board for the first time
2. Verify the widget displays a message like "Add your first location to get started" with an "Add location" button
3. Verify no errors appear in Event Viewer under Application logs

---

## Scenario 2: Add a Location Tile

**Goal**: Confirm tile creation and data population.

1. Click "Add location" (or click an empty tile slot)
2. In the settings window, enter:
   - Display name: `Tokyo`
   - Timezone: `Asia/Tokyo`
   - Currency: `JPY`
   - Country: `JP`
   - Working hours: `09:00 – 18:00`, Mon–Fri
3. Save
4. Verify the tile appears showing:
   - Current Tokyo local time (compare with a known source)
   - Weather in °C and °F
   - JPY exchange rate vs. base currency
   - RAG working hours indicator (Green/Amber/Red as appropriate)
   - 7-day strip with weekends and any upcoming Japanese public holidays marked

---

## Scenario 3: RAG Indicator Accuracy

**Goal**: Confirm the working hours indicator responds to time and holidays.

1. Configure a tile for a location where the current time is within working hours → verify **Green**
2. Configure a tile for a location currently outside working hours → verify **Red**
3. Configure a tile for a location with a known public holiday today (requires test data or date manipulation):
   - Set system date to a known holiday (e.g. `2026-01-01` for Japan) → verify **Red** with holiday name shown

---

## Scenario 4: 7-Day Non-Working Day Strip

**Goal**: Confirm weekends and holidays are marked correctly.

1. Configure a tile for `GB` / `London`
2. Inspect the 7-day strip
3. Verify Saturday and Sunday are marked non-working
4. If any of the next 7 days contains a UK public holiday (see [Nager.Date](https://date.nager.at)), verify it is also marked

---

## Scenario 5: Offline Resilience

**Goal**: Confirm the widget shows stale data gracefully when offline.

1. Configure at least one tile and confirm it is showing live data
2. Disable network connectivity (turn off Wi-Fi / disconnect ethernet)
3. Wait for the normal refresh interval to pass (default 15 minutes)
4. Verify:
   - Tiles still show the last-known weather, currency, and holiday data
   - A staleness indicator (e.g. last-updated timestamp or warning icon) is visible
   - Local time continues to update correctly from the device clock

---

## Scenario 6: Base Currency Configuration

**Goal**: Confirm the global base currency setting works.

1. Open widget settings
2. Change the base currency from the detected default to `USD`
3. Save
4. Verify all tile currency rates update to reflect exchange rates against USD

---

## Scenario 7: Maximum Tiles

**Goal**: Confirm the 8-tile cap is enforced.

1. Add 8 location tiles one at a time
2. Verify all 8 are visible in the widget board
3. Attempt to add a 9th tile
4. Verify a clear message is shown explaining the maximum has been reached
5. Verify no 9th tile is created

---

## Scenario 8: Edit and Remove a Tile

**Goal**: Confirm tile modification and deletion.

1. Click an existing tile → settings window opens pre-populated with current values
2. Change the display name → save → verify tile shows updated name
3. Click a tile → choose Remove → verify tile is gone and remaining tiles reorder cleanly

---

## Known Limitations to Verify as Expected Behaviour

- Currencies not covered by Frankfurter (~33 ECB-tracked currencies) display "N/A" — this is correct behaviour
- Countries not supported by Nager.Date show weekends only on the calendar strip with a note icon — this is correct behaviour
- Exchange rates update once daily on ECB business days — rate may appear unchanged on weekends
