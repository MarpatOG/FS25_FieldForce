# Gameplay FFB Mechanics

This document tracks gameplay-driven force feedback implemented by the Windows bridge.

Entry points:

- Calculator: `windows-app/src/App/Services/GameplayFfbCalculator.cs`
- DirectInput output: `windows-app/src/App/Services/DirectInputFfbBackend.cs`
- Settings defaults: `windows-app/src/App/Models/FfbEffectSettings.cs`
- Telemetry packet: `windows-app/src/App/Models/TelemetryPacket.cs`

## Shared Behavior

Gameplay FFB outputs zero when gameplay FFB is disabled, no valid packet exists, the player is not in a vehicle, or telemetry fade reaches zero.

Telemetry fade:

```text
age <= 300 ms:  1.00 down to 0.95
300..1000 ms:   0.95 down to 0.00
age > 1000 ms:  0.00
```

All effect percentages are clamped to `0..100`, except bump impulse which is signed `-100..100`. DirectInput magnitudes are still capped by the stricter of global and device force limits.

## Telemetry Inputs

The Lua mod sends the original vehicle fields plus:

```text
surfaceType, surfaceAttribute, groundWetness, rainScale
wheelSlip, maxWheelSlip, groundContactRatio
wheelTireTypes, wheelTireProfile
pitchDeg, rollDeg, yawRateDegPerSec, slopeDeg
localAccelerationX, localAccelerationY, localAccelerationZ
bumpImpulse
```

Surface detection is strict exact. The bridge recognizes exact labels from FS25 wheel physics such as `asphalt`, `field`, `wetField`, `grass`, `shallowWater`, and `snow`. It emits `dirt`, `gravel`, or `mud` only if FS25 returns those exact names. Raw `surfaceAttribute` is preserved but not guessed into a named surface.

Tire profile detection reads FS25 `wheel.physics.tireType` and normalizes unique tire names into `street`, `agricultural`, `mixed`, `tracked`, or `unknown`. Raw truck categories with agricultural or tracked tire profiles are routed to tractor profiles by the Lua mod.

## Category Profiles

Effects profile version `6` stores full per-category effect profiles in `GameplayFfbSettings.VehicleCategoryEffectProfiles`. The Windows app uses `packet.VehicleCategory` to select a profile, falls back to `Unknown`, then to the base profile. Version 5 `VehicleCategoryProfiles` multipliers are legacy migration input only; v5 configs are migrated by cloning the current base profile per category and applying those multipliers once.

## Effects

- `Speed Spring`: DirectInput Spring condition; grows with speed using `SpeedSpring.SpeedReferenceKmh` and a small standstill floor.
- `Speed Damper`: DirectInput Damper condition; grows with speed and provides high-speed resistance.
- `Mechanical Friction`: DirectInput Friction condition; baseline steering friction, increased by load and field modifiers.
- `Load Resistance`: modifier over spring, damper, and friction using `totalMass / mass`, clamped to a load factor range of `1..4`.
- `RPM Vibration`: DirectInput Sine periodic effect; intensity and frequency increase between configured min/max RPM.
- `Surface Feedback`: DirectInput Sine periodic effect on exact field/wet-field surfaces above minimum speed; also modifies spring, damper, and friction. Profile version 4 lowers the default field activation threshold to `0.2 km/h`.
- `Slip Feedback`: DirectInput Sine periodic effect when `maxWheelSlip` or `wheelSlip` exceeds `SlipFeedback.MinSlip`; frequency rises toward `MaxFrequencyHz`. Profile version 4 lowers the default speed gate to `0.5 km/h`.
- `Wetness`: modifier, not a standalone effect; when `groundWetness` or `rainScale` is present, it increases damper and field surface vibration.
- `Motion`: modifier, not a standalone effect; pitch, roll, slope, yaw rate, and local acceleration lightly increase spring and damper within configured caps.
- `Bump Feedback`: short DirectInput ConstantForce pulse from signed `bumpImpulse`; direction comes from local lateral acceleration or steering fallback, with duration/cooldown settings.

## Stop Behavior

The controller stops gameplay effects when telemetry is lost, the player leaves the vehicle, gameplay FFB is disabled, the backend/device is disposed, or Emergency Stop/Stop All is triggered. Periodic and condition effects are disposed when their output reaches zero. Bump pulses are finite-duration effects and are also disposed during stop-all handling.

The Windows app also writes `effectStatus.json` under the FS25 modSettings folder at up to 10 Hz. Zero-status is written immediately when effects are stopped due to Stop All, telemetry loss, or disposal so the in-game overlay can show real output state.
