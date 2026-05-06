# FS25 Real FFB Bridge

Windows-first force feedback bridge for Farming Simulator 25.

Milestone 1 implemented the standalone Windows FFB test app. Milestone 2 added a FS25 Lua telemetry mod and a UDP/file telemetry receiver. The current MVP stage adds gameplay-driven FFB effects and a fuller in-game telemetry overlay.

## Current Status

- Windows app: DirectInput FFB test baseline physically confirmed on Logitech MOMO Racing Wheel.
- FS25 Lua telemetry mod: implemented for Milestone 2 as a defensive UDP sender.
- Telemetry receiver: implemented in the Windows app with UDP and file fallback.
- Gameplay-driven FFB effects: Speed Spring, Speed Damper, Load Resistance, RPM Vibration, and Surface Feedback are implemented with stronger Logitech MOMO MVP defaults.
- FS25 overlay: displays a compact vertical diagnostic panel with every transmitted telemetry field.

## Requirements

- Windows 10/11.
- .NET 8 SDK to build.
- DirectInput force feedback wheel, initially tuned conservatively for Logitech MOMO Racing Wheel.

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

## Milestone 2 Telemetry Test

1. Run the Windows app.
2. Open `Telemetry`; it should show `Waiting`.
3. Install `fs25-mod/` as `Documents/My Games/FarmingSimulator2025/mods/FS25_RealFfbTelemetry`.
4. Start FS25 and enable `FS25 Real FFB Telemetry`.
5. Enter a vehicle.
6. The app should change to `Connected` and show vehicle/speed/RPM fields as available.
7. With an acquired FFB wheel and gameplay FFB enabled, the bridge applies the enabled MVP effects from telemetry. It fades or stops them when telemetry is lost or the player leaves the vehicle.

The `Telemetry` tab shows UDP status, file fallback status, last packet source, packet age, parser status, and transport errors separately.
If FS25 logs that Lua socket is unavailable, the mod logs the `require("socket")` failure details and falls back to:

```text
Documents/My Games/FarmingSimulator2025/modSettings/FS25_RealFfbTelemetry/telemetry.json
```

Manual transport checks are documented in `docs/telemetry-protocol.md`.

## Milestone 1 Baseline

The current DirectInput test values are calibrated as a conservative Logitech MOMO baseline:

- Default limits: 40% global, 35% device.
- Spring/damper/constant tests use full DirectInput test magnitude before safety caps.
- Vibration uses a 24 Hz sine wave with high test magnitude before safety caps.
- Directional constant force uses Cartesian directions `-10000/+10000`.

## MVP Effects

The `Effects` tab controls gameplay FFB. Strength controls the requested effect output, Max cap limits that effect before global/device limits, and Curve changes how quickly the effect grows from telemetry.

- `Speed Spring`: centers the wheel more strongly as speed increases.
- `Speed Damper`: adds viscous resistance as speed increases.
- `Load Resistance`: uses `totalMass / mass` to increase spring and damper influence for heavier setups.
- `RPM Vibration`: capped sine vibration from RPM while the engine is started.
- `Surface Feedback`: low-frequency field vibration when `isOnField=true`, with conservative spring/damper modifiers.

`Emergency Stop` disables gameplay FFB until `FFB Enabled` is checked again in the `Effects` tab.

## Data Locations

- Config: `%APPDATA%/FS25FFBBridge/config.json`
- Logs: `%LOCALAPPDATA%/FS25FFBBridge/logs/bridge-.log`

## Repository Layout

```text
docs/
fs25-mod/
samples/
tools/
windows-app/
```
