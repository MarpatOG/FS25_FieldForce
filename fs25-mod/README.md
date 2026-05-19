# FieldForce Telemetry Mod

Milestone 2 telemetry sender for Farming Simulator 25.

## Install

Package this `fs25-mod` folder as:

```text
FS25_FieldForceTelemetry.zip
```

Place that zip in:

```text
Documents/My Games/FarmingSimulator2025/mods/
```

If Windows redirects Documents to OneDrive, place it in the OneDrive FS25 profile instead, for example:

```text
%USERPROFILE%/OneDrive/Документы/My Games/FarmingSimulator2025/mods/
```

Use the `mods` directory beside the active FS25 `log.txt`.

The zip contents should have `modDesc.xml` at the archive root:

```text
FS25_FieldForceTelemetry.zip
  modDesc.xml
  VERSION.md
  config/
  src/
```

Enable `FieldForce Telemetry` in a savegame. Start FieldForce App before or after FS25; if the app is not running, the mod must not crash the game.

The in-game telemetry overlay can be toggled from `Settings` -> `General Settings` -> `FieldForce` -> `Telemetry overlay`.

## Update Installed Mod

Rebuild the zip after any shipped Lua/config/version change:

```powershell
.\scripts\Build-Artifacts.ps1
Copy-Item -Force artifacts/FS25_FieldForceTelemetry.zip "$env:USERPROFILE\Documents\My Games\FarmingSimulator2025\mods\FS25_FieldForceTelemetry.zip"
Remove-Item -Recurse -Force "$env:USERPROFILE\Documents\My Games\FarmingSimulator2025\mods\FS25_FieldForceTelemetry" -ErrorAction SilentlyContinue
```

The build script avoids `Compress-Archive`; FS25 expects forward slash paths inside the zip.

Keep only the zip in the FS25 `mods` directory. A stale folder copy can make FS25 display or load an older mod version.

## Versioning

The current mod version and bump rules are tracked in [VERSION.md](VERSION.md). Any shipped change under `fs25-mod/` must keep that file and `modDesc.xml` in sync, unless the commit message explicitly explains why the mod package is unaffected.

## Config

Edit `config/TelemetryConfig.lua`:

- `host`: default `127.0.0.1`
- `port`: default `34325`
- `transport`: default `file`; hidden diagnostic values are `udp` and `file+udp`
- `fileTelemetryRateHz`: default `60`; allowed values are `1`, `10`, `30`, `60`
- `udpTelemetryRateHz`: default `60`; allowed values are `1`, `10`, `30`, `60`
- `debug`: default `false`
- `overlay.enabled`: default `false`; default for the in-game telemetry debug overlay before a saved menu override exists
- `overlay.x`, `overlay.y`: screen-space overlay position, where `0,0` is bottom-left
- `overlay.fontSize`, `overlay.lineHeight`: text sizing
- `overlay.maxRawLength`: max raw JSON characters shown in the overlay

## Transport Diagnostics

The mod writes file telemetry by default. UDP is still available as a hidden diagnostic transport through `transport = "udp"` or `transport = "file+udp"`.

```text
Documents/My Games/FarmingSimulator2025/modSettings/FS25_FieldForceTelemetry/telemetry.json
```

If file telemetry cannot start, the log reports the exact missing API or filesystem step, such as `io.open`, `getUserProfileAppPath`, `createFolder`, or `os.rename`.
