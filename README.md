# EVE-Preview

A Windows x64 multi-client preview and window-control utility for EVE Online. It shows unmodified live thumbnails of configured EVE clients, switches between them, and saves or restores client window layouts.

[中文说明](README-cn.md)

## Project status

- **License:** [Unlicense](LICENSE) — public domain. You may copy, modify, redistribute, or sell this software without permission or attribution.
- **AI-generated:** This project was developed with substantial AI-generated code and documentation. Review and test it before relying on it in critical environments.
- **Forks are welcome:** You may fork, modify, and redistribute this project under the Unlicense. No further permission is required.

## Features

- Process-name monitoring, multi-client discovery, title tracking, and deterministic cleanup.
- Live DWM thumbnails with an optional GDI fallback.
- Physical-pixel thumbnail sizes, opacity, always-on-top, position lock, grid/edge snapping, hover zoom, active-client highlighting, and privacy masking.
- Per-client alias, individual global hotkey, cycle exclusion, privacy mask, and preview visibility.
- Per-monitor DPI handling, including mixed-DPI multi-monitor placement and center-point monitor selection.
- Save and apply EVE client window positions and sizes, individually or in bulk, with an optional startup auto-restore toggle.
- Dynamically sized cycle groups with global, group, and per-client hotkeys.
- Dark WPF control interface, tray integration, pause/resume, atomic configuration saves, and automatic invalid-configuration repair.
- English and Simplified Chinese UI.

## Interaction boundaries

EVE-Preview only reads the configured client process IDs, main window handles, titles, and window bounds, and displays the unmodified DWM/GDI surface. It does **not** modify the EVE UI or game files, inject DLLs, read or write game memory, hook rendering or networking, send keyboard or mouse input, or automate gameplay.

The only writes to EVE clients are permitted window-management operations: bring to foreground, restore, move, resize, minimize, and maximize.

## Requirements

- Windows 10 1809 (10.0.17763) x64 or later.
- .NET 10 SDK to build from source.
- Published builds are self-contained; the target machine does not need .NET installed.

## Download

Download the latest builds from the [Releases](https://github.com/extrame911/EVE-Preview/releases) page.

Each release provides two archives:

- `EVE-Preview-folder.zip` — executable and runtime dependencies.
- `EVE-Preview-single.zip` — self-contained single executable.

Both are self-contained and run as `EVE-Preview.exe`. Verify the archives with `SHA256SUMS.txt`.

## Build and test

```powershell
dotnet restore EVE-Preview.sln
dotnet build EVE-Preview.sln -c Release -warnaserror
dotnet test EVE-Preview.sln -c Release
```

Publish the self-contained folder build:

```powershell
dotnet publish src/EVE-Preview.App/EVE-Preview.App.csproj `
  -c Release -r win-x64 --self-contained true `
  -p:PublishSingleFile=false -o dist/folder
```

Publish the self-contained single-file build:

```powershell
dotnet publish src/EVE-Preview.App/EVE-Preview.App.csproj `
  -c Release -r win-x64 --self-contained true `
  -p:PublishSingleFile=true `
  -p:IncludeNativeLibrariesForSelfExtract=true `
  -p:EnableCompressionInSingleFile=true -o dist/single
```

## Mock client

`mock/EVE-Preview.Mock` builds as `ExeFile.exe` and displays a dynamic clock. It is used only to verify preview rendering and permitted window operations. Start the mock first, then EVE-Preview:

```powershell
dotnet build mock/EVE-Preview.Mock/EVE-Preview.Mock.csproj -c Debug
start mock/EVE-Preview.Mock/bin/Debug/net10.0-windows10.0.17763.0/ExeFile.exe
start src/EVE-Preview.App/bin/Debug/net10.0-windows10.0.17763.0/win-x64/EVE-Preview.exe
```

## Configuration

The default configuration file is `EVE-Preview.json` next to the executable. Normal settings are saved after a 500 ms debounce; the latest layout is also saved on exit. Saves use a temporary file, `.bak` backup, and atomic replacement.

Invalid configuration is repaired per top-level module and written back automatically; the original file is preserved as `.corrupt-*.bak`. Invalid client entries are dropped or downgraded individually so other client settings remain intact.

## Project structure

```text
src/EVE-Preview.Core/
  Configuration/       Configuration model, repair, atomic storage, legacy import
  Clients/             Client identity, titles, snapshots, process-name handling
  Layout/              Grid snapping and layout constraints
  Hotkeys/             Hotkey parsing
  CycleGroups/         Cycle-group model
src/EVE-Preview.Windows/
  Hotkeys/  Previews/  Processes/  WindowManagement/
src/EVE-Preview.App/
  Services/Runtime/          Preview runtime and lifecycle/polling/persistence slices
  Services/Previews/         Preview windows, overlays, DPI, monitor and snap geometry
  Services/Localization/     Localization and visual text refresh
  Services/Infrastructure/   Tray, single instance, profiles, fonts, build info
  ViewModels/Shell/          Main window, overview and page state
  ViewModels/Clients/        Client list and properties
  ViewModels/CycleGroups/    Cycle groups
  ViewModels/Settings/       System, thumbnail and hotkey settings
  Views/                     XAML pages (stable paths for pack URIs)
  Themes/                    Palette, controls, icons, merged theme entry
tests/
  EVE-Preview.Core.Tests/
  EVE-Preview.Windows.Tests/
  EVE-Preview.App.Tests/
```

Detailed design and behavior constraints are documented in [DESIGN.md](DESIGN.md).

## License

This is free and unencumbered software released into the public domain under the [Unlicense](LICENSE).

Anyone is free to copy, modify, publish, use, compile, sell, or distribute this software, either in source code form or as a compiled binary, for any purpose, commercial or non-commercial, and by any means.

## Forking and contributions

Forks, modifications, derivative projects, and pull requests are welcome. Under the Unlicense, you do not need permission or attribution to publish a modified version, but keeping a link to the original project is appreciated.
