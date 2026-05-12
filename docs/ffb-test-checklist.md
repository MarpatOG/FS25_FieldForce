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

## MVP Gameplay Effects

- Select and acquire the FFB wheel before starting FS25 telemetry verification.
- Confirm the header `FFB` status button shows `ready` after selecting the wheel.
- Enter a vehicle and start driving slowly.
- `Speed Spring`: centering is very weak at standstill, soft at low speed, and stronger around 20-30 km/h.
- `Speed Damper`: wheel movement gets less twitchy as speed increases, without becoming stiff at standstill.
- `Load Resistance`: attaching heavier equipment increases the displayed load factor and resistance if enabled.
- `RPM Vibration`: no vibration when the engine is off; vibration appears when RPM rises.
- `Surface Feedback`: exact `field` or `wetField` surface state enables soft low-frequency vibration above minimum speed.
- `Slip Feedback`: high `maxWheelSlip` enables slip vibration and low slip stays quiet.
- `Wetness`: exact `wetField` telemetry increases damping/surface feel; `groundWetness` and `rainScale` should still appear in the overlay when available.
- `Motion`: yaw rate and pitch/slope telemetry should affect stability/load behavior; the UI motion toggle is not currently a separate gated output layer.
- `Terrain Rumble`: on asphalt, small/medium suspension impulse stays quiet; large road hits may produce only weak rumble. On `field`, `wetField`, `grass`, `dirt`, `gravel`, `mud`, `snow`, or `shallowWater`, the same impulse produces stronger continuous low-frequency rumble without a finite pulse lamp.
- `Bump Feedback`: asphalt only produces short, softened pulses for noticeable vertical hits; off-road bumps are stronger and still return to zero after the pulse.
- `Suspension Hit`: left/right suspension impulse dominance shows a suspension-hit pulse, not a generic bump. Road side hits require a clearer imbalance than off-road side hits.
- `Implement/trailer load`: when `totalMass > mass`, off-road Terrain Rumble, Bump Feedback, and Suspension Hit increase moderately; asphalt should not turn into constant vibration.
- `Unknown surface`: `unknown` without `isOnField` uses the intermediate `unknownMixed` suspension/terrain tuning, so custom map surfaces are not damped as strict road; `unknown` with `isOnField` follows off-road suspension/terrain behavior.
- `Landing`: contact loss followed by vertical impact shows the landing pulse.
- `Collision`: hard horizontal contact shows the collision pulse and suppresses normal bump in the same frame.
- `Drivetrain Pulse`: gear changes or acceleration/braking jerk show drivetrain pulse activity with lower strength than bump/collision.
- Each effect can be enabled/disabled independently and each strength slider changes the live output value.
- Stop the engine, leave the vehicle, or stop telemetry; gameplay outputs fade or stop safely.
- `Stop All` and `Ctrl+Alt+Pause` stop test and gameplay effects. `Stop All` disables gameplay FFB until the header `FFB` status button is pressed again.

## FS25 Overlay

- Overlay appears as a compact mod container.
- Telemetry fields are displayed vertically as label/value rows.
- The overlay includes the base vehicle fields plus surface, wetness/rain, wheel slip, attitude, local acceleration, and bump/impact impulse telemetry.
- If the panel background is unavailable in the game render API, the text list still renders and FS25 does not crash.

## Safety Checks

- `Emergency Stop` stops active effects.
- `Ctrl+Alt+Pause` stops active effects and disables gameplay FFB, including when another window has focus if global hotkey registration succeeds.
- Closing the app stops active effects.
- Disconnecting the wheel and pressing `Scan` logs a device disconnect safety event.

## Expected Logs

- Device detected.
- Device selected.
- Effect started.
- Effect stopped.
- Safety event.
- App shutdown safety stop.
