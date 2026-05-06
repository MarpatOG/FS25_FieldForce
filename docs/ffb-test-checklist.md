# FFB Test Checklist

Use this checklist with the wheel firmly mounted and your hands clear before starting each effect.

## App Launch

- App opens without FS25 running.
- Dashboard shows the DirectInput backend as idle or ready.
- Logs show application initialization.

## Device Scan

- Press `Scan`.
- Logitech MOMO or another DirectInput wheel appears in the list.
- Device name, stable ID, axes, and FFB capability are visible.
- Selecting the wheel logs `Device selected`.

## Test Effects

- Confirmed baseline hardware: Logitech MOMO Racing Wheel.
- Recommended initial limits: 40% global, 35% device.
- `Spring`: wheel gently returns toward center.
- `Damper`: wheel movement feels heavier.
- `Constant Left`: wheel pulls left briefly.
- `Constant Right`: wheel pulls right briefly.
- `Vibration`: short sine vibration is felt clearly but should not yank the wheel.
- `Stop All`: active effect stops immediately.

## Gameplay RPM Vibration

- Select and acquire the FFB wheel before starting FS25 telemetry verification.
- Enter a vehicle and start the engine.
- A very light vibration appears and changes subtly with RPM.
- Stop the engine or leave the vehicle; vibration stops within about half a second.
- `Stop All` and `Ctrl+Alt+Pause` stop both test effects and gameplay vibration.

## Safety Checks

- `Emergency Stop` stops active effects.
- `Ctrl+Alt+Pause` stops active effects, including when another window has focus if global hotkey registration succeeds.
- Closing the app stops active effects.
- Disconnecting the wheel and pressing `Scan` logs a device disconnect safety event.

## Expected Logs

- Device detected.
- Device selected.
- Effect started.
- Effect stopped.
- Safety event.
- App shutdown safety stop.
