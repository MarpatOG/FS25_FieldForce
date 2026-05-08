# FS25 Real FFB Telemetry Mod

Milestone 2 telemetry sender for Farming Simulator 25.

## Install

Package this `fs25-mod` folder as:

```text
FS25_RealFfbTelemetry.zip
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
FS25_RealFfbTelemetry.zip
  modDesc.xml
  VERSION.md
  config/
  src/
```

Enable `FS25 Real FFB Telemetry` in a savegame. Start the Windows bridge before or after FS25; if the bridge is not running, the mod must not crash the game.

## Update Installed Mod

Rebuild the zip after any shipped Lua/config/version change:

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

Do not use `Compress-Archive`; FS25 expects forward slash paths inside the zip.

Keep only the zip in the FS25 `mods` directory. A stale folder copy can make FS25 display or load an older mod version.

## Versioning

The current mod version and bump rules are tracked in [VERSION.md](VERSION.md). Any shipped change under `fs25-mod/` must keep that file and `modDesc.xml` in sync, unless the commit message explicitly explains why the mod package is unaffected.

## Config

Edit `config/TelemetryConfig.lua`:

- `host`: default `127.0.0.1`
- `port`: default `34325`
- `updateRateHz`: default `125` for UDP telemetry
- `fileFallbackRateHz`: default `30` for diagnostic/compatibility file fallback writes
- `debug`: default `false`
- `overlay.enabled`: default `true`; draws the in-game telemetry debug overlay
- `overlay.x`, `overlay.y`: screen-space overlay position, where `0,0` is bottom-left
- `overlay.fontSize`, `overlay.lineHeight`: text sizing
- `overlay.maxRawLength`: max raw JSON characters shown in the overlay

## Transport Diagnostics

The mod tries UDP first through `require("socket")`. If FS25 does not expose LuaSocket, the log includes the `require` error plus `package.path` and `package.cpath`, then the mod tries the file fallback.

File fallback writes the same JSON packet to the fallback file at `fileFallbackRateHz`; UDP remains the primary transport when LuaSocket is available.

```text
Documents/My Games/FarmingSimulator2025/modSettings/FS25_RealFfbTelemetry/telemetry.json
```

If file fallback cannot start, the log reports the exact missing API or filesystem step, such as `io.open`, `getUserProfileAppPath`, `createFolder`, or `os.rename`.
