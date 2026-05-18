# FS25 Real FFB Bridge - G27 FS25 Test

This package contains a portable Windows app build and the matching FS25 telemetry mod.

## Install

1. Extract this archive to any local folder.
2. Copy `mod/FS25_RealFfbTelemetry.zip` to:

   ```text
   Documents\My Games\FarmingSimulator2025\mods\
   ```

3. Make sure there is not also an unpacked `FS25_RealFfbTelemetry\` folder in the same `mods` directory.
4. Start `app/FS25FfbBridge.App.exe`.

## First FS25 Test With Logitech G27

1. Keep the wheel mounted firmly.
2. In the Logitech driver/profiler and in FS25, turn native force feedback off or set it very low before the first bridge test.
3. In the app, open `Device`, press `Scan`, and select the G27.
4. Expected app result: the device is acquired as `Logitech G27`, and the global force limit changes to `50%`.
5. Open the app's `Telemetry` tab. By default it watches:

   ```text
   %USERPROFILE%\Documents\My Games\FarmingSimulator2025\modSettings\FS25_RealFfbTelemetry\telemetry.json
   ```

6. If FS25 uses another profile folder, press `Choose folder`. You can select `Documents`, `My Games`, `FarmingSimulator2025`, `modSettings`, or `FS25_RealFfbTelemetry`; the app resolves it to the matching `telemetry.json`.
7. Start FS25, enable the `FS25 Real FFB Telemetry` mod, load a save, and enter a vehicle.
8. In the app, the telemetry state should become `Connected`.
9. Drive slowly first. Gameplay FFB is enabled by default and uses the current full effect set: speed spring, damper, friction, load, RPM, surface, slip, wetness, Motion, bump, suspension hit, landing, collision, terrain rumble, and drivetrain pulse.

## Safety

- Press `Stop All` in the app to stop current effects.
- Press `Emergency Stop` or `Ctrl+Alt+Pause` if anything feels wrong.
- Closing the app should stop active force feedback effects.
- If telemetry becomes lost, or the player leaves the vehicle, gameplay force feedback should fade or stop.

## Logs

App logs are written under:

```text
%LOCALAPPDATA%\FS25FFBBridge\logs\
```

Useful failure signs: DirectInput acquire/apply errors, telemetry JSON parse errors, or FS25 mod loading errors in the game's `log.txt`.
