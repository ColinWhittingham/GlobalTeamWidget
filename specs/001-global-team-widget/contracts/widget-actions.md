# Contract: Widget Action Verbs

**Type**: Widget provider ↔ Adaptive Cards interaction contract
**Direction**: Adaptive Card → `IWidgetProvider.OnActionInvoked` callback

All interactive elements in the widget dashboard use `Action.Execute`. The `verb` field identifies the action; the provider dispatches on it.

---

## Verbs

### `open-edit`

Triggered when the user clicks a tile body or an explicit "Edit" button on a tile.

**Sent data**:
```json
{
  "verb": "open-edit",
  "tileId": "<guid>"
}
```

**Provider response**: Launches the WinUI 3 `SettingsWindow` scoped to the given `tileId`. No widget card update required immediately; card refreshes after settings window closes.

---

### `remove-tile`

Triggered by a "Remove" button in the tile edit context (surfaced via Adaptive Card action from the settings window save path, or a dedicated card action).

**Sent data**:
```json
{
  "verb": "remove-tile",
  "tileId": "<guid>"
}
```

**Provider response**: Deletes the `LocationTile` from configuration, rebuilds and pushes an updated dashboard card.

---

### `refresh`

Triggered by an optional manual refresh button in the widget dashboard.

**Sent data**:
```json
{
  "verb": "refresh"
}
```

**Provider response**: Triggers an immediate data refresh for all tiles; pushes an updated card on completion.

---

## Notes

- `Action.Submit` is not supported by the Windows 11 Widget Board renderer — all actions must use `Action.Execute`.
- The widget provider must handle unknown verbs gracefully (log and no-op).
- The settings window communicates configuration changes back to the provider via an in-process event or callback, not via an Adaptive Card action.
