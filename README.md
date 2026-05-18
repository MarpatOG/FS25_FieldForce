# FieldForce

FieldForce adds telemetry-driven force feedback for Farming Simulator 25. It has two parts:

- **FieldForce Telemetry**: an FS25 Lua mod that reads the active vehicle state and writes telemetry JSON.
- **FieldForce App**: a Windows desktop app that receives that telemetry and drives DirectInput force feedback effects.

The mod does not change vehicle physics or steering. The Windows app calculates wheel forces from telemetry such as speed, steering, RPM, surface, slip, load, suspension impacts, collisions, terrain, and drivetrain events.

## Requirements

- Windows 10/11.
- Farming Simulator 25.
- DirectInput force feedback wheel.
- .NET 8 SDK when building from source.

Built-in wheel profiles cover Logitech MOMO, Driving Force GT/Pro/EX, G25, G27, G29, G920, and G923. Unknown wheels use a conservative `Generic FFB Wheel` fallback.

## Build And Run

```powershell
dotnet restore FieldForce.sln
dotnet build FieldForce.sln
dotnet run --project windows-app/src/App/FieldForce.App.csproj
```

## Install FieldForce Telemetry

Build the FS25 mod zip with `modDesc.xml` at the archive root:

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
```

Copy `artifacts/FS25_FieldForceTelemetry.zip` to:

```text
Documents/My Games/FarmingSimulator2025/mods/
```

If FS25 uses a OneDrive profile, use the `mods` folder beside the active FS25 `log.txt`. Keep only the zip in `mods`; remove any unpacked `FS25_FieldForceTelemetry/` folder so FS25 does not load a stale copy.

Start FS25, enable **FieldForce Telemetry** in the savegame, and enter a vehicle.

## First Test

1. Turn native FS25 force feedback and wheel-driver force feedback down or off before the first FieldForce test.
2. Start **FieldForce App**.
3. Open `Device`, press `Scan`, and select the wheel.
4. Keep initial limits conservative: about 40% global and 35% device.
5. Test `Spring`, `Damper`, `Constant Left`, `Constant Right`, and `Vibration` one at a time.
6. Press `Emergency Stop` or `Ctrl+Alt+Pause` if anything feels wrong.

## Telemetry Flow

FieldForce Telemetry writes file telemetry at 60 Hz by default:

```text
%USERPROFILE%/Documents/My Games/FarmingSimulator2025/modSettings/FS25_FieldForceTelemetry/telemetry.json
```

FieldForce App watches that file by default. If FS25 uses another profile folder, open the app's `Telemetry` tab, press `Choose folder`, and select `Documents`, `My Games`, `FarmingSimulator2025`, `modSettings`, or `FS25_FieldForceTelemetry`.

The current telemetry protocol name is `FIELDFORCE_TELEMETRY`. The app still accepts legacy `FS25_REAL_FFB_TELEMETRY` packets and the old `FS25_RealFfbTelemetry` telemetry folder so existing installs can be migrated.

## Data Locations

- Config: `%APPDATA%/FieldForce/config.json`
- Logs: `%LOCALAPPDATA%/FieldForce/logs/fieldforce-.log`
- User effect profiles: `%APPDATA%/FieldForce/effect-profiles/<wheel-profile-id>.json`
- Effect status file: `Documents/My Games/FarmingSimulator2025/modSettings/FS25_FieldForceTelemetry/effectStatus.json`

The app can load old `%APPDATA%/FS25FFBBridge` config/effect profiles and save migrated copies under `%APPDATA%/FieldForce`.

## Repository Layout

```text
docs/
fs25-mod/
samples/
windows-app/
```

More detail:

- `docs/fs25-mod-installation.md`: FS25 mod packaging and install notes.
- `docs/telemetry-protocol.md`: telemetry packet contract.
- `docs/gameplay-ffb-mechanics.md`: force feedback calculation notes.
- `windows-app/README.md`: app-specific run notes.
