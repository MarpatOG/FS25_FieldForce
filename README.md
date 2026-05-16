# FS25 Real FFB Bridge

Windows-first force feedback bridge for Farming Simulator 25. MVP scope is complete after the Logitech wheel profile catalog and fully gated Motion layer.

## Current Status

- Windows app: DirectInput FFB tests and gameplay FFB pipeline are implemented.
- FS25 Lua telemetry mod: sends file-based telemetry by default, with hidden UDP diagnostic modes.
- Telemetry receiver: implemented in the Windows app with file, UDP, and file+UDP transport modes.
- Wheel profiles: built-in Logitech gear-driven profiles for MOMO, Driving Force GT/Pro/EX, G25, G27, G29, G920, G923, plus Generic FFB fallback. Profiles are auto-detected from DirectInput aliases and user effect profiles are keyed by stable profile id.
- Gameplay-driven FFB effects: Speed Spring, Speed Damper, Mechanical Friction, Load Resistance, RPM Vibration, Surface Feedback, Slip Feedback, Wetness, Motion, Bump, Suspension Hit, Landing, Collision, Terrain Rumble, and Drivetrain Pulse are implemented with conservative per-wheel caps.
- FS25 overlay: displays a compact vertical diagnostic panel with transmitted telemetry fields and can be toggled from the in-game General Settings page.
- Windows app overlay: displays live effect activation lamps from the actual gameplay FFB output.

## Requirements

- Windows 10/11.
- .NET 8 SDK to build.
- DirectInput force feedback wheel. Logitech gear-driven wheels use built-in profile defaults; unknown wheels use `Generic FFB Wheel`.

## Build and Run

```powershell
dotnet restore
dotnet build
dotnet run --project windows-app/src/App/FS25FfbBridge.App.csproj
```

## Safe First Test

1. Set the wheel driver and FS25 native force feedback to off or very low before using bridge-driven FFB later.
2. Launch the app without FS25.
3. Open `Device`, press `Scan`, and select the wheel.
4. Keep global and device force limits near the defaults: 40% global, 35% device.
5. Test `Spring`, `Damper`, `Constant Left`, `Constant Right`, and `Vibration` one at a time.
6. Press `Emergency Stop` or `Ctrl+Alt+Pause` if anything feels wrong.

## Telemetry Test

1. Run the Windows app.
2. Open `Telemetry`; it should show `Waiting`.
3. Install `artifacts/FS25_RealFfbTelemetry.zip` into `Documents/My Games/FarmingSimulator2025/mods/`.
4. Start FS25 and enable `FS25 Real FFB Telemetry`.
5. Enter a vehicle.
6. The app should change to `Connected` and show vehicle/speed/RPM fields as available.
7. With an acquired FFB wheel and gameplay FFB enabled, the bridge applies the enabled MVP effects from telemetry. It fades or stops them when telemetry is lost or the player leaves the vehicle.

The `Telemetry` tab shows UDP status, file status, last packet source, packet age, parser status, and transport errors separately.
By default, the mod writes telemetry JSON at 60 Hz to:

```text
Documents/My Games/FarmingSimulator2025/modSettings/FS25_RealFfbTelemetry/telemetry.json
```

Manual transport checks are documented in `docs/telemetry-protocol.md`.

The in-game telemetry overlay uses `overlay.enabled` from `fs25-mod/config/TelemetryConfig.lua` as its default. It can be changed in FS25 under `Settings` -> `General Settings` -> `FS25 Real FFB` -> `Telemetry overlay`.

## Mod Versioning

Mandatory rule: any change to files shipped inside `fs25-mod/` must update the FS25 mod version in both `fs25-mod/modDesc.xml` and [fs25-mod/VERSION.md](fs25-mod/VERSION.md), unless the commit message explicitly states why the shipped mod package is unaffected.

## Update FS25 Mod Zip

FS25 should use the zip package, matching the normal mod layout. After changing files under `fs25-mod/`, rebuild and replace the installed zip:

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

