# Comprehensive Sailing Assistant

Bilingual Chinese/English in-game sailing assistant for **The Pirate: Caribbean Hunt**.

## Repository contents

- `src/SailingAssistant.cs` — extension implementation and in-game IMGUI panel.
- `src/Build.ps1` — build script for the installed game directory.

## Dependency

This source requires **Pirate Framework 1.4.0 or later**. Build and install the framework first so that `UserLibs/PirateModAPI.dll` exists.

## Build

From the game root:

```powershell
powershell -ExecutionPolicy Bypass -File .\src\Build.ps1
```

The script outputs `Mods/PirateExtensions/SailingAssistant.dll`.

## Features

- Current-area player and AI ship filters.
- Official localized ship and cargo names.
- Hull, sails, crew, cannon count, speed and anchor status.
- Player cargo and port warehouse information.
- Chinese/English language switch with saved preference.

## Runtime requirements

- The Pirate: Caribbean Hunt 10.2.9 (tested)
- MelonLoader 0.7.3 Mono x64
- Pirate Framework 1.4.0 or later

## License

Add your preferred license before publishing.

