**English** | [日本語](README.ja.md) | [한국어](README.md)

> This document was translated by AI. The Korean version ([README.md](README.md)) is the original.

# GodotXOPS

A port of the Japanese indie FPS **XOPS** (X operations, 2000) to the Godot engine. It is based on the open-source implementation [OpenXOPS](https://openxops.net/) and its Unity port, [UnityXOPS](https://github.com/dlwowlsgod/UnityXOPS).

- Engine: Godot 4.7.2 (.NET)
- Platform: Windows
- Latest release: [1.2.5](https://github.com/jejusbluesea/GodotXOPS/releases/tag/v1.2.5)

The goal is to reproduce the feel of the original. Movement, collision, and bullet hit detection do not use the engine's physics; the original calculations were ported directly, and the game runs at the same 33.33 ticks per second as the original.

## Features

- Play the original missions and addon missions (opening → menu → briefing → game → result)
- The original AI, mission events, and mission complete / failure checks
- Key bindings, resolution, brightness and gamma, fog and draw distance, crosshair shape, and volume in the menu's OPTION screen. The keys of buttons added by add-ons (custom bindings) are changed there too
- Human, weapon, small object, and effect values editable through JSON files in `godotdata/`
- Things the original does not have can be made with data alone: armor and helmets, health regeneration, melee weapons, weapons loaded one round at a time, a grenade launcher attached to a gun, and the range and order in which the AI uses weapons. The default data does not use them, so the original missions play as before
- Map packs: several missions and the data they share (humans, weapons, sky, events, materials and more) bundled by one manifest; registering it in `addon.json` makes it one addon page in the menu
- Extended file formats (BD2, PD2, MIF2): no limits on the number of textures, point numbers, or event lines, and map packs and missions can bring their own humans, weapons, small objects, sounds, and images (map packs also sky, events, materials, and effects). The original formats (BD1, PD1, MIF) are still supported
- Script events: in addition to the ten original events, more are included (variables and branches, spawning, on-screen text, moving small objects and blocks, playing sounds, putting images on the screen, cutscene staging such as the camera, fades, letterbox and pausing the game, and so on), and you can add your own event packs. Scripts only run inside a sandbox
- Screen scripts: the opening, menu, briefing, in-game screen (HUD), result, and option screens can each be redrawn by a script. The built-in screens stay as they are and only the screens you register are replaced; samples that rewrite all six built-in screens are included. Missions and map packs can also bring their own briefing, HUD, and result screens and font, and a map pack can replace the title on its page of the menu. Scripts only run inside a sandbox
- [Editor](#editor): edit blocks, points and events, missions, and data, and play-test right away
- Debug console (set `AllowConsole` to `"true"` in `godotdata/config.json`, then press F11)

## Installation

The original XOPS assets (the `data` folder) are not included in this repository or in the release files for copyright reasons. You need to take them from the original XOPS yourself.

1. Download `GodotXOPS_x.y.z.7z` from the [releases page](https://github.com/jejusbluesea/GodotXOPS/releases) and extract it.
2. Copy the `data` folder of the original XOPS into the folder that contains `GodotXOPS.exe`.
3. To play addon missions, put the `addon` folder in the same place.
4. Run `GodotXOPS.exe`.

The folder should look like this.

```
GodotXOPS/
├─ GodotXOPS.exe
├─ GodotXOPS_Editor.bat             (starts the editor)
├─ GodotXOPS.pck
├─ libgodot_riscv.windows.template_release.x86_64.dll   (runs the scripts)
├─ data_GodotXOPS_windows_x86_64/   (.NET runtime, not game data)
├─ godotdata/                       (settings and game value JSON)
├─ addon.json
├─ LICENSE.txt
├─ THIRD_PARTY_NOTICES.txt
├─ data/                            (copied from the original XOPS)
└─ addon/                           (optional)
```

## Controls

These are the defaults. You can change them in OPTION → Input in the menu.

| Action | Key |
|---|---|
| Move | W / A / S / D |
| Look | Mouse (arrow keys also work) |
| Fire | Left mouse button |
| Jump | Space |
| Walk | Tab |
| Reload | R |
| Scope | Left Shift |
| Weapon slot 1 / 2 | 1 / 2 |
| Switch weapon type (semi / full auto, a grenade launcher attached to a gun, etc. Previous / next) | Z / X |
| Drop weapon | G |
| Interact (only when a mission's events use it) | F |
| Switch view (first / third person) | F1 |
| HUD display mode | F2 |
| Restart mission | F12 |
| Back to menu | ESC |

The original cheat keys (F5 to F9) are also available.

## Editor

Run `GodotXOPS_Editor.bat` (the same as `GodotXOPS.exe -- --scene editor`). It creates and edits extended-format blocks (BD2), points and events (PD2), missions (MIF2), and data (JSON). Maps and missions in the original formats are converted to the extended formats through File → Import.

- The controls follow Blender's defaults: middle mouse button for the view, left click to select, G / R / S to move, rotate, and scale. The same functions are also in the menus and buttons.
- F5 play-tests the current content right away, and Esc returns to the editor.
- The full list of controls is in the [editor section of the development guide](docs/development.md#에디터) (written in Korean).

## Addon pages and map packs

The `addon` folder is the first page of the addon list by default (in file name order). The pages after it are map packs. To register a map pack, write the path of its manifest file in `addon.json`. Paths are relative to the folder that contains `GodotXOPS.exe`.

```json
{
    "manifests" : [
        "mappacks/vol0/manifest.json"
    ]
}
```

The manifest holds the map pack's name, the order of its missions (MIF2), and the data files the map pack shares. See [map packs in the modding guide](docs/modding.md#맵팩) for details. From 1.2.4 on, the old `addon.json` keys up to 1.2.3 (`addonPath`, `addonName`) are no longer read.

## Documentation

The documents are written in Korean.

- [Modding guide](docs/modding.md) — every key of the `godotdata/` JSON files (weapons, humans, AI, small objects, effects, missions), the extended file formats, map pack and mission addon data, script events, screen scripts, per-mission screens, images and font, and addon pages and map packs
- [Development guide](docs/development.md) — building from source, code structure, check tools, the editor, the debug console, and behavior that differs from the original

## Differences from the original

This project tries to reproduce the original as closely as possible, using OpenXOPS as the reference. However, OpenXOPS itself is not an exact reproduction of the original, so some things may feel different from the original XOPS. If you find a difference, please report it in the [issues](https://github.com/jejusbluesea/GodotXOPS/issues).

## Roadmap

The port was completed with 1.0.0, and 1.1.0 added the extended file formats, script events, and the editor, 1.2.0 added screen scripts, and 1.2.2 added cutscene events, data extensions such as armor and melee weapons, and per-mission screens, 1.2.3 added per-texture materials and UV editing in the editor, 1.2.4 added map packs (manifests) and their screens and fonts, and 1.2.5 fixed UI misalignment on large screens such as 4K. 1.3.0, in progress, adds custom bindings and the fog and draw distance settings. What goes into the next version will be written in the [roadmap](ROADMAP.md) (in Korean) once it is decided.

## License and notices

- The code in this repository is under the [MIT License](LICENSE).
- The XOPS assets (`data`, `addon`) belong to their original authors and are not included in this repository.
- Made with [Godot Engine](https://godotengine.org) (MIT). Scripts (events and screens) run on [Godot Sandbox](https://github.com/libriscv/godot-sandbox) 0.60 (Alf-André Walla, BSD-3-Clause). The full license texts are in `THIRD_PARTY_NOTICES.txt` in the release files ([the copy in this repository](dist/THIRD_PARTY_NOTICES.txt)).
- AI was used for coding and translation. The 2D, 3D, and sound assets are not AI-generated.

## Credits

- XOPS — nine-two
- [OpenXOPS](https://openxops.net/) — OpenXOPS Project
- [UnityXOPS](https://github.com/dlwowlsgod/UnityXOPS)