Do not use `Compress-Archive` here: it can store Windows backslash paths in the zip, and FS25 then fails to load `config/TelemetryConfig.lua` and `src/FS25RealFfbTelemetry.lua`.

Latest local OneDrive install target for the FS25 mod zip:

```text
C:\Users\Marpa\OneDrive\Документы\My Games\FarmingSimulator2025\mods
```

Copy the latest package there as:

```text
C:\Users\Marpa\OneDrive\Документы\My Games\FarmingSimulator2025\mods\FS25_RealFfbTelemetry.zip
```

Check the active `log.txt` under `My Games/FarmingSimulator2025` when in doubt, and replace the zip in the same profile directory that FS25 is logging to.

Do not keep both `FS25_RealFfbTelemetry.zip` and a `FS25_RealFfbTelemetry/` folder in the FS25 `mods` directory. If FS25 still shows an old version, close the game, remove the folder copy, replace the zip, and start FS25 again.

## Closed Milestones

Milestone 1 delivered the standalone DirectInput test app. Milestone 2 delivered the FS25 Lua telemetry mod and Windows telemetry receiver. These are historical checkpoints; the normal flow is now the MVP app, telemetry mod, profile catalog, and gameplay FFB pipeline.

## DirectInput Baseline

DirectInput test values are kept conservative:

- Default global force limit comes from the detected wheel profile. Unknown wheels start at 35%; Logitech MOMO starts at 40%.
- Spring/damper/constant tests use full DirectInput test magnitude before safety caps.
- Vibration uses a 24 Hz sine wave with high test magnitude before safety caps.
- Directional constant force uses Cartesian directions `-10000/+10000`.

## MVP Effects

The `Effects` tab controls gameplay FFB per vehicle category. Select a category tab, set the category's Overall FFB Strength, then edit each effect's Enabled, Strength, and Curve values for that full category profile.

- `Speed Spring`: centers the wheel more strongly as speed increases.
- `Speed Damper`: adds viscous resistance as speed increases.
- `Mechanical Friction`: adds baseline steering friction, with load and field influence.
- `Load Resistance`: uses `totalMass / mass` to increase spring, damper, and friction influence for heavier setups.
- `RPM Vibration`: capped sine vibration from RPM while the engine is started.
- `Surface Feedback`: low-frequency exact field/wet-field vibration with conservative spring/damper/friction modifiers.
- `Slip Feedback`: sine vibration when wheel slip rises above threshold.
- `Wetness`: increases damping and surface vibration for exact `wetField` surface telemetry; `groundWetness` and `rainScale` are transmitted for diagnostics.
- `Motion`: gates yaw-rate stability/load, pitch/slope load, roll center offset, and local-acceleration load. `Hill Standstill Load` and `Side Slope Bias` have their own toggles but stay inactive while Motion is disabled.
- `Bump Feedback`: short signed constant-force pulses from bump impulse telemetry.

`Emergency Stop` disables gameplay FFB until the header `FFB` status button is pressed again.

Every gameplay FFB mechanic must be documented in `docs/gameplay-ffb-mechanics.md`. Keep that file in sync with code changes that affect telemetry inputs, calculations, DirectInput output mapping, defaults, or stop/safety behavior.

## Data Locations

- Config: `%APPDATA%/FS25FFBBridge/config.json`
- Logs: `%LOCALAPPDATA%/FS25FFBBridge/logs/bridge-.log`
- User effect profiles: `%APPDATA%/FS25FFBBridge/effect-profiles/<wheel-profile-id>.json`
- Effect status file: `Documents/My Games/FarmingSimulator2025/modSettings/FS25_RealFfbTelemetry/effectStatus.json`

The Windows app can show a compact topmost effect overlay window. Use borderless/windowed fullscreen in FS25 if the overlay does not appear over exclusive fullscreen. Disable `Click-through` before dragging the overlay, then enable it again before driving.

## Repository Layout

```text
docs/
fs25-mod/
samples/
tools/
windows-app/
```
