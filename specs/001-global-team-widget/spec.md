# Feature Specification: Global Team Widget

**Feature Branch**: `001-global-team-widget`

**Created**: 2026-06-25

**Status**: Draft

**Input**: User description: "A native Windows 11 sidebar widget for managing a geographically distributed team, with up to 8 location tiles. Each tile shows: current local time, weather with both Celsius and Fahrenheit, local currency exchange rate, a Red/Amber/Green colour indicator for working hours (factoring in public holidays), and a visual indicator for the next 7 days showing non-working days. Clicking a tile opens an edit panel to configure the tile's parameters (city/location, timezone, currency, working hours). No team-member-level data — purely location-level information."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - View Location Dashboard at a Glance (Priority: P1)

A distributed team lead opens the widget and immediately sees the current status of all their configured locations — local time, weather, currency rate, and a colour-coded working hours indicator — without any interaction required.

**Why this priority**: This is the core value proposition. Every other feature is secondary to the at-a-glance dashboard. An MVP with read-only tiles already delivers daily value.

**Independent Test**: Configure two location tiles. Open the widget. Verify that current local time (updating each minute), weather in both C and F, currency exchange rate, and a RAG working hours indicator are all visible without clicking anything.

**Acceptance Scenarios**:

1. **Given** the widget is open with at least one configured tile, **When** the user views the widget, **Then** each tile displays current local time, weather (°C and °F), currency exchange rate, and a RAG working hours indicator without any interaction
2. **Given** a location tile is configured for a city that is currently outside working hours, **When** the user views the widget, **Then** that tile's indicator is Red
3. **Given** a location tile is configured for a city observing a public holiday today, **When** the user views the widget, **Then** that tile's working hours indicator is Red regardless of time of day
4. **Given** it is within one hour before or after working hours in a location, **When** the user views the widget, **Then** that tile's working hours indicator is Amber

---

### User Story 2 - Configure a Location Tile (Priority: P2)

A user adds a new office location or updates an existing one by clicking a tile and editing its parameters: city name, timezone, currency, and working hours.

**Why this priority**: Without configuration, the widget has no locations to show. This is required immediately after first launch, and whenever team geography changes.

**Independent Test**: Click an empty tile slot. Set city to "Tokyo", timezone to "Asia/Tokyo", currency to "JPY", working hours to 09:00–18:00 Mon–Fri. Save. Verify the tile now shows Tokyo time, weather, JPY rate, and correct RAG status.

**Acceptance Scenarios**:

1. **Given** the user clicks a configured tile, **When** the edit panel opens, **Then** it shows the current values for city name, timezone, currency, working hours start/end, and working days
2. **Given** the user updates any field and saves, **When** returning to the dashboard, **Then** the tile immediately reflects the updated configuration
3. **Given** the user clicks an empty tile slot (below the current tile count), **When** the edit panel opens, **Then** it presents a blank form to create a new location
4. **Given** the user saves a configuration, **When** the widget is closed and reopened, **Then** the configuration is still present

---

### User Story 3 - Review Non-Working Days for the Next 7 Days (Priority: P2)

A user planning a deadline or cross-location meeting checks the 7-day strip on each tile to see which days are non-working (weekends or public holidays) in each location.

**Why this priority**: Equal priority to tile configuration because the calendar strip is a key differentiator — it prevents scheduling mistakes caused by local holidays the user may not know about.

**Independent Test**: Configure a tile for a location with a known public holiday within the next 7 days. Verify that day is visually marked as non-working on the tile's calendar strip.

**Acceptance Scenarios**:

1. **Given** a tile is configured, **When** the user views the tile, **Then** a 7-day strip starting from today is visible, with each day labelled (e.g., Mon, Tue)
2. **Given** a day within the next 7 days is a weekend for that location, **When** viewing the strip, **Then** that day is visually distinguished from working days
3. **Given** a day within the next 7 days is a public holiday in that location's country, **When** viewing the strip, **Then** that day is also visually marked as non-working

---

### User Story 4 - Manage the Set of Location Tiles (Priority: P3)

A user adds a new location (up to the 8-tile maximum) or removes a location that is no longer relevant to their team.

**Why this priority**: The set of locations changes infrequently. This is important for initial setup and occasional team restructuring, but not daily use.

**Independent Test**: Starting with 7 tiles, add an 8th. Verify it appears. Attempt to add a 9th and verify the widget prevents it with a clear message. Remove one tile and verify it is gone.

**Acceptance Scenarios**:

