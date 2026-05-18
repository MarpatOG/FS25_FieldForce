# FS25 Mod Installation

## Install Zip

Use this zip package:

```text
artifacts/FS25_FieldForceTelemetry.zip
```

Copy it to:

```text
Documents/My Games/FarmingSimulator2025/mods/FS25_FieldForceTelemetry.zip
```

If Documents is redirected, the active FS25 profile can be under OneDrive instead, for example:

```text
%USERPROFILE%/OneDrive/Документы/My Games/FarmingSimulator2025/mods/FS25_FieldForceTelemetry.zip
```

Use the `mods` directory beside the active FS25 `log.txt`.

The archive contents must be:

```text
FS25_FieldForceTelemetry.zip
  modDesc.xml
  VERSION.md
  config/
    TelemetryConfig.lua
  src/
    FieldForceTelemetry.lua
```

Do not zip the parent repository folder itself. `modDesc.xml` must be directly at the archive root.

## Update Existing Install

Close FS25 first, then rebuild and replace the zip:

```powershell
$artifact = "artifacts/FS25_FieldForceTelemetry.zip"
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
Copy-Item -Force artifacts/FS25_FieldForceTelemetry.zip "$env:USERPROFILE\Documents\My Games\FarmingSimulator2025\mods\FS25_FieldForceTelemetry.zip"
Remove-Item -Recurse -Force "$env:USERPROFILE\Documents\My Games\FarmingSimulator2025\mods\FS25_FieldForceTelemetry" -ErrorAction SilentlyContinue
```

Do not use `Compress-Archive`; FS25 expects forward slash paths inside the zip and will fail to load source files from backslash entries.

Keep only one installed copy. If both `FS25_FieldForceTelemetry.zip` and `FS25_FieldForceTelemetry/` exist in the FS25 `mods` directory, FS25 can show or load a stale version.

## Configure

Default config is in `config/TelemetryConfig.lua`:

```lua
FieldForceTelemetryConfig = {
    transport = "file",
    host = "127.0.0.1",
    port = 34325,
    fileTelemetryRateHz = 60,
    udpTelemetryRateHz = 60,
    debug = false,
    fileName = "telemetry.json",
    overlay = {
        enabled = false,
        x = 0.665,
        y = 0.965,
        width = 0.32,
        padding = 0.008,
        fontSize = 0.014,
        lineHeight = 0.018,
        maxRawLength = 120
    }
}
```

Keep `debug=false` for normal use.
Set `debug=true` only while diagnosing transport issues; the mod will then print sent UDP/file payloads and extra file write details.
Use `Settings` -> `General Settings` -> `FieldForce` -> `Telemetry overlay` to turn the in-game debug overlay on or off. `overlay.enabled` in `TelemetryConfig.lua` remains the default before a saved menu override exists.

## Run

1. Launch the Windows app.
2. Open the `Telemetry` tab and confirm it shows `Waiting`.
3. Launch FS25.
4. Enable `FieldForce Telemetry` in the savegame.
5. Enter a vehicle.
6. Confirm the Windows app changes to `Connected`.

By default, telemetry is written to the file transport at `60 Hz`, and the Windows app watches the normal local FS25 profile path. Supported file rates are `1`, `10`, `30`, and `60 Hz`; unsupported values fall back to `60 Hz`.
UDP remains available as a hidden diagnostic transport through `transport = "udp"` or `transport = "file+udp"`:

```text
%USERPROFILE%/Documents/My Games/FarmingSimulator2025/modSettings/FS25_FieldForceTelemetry/telemetry.json
```

If the active FS25 profile is elsewhere, open the app's Telemetry tab and use `Choose folder`. Selecting `Documents`, `My Games`, `FarmingSimulator2025`, `modSettings`, or `FS25_FieldForceTelemetry` is accepted; the app resolves the selected folder to `FS25_FieldForceTelemetry/telemetry.json`.

If file telemetry fails, check the FS25 log for the exact missing API or filesystem failure, such as unavailable `io.open`, unavailable `getUserProfileAppPath`, folder creation failure, or atomic rename failure.
