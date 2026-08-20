# Map Tab Customizer

A RimWorld 1.6 mod that adds a persistent custom label and selectable icon to every map tab.

## Features

- Custom text and one of five built-in icons for each map.
- Persistent settings stored with the map in the save file.
- Optional labels that appear only while hovering.
- Optional label that remains visible for the active map.
- Optional compact, fixed-size map tabs whose width does not depend on pawn count.
- Optional native pawn portraits for the active map while inactive maps remain compact.
- Left-click to switch maps and right-click to customize a tab in compact mode.
- English and French translations.
- Automatic compatibility with `[LTO] Colony Groups`, including its native group controls.
- Options to hide all LTO group controls or show only those belonging to the active map.

## Installation

Download or clone the repository into RimWorld's `Mods` folder, then enable **Map Tab Customizer** in the mod list. The compiled assembly is included under `Assemblies/`.

RimWorld 1.6 and Harmony are required.

## Usage

Right-click a map's colonist group, choose a label and icon, then save. The customization follows the map in the save file.

The mod settings can hide labels until hovered or replace pawn portraits with compact, fixed-size map tabs. With `[LTO] Colony Groups`, its native group controls remain available.

## Building

```powershell
dotnet build .\Source\MapTabCustomizer\MapTabCustomizer.csproj -c Release
```

If Harmony is installed elsewhere, pass `-p:HarmonyPath=C:\path\to\0Harmony.dll`.
