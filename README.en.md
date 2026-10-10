**English** | [日本語](README.ja.md) | [한국어](README.md)

> This document was translated by AI. The Korean version ([README.md](README.md)) is the original.

# GodotXOPS

A port of the Japanese indie FPS **XOPS** (X operations, 2000) to the Godot engine. It is based on the open-source implementation [OpenXOPS](https://openxops.net/) and its Unity port, [UnityXOPS](https://github.com/dlwowlsgod/UnityXOPS).

- Engine: Godot 4.7.2 (.NET)
- Platform: Windows
- Latest release: [1.1.0](https://github.com/jejusbluesea/GodotXOPS/releases/tag/v1.1.0)

The goal is to reproduce the feel of the original. Movement, collision, and bullet hit detection do not use the engine's physics; the original calculations were ported directly, and the game runs at the same 33.33 ticks per second as the original.

## Features

- Play the original missions and addon missions (opening → menu → briefing → game → result)
- The original AI, mission events, and mission complete / failure checks
- Key bindings, resolution, brightness and gamma, crosshair shape, and volume in the menu's OPTION screen
- Human, weapon, small object, and effect values editable through JSON files in `godotdata/`
- Multiple addon folders registered as separate pages through `addon.json`
- Extended file formats (BD2, PD2, MIF2): no limits on the number of textures, point numbers, or event lines, and a mission can bring its own humans, weapons, small objects, effects, materials, and sounds. The original formats (BD1, PD1, MIF) are still supported
- Script events: in addition to the ten original events, 36 more are included (variables and branches, spawning, on-screen text, moving small objects and blocks, playing sounds, and so on), and you can add your own event packs. Scripts only run inside a sandbox
- Screen scripts: the opening, menu, briefing, in-game screen (HUD), result, and option screens can each be redrawn by a script. The built-in screens stay as they are and only the screens you register are replaced; samples that rewrite all six built-in screens are included. Scripts only run inside a sandbox
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
| Switch fire mode (semi / full auto etc., previous / next) | Z / X |
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

## Addon pages

The `addon` folder is the first page of the addon list by default. To register more folders, write their paths and page names in `addon.json`, with the same number of entries in both lists. Paths are relative to the folder that contains `GodotXOPS.exe`.

```json
{
    "addonPath" : [
        "addon_pack1",
        "addon_pack2"
    ],
    "addonName" : [
        "Pack 1",
        "Pack 2"
    ]
}
```

## Documentation

The documents are written in Korean.

- [Modding guide](docs/modding.md) — how to edit weapons, humans, small objects, effects, and missions through the `godotdata/` JSON files, the extended file formats, script events, screen scripts, and addon pages
- [Development guide](docs/development.md) — building from source, code structure, check tools, the editor, the debug console, and behavior that differs from the original

## Differences from the original

This project tries to reproduce the original as closely as possible, using OpenXOPS as the reference. However, OpenXOPS itself is not an exact reproduction of the original, so some things may feel different from the original XOPS. If you find a difference, please report it in the [issues](https://github.com/jejusbluesea/GodotXOPS/issues).

## Roadmap

The port was completed with 1.0.0, and 1.1.0 added the extended file formats, script events, and the editor, and 1.2.0 added screen scripts. What goes into the next version will be written in the [roadmap](ROADMAP.md) (in Korean) once it is decided.

## License and notices

- The code in this repository is under the [MIT License](LICENSE).
- The XOPS assets (`data`, `addon`) belong to their original authors and are not included in this repository.
- Made with [Godot Engine](https://godotengine.org) (MIT). Scripts (events and screens) run on [Godot Sandbox](https://github.com/libriscv/godot-sandbox) 0.60 (Alf-André Walla, BSD-3-Clause). The full license texts are in `THIRD_PARTY_NOTICES.txt` in the release files ([the copy in this repository](dist/THIRD_PARTY_NOTICES.txt)).
- AI was used for coding and translation. The 2D, 3D, and sound assets are not AI-generated.

## Credits

- XOPS — nine-two
- [OpenXOPS](https://openxops.net/) — OpenXOPS Project
- [UnityXOPS](https://github.com/dlwowlsgod/UnityXOPS)