1. **Given** fewer than 8 tiles are configured, **When** the user adds a new tile, **Then** it appears in the next available slot
2. **Given** 8 tiles are already configured, **When** the user attempts to add a ninth, **Then** a clear message explains the maximum has been reached
3. **Given** the user removes a tile, **When** returning to the dashboard, **Then** the tile is no longer shown and the remaining tiles re-arrange cleanly

---

### Edge Cases

- What happens when weather data is unavailable for a location? → Tile displays last known weather with a visual staleness indicator; if no data ever loaded, shows a "weather unavailable" placeholder
- What happens when public holiday data is unavailable for a country? → Calendar strip shows weekends only; RAG indicator ignores holiday factor and notes data is incomplete
- What happens when the widget has no internet connection? → Last known data is shown with a timestamp of last successful refresh; time continues to update locally from the device clock
- What if a location is in a timezone with a half-hour or quarter-hour UTC offset (e.g., India, Nepal)? → The time display and RAG logic must handle non-whole-hour offsets correctly
- What if two tiles are configured for the same city? → Both are permitted; no deduplication enforced
- What if the user's device clock or timezone is incorrect? → The widget relies on the device clock; no independent time synchronisation is in scope

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Widget MUST display between 1 and 8 configurable location tiles
- **FR-002**: Each tile MUST display the current local time for its configured timezone, refreshing at least once per minute
- **FR-003**: Each tile MUST display current weather conditions and temperature in both Celsius and Fahrenheit simultaneously
- **FR-004**: Each tile MUST display an exchange rate for the tile's configured currency relative to a user-configurable global base currency, which defaults to the currency of the user's system locale on first launch
- **FR-005**: Each tile MUST display a Red/Amber/Green working hours indicator: Green = within configured working hours on a working day; Amber = within 60 minutes before or after the start/end of working hours on a working day; Red = outside working hours or a public holiday
- **FR-006**: Public holidays MUST be factored into the RAG indicator and the 7-day non-working day strip
- **FR-007**: Each tile MUST show a 7-day visual strip, starting from today, marking each day as working or non-working (weekend or public holiday)
- **FR-008**: Clicking any tile MUST open an edit panel exposing: city/location name, timezone, currency code, working hours start time, working hours end time, and working days of the week
- **FR-009**: All tile configuration MUST persist across widget restarts
- **FR-010**: Widget MUST gracefully handle loss of internet connectivity, displaying last known data with a visual staleness indicator
- **FR-011**: Weather and currency data MUST refresh automatically at a regular interval when the widget is open and connected
- **FR-012**: A tile MUST be removable from the dashboard via the edit panel

### Key Entities

- **Location Tile**: city/display name, IANA timezone identifier, ISO 4217 currency code, working hours start time, working hours end time, working days (subset of Mon–Sun), display order
- **Weather Snapshot**: temperature in Celsius, temperature in Fahrenheit, condition description, last fetched timestamp
- **Currency Rate**: exchange rate value, base currency code, quote currency code, last fetched timestamp
- **Public Holiday**: date, name, applicable country/region code
- **Non-Working Day Strip**: ordered 7-day sequence (today + 6), each day flagged as working or non-working with reason (weekend / public holiday / working day)

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A user with all 8 tiles configured can read the time, weather, currency, and RAG status for every location without scrolling or additional interaction
- **SC-002**: The RAG working hours indicator accurately reflects public holidays for all supported countries, verified against a reference holiday dataset
- **SC-003**: Weather and currency data displayed is no more than 15 minutes stale when the device is connected
- **SC-004**: A user can add, configure, and save a new location tile in under 60 seconds
- **SC-005**: The 7-day non-working day strip correctly identifies weekends and public holidays for all supported countries, with zero errors on known holiday dates
- **SC-006**: The widget remains functional (displaying last known data with a staleness indicator) for at least 24 hours without an internet connection
- **SC-007**: Local time display is accurate for timezones with non-whole-hour UTC offsets (e.g., IST UTC+5:30, NPT UTC+5:45)

## Assumptions

- The widget targets the Windows 11 Widget Board (the native sidebar panel accessible from the taskbar)
- Public holiday data covers the countries corresponding to the cities users are likely to configure; coverage for obscure territories may be incomplete
- Currency exchange rates are mid-market indicative rates, not live trading or transactional rates
- The global base currency defaults to the currency detected from the user's system locale on first launch and can be changed in widget settings at any time
- Working days default to Monday–Friday and working hours default to 09:00–17:00 local time when a new tile is first created
- The widget is single-user; there is no shared or synchronised configuration across devices or team members
- Weather data is sourced from a publicly available weather service; the specific provider is an implementation decision
- The widget has no authentication or login requirement; it operates entirely on the local device
- Mobile and web versions of the widget are out of scope
