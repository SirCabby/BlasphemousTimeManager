# Blasphemous Time Manager

A BepInEx plugin to view, freeze, and set the in-game total **play time**
(otherwise only visible on the save-slot screen).

## How the timer works (discovered)

Blasphemous does not store the running total directly. On
`Framework.Managers.PersistentManager` it keeps:

- `TimeStored`     — seconds accumulated up to the last save ("store")
- `LastTimeStored` — `Time.realtimeSinceStartup` captured at that save

and computes the live total as:

```
total = TimeStored + (Time.realtimeSinceStartup - LastTimeStored)
```

(wall-clock, counts menus/pauses). That base + (now - mark) split is why a plain
Cheat Engine value scan never found it.

- **Freeze**: each frame set `TimeStored = frozen`, `LastTimeStored = now` -> total held.
- **Set**:    `TimeStored = value`, `LastTimeStored = now` -> shows immediately and persists at the next save.

## Build & install

- Game: Steam Blasphemous, Unity 2017.4 (**Mono CLR 2.0**) -> plugin targets **net35**.
- Requires BepInEx 5.x (x64, Mono) installed in the game folder.
- Copy `GameDir.local.props.example` to `GameDir.local.props` and set `<GameDir>`
  to your install (this file is gitignored), then `dotnet build -c Release`.
- Copy `bin/Release/BlasTimeManager.dll` to `Blasphemous/BepInEx/plugins/`.

## Controls (rebind in `BepInEx/config/local.blasphemous.timemanager.cfg`)

The overlay (`Play time  HH:MM:SS`, top-left) shows only during gameplay.

- **F7** — toggle the overlay.
- **F8** — freeze / unfreeze the timer (overlay turns cyan + `[FROZEN]`).
- **F9** — open the (opaque) input box; type `HH:MM:SS` (or plain seconds).
  **Enter** applies, **Esc** cancels — keyboard only, no mouse needed.
  Rest at a Prie Dieu to persist the new value.
