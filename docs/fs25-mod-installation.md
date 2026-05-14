# FS25 Mod Installation

## Install Zip

Use this zip package:

```text
artifacts/FS25_RealFfbTelemetry.zip
```

Copy it to:

```text
Documents/My Games/FarmingSimulator2025/mods/FS25_RealFfbTelemetry.zip
```

If Documents is redirected, the active FS25 profile can be under OneDrive instead, for example:

```text
%USERPROFILE%/OneDrive/Документы/My Games/FarmingSimulator2025/mods/FS25_RealFfbTelemetry.zip
```

Use the `mods` directory beside the active FS25 `log.txt`.

The archive contents must be:

```text
FS25_RealFfbTelemetry.zip
  modDesc.xml
  VERSION.md
  config/
    TelemetryConfig.lua
  src/
    FS25RealFfbTelemetry.lua
```

Do not zip the parent repository folder itself. `modDesc.xml` must be directly at the archive root.

## Update Existing Install

Close FS25 first, then rebuild and replace the zip:

```powershell
$artifact = "artifacts/FS25_RealFfbTelemetry.zip"
Remove-Item -Force $artifact -ErrorAction SilentlyContinue
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem
$source = (Resolve-Path fs25-mod).Path
$zip = [System.IO.Compression.ZipArchive]::new(
    [System.IO.File]::Open($artifact, [System.IO.FileMode]::CreateNew),
    [System.IO.Compression.ZipArchiveMode]::Create)
try {
    Get-ChildItem $source -Recurse -File | ForEach-Object {
        $entry = $_.FullName.Substring($source.Length + 1).Replace('\', '/')
        [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip, $_.FullName, $entry) | Out-Null
    }
} finally {
    $zip.Dispose()
}
Copy-Item -Force artifacts/FS25_RealFfbTelemetry.zip "$env:USERPROFILE\Documents\My Games\FarmingSimulator2025\mods\FS25_RealFfbTelemetry.zip"
Remove-Item -Recurse -Force "$env:USERPROFILE\Documents\My Games\FarmingSimulator2025\mods\FS25_RealFfbTelemetry" -ErrorAction SilentlyContinue
```

Do not use `Compress-Archive`; FS25 expects forward slash paths inside the zip and will fail to load source files from backslash entries.

Keep only one installed copy. If both `FS25_RealFfbTelemetry.zip` and `FS25_RealFfbTelemetry/` exist in the FS25 `mods` directory, FS25 can show or load a stale version.

## Configure

Default config is in `config/TelemetryConfig.lua`:

```lua
FS25RealFfbTelemetryConfig = {
    transport = "file",
    host = "127.0.0.1",
    port = 34325,
    fileTelemetryRateHz = 60,
    udpTelemetryRateHz = 60,
    debug = false,
    fileName = "telemetry.json",
    overlay = {
        enabled = false,
        x = 0.015,
        y = 0.965,
        fontSize = 0.014,
        lineHeight = 0.018,
        maxRawLength = 120
    }
}
```

Keep `debug=false` for normal use.
Set `debug=true` only while diagnosing transport issues; the mod will then print sent UDP/file payloads and extra file write details.
Use `Settings` -> `General Settings` -> `FS25 Real FFB` -> `Telemetry overlay` to turn the in-game debug overlay on or off. `overlay.enabled` in `TelemetryConfig.lua` remains the default before a saved menu override exists.

## Run

1. Launch the Windows app.
2. Open the `Telemetry` tab and confirm it shows `Waiting`.
3. Launch FS25.
4. Enable `FS25 Real FFB Telemetry` in the savegame.
5. Enter a vehicle.
6. Confirm the Windows app changes to `Connected`.

By default, telemetry is written to the file transport at `60 Hz`. Supported file rates are `1`, `10`, `30`, and `60 Hz`; unsupported values fall back to `60 Hz`.
UDP remains available as a hidden diagnostic transport through `transport = "udp"` or `transport = "file+udp"`:

```text
Documents/My Games/FarmingSimulator2025/modSettings/FS25_RealFfbTelemetry/telemetry.json
```

If file telemetry fails, check the FS25 log for the exact missing API or filesystem failure, such as unavailable `io.open`, unavailable `getUserProfileAppPath`, folder creation failure, or atomic rename failure.
