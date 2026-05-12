# Gameplay FFB Mechanics

This document is the implementation reference for gameplay-driven force feedback in the Windows bridge. Keep it in sync with telemetry inputs, calculation formulas, DirectInput output mapping, defaults, and stop/safety behavior.

Entry points:

- Calculator: `windows-app/src/App/Services/GameplayFfbCalculator.cs`
- Pipeline models: `windows-app/src/App/Models/FfbPipelineModels.cs`
- Telemetry packet: `windows-app/src/App/Models/TelemetryPacketV1.cs`
- FS25 sender: `fs25-mod/src/FS25RealFfbTelemetry.lua`

## Telemetry Input

The Lua mod sends nested `FS25_REAL_FFB_TELEMETRY` v1.0 packets. The wire contract contains raw or normalized telemetry only; FFB-specific features are derived in Windows.

Core blocks:

```text
protocol, frame, game, player
vehicle, controls, motion, steering, engine, transmission
wheels, suspension, surface, environment, attachments, collisions
diagnostics
```

Important source details:

- `motion.speedKmh` is primarily derived from root-node horizontal world-position delta. Movement below `2 km/h` is treated as standstill, and values are capped at `300 km/h`.
- `motion.speedMps` is the same source speed in meters per second.
- `motion.localAccelerationMps2` is vehicle-local acceleration.
- `motion.yawRateRadPerSec` is radians per second.
- `vehicle.massT` and `vehicle.totalMassT` are metric tonnes.
- `vehicle.isArticulated` marks articulated-frame vehicles whose frame/pivot motion should not be treated as a left/right suspension hit.
- `wheels[]` carries per-wheel slip, steering flag, side, contact, and suspension impulse.
- `suspension.verticalImpactImpulse`, `suspension.landingImpulse`, and `collisions.collisionImpulse` remain telemetry inputs; they are not effect percentages.
- Missing optional FS25 API values are sent as `null`.

When `vehicle=null`, vehicle-dependent blocks are `null`, `wheels=[]`, and `attachments=[]`. The calculator outputs zero FFB.

## Feature Extraction

`TelemetryFeatureExtractor` converts nested telemetry into `TelemetryFeatures` before effects are calculated:

```text
speed = motion.speedKmh < 2 ? 0 : max(motion.speedKmh, 0)
speedRatio = clamp(speed / max(1, SpeedSpring.SpeedReferenceKmh), 0, 1)
loadFactor = clamp(vehicle.totalMassT / vehicle.massT, 1, 4) when both masses are valid, otherwise 1
slip = clamp(max(steering wheel slip, max wheel slip, average wheel slip, 0), 0, 1)
contactRatio = clamp(steering contact ratio ?? all-wheel contact ratio ?? 1, 0, 1)
surfaceClass = road/offroad/unknownMixed; surface.isOnField=true promotes unknown surfaces to offroad
wetness = max(environment.groundWetness, environment.rainScale), with wetField fallback of 0.6
rpmRatio = clamp((engine.rpm - MinRpm) / (MaxRpm - MinRpm), 0, 1)
yawRateRatio = clamp(abs(motion.yawRateRadPerSec converted to deg/s) / FullYawRateDegPerSec, 0, 1)
slopeRatio = max(abs(motion.pitchDeg), abs(motion.slopeDeg)) normalized by FullPitchDeg
suspensionImpulse = maxValid(abs(suspension.impulse), abs(suspension.verticalImpactImpulse)), clamped 0..2
verticalImpactImpulse = maxValid(abs(suspension.verticalImpactImpulse), abs(suspension.impulse)), clamped 0..2
landingImpulse = normalized separately with small impulse noise rejected
collisionImpulse = normalized separately with small impulse noise rejected
longitudinalJerkImpulse = clamp(abs(collisions.longitudinalJerkImpulse ?? local horizontal acceleration fallback), 0, 2)
isArticulatedVehicle = vehicle.isArticulated == true
```

FFB-derived values such as speed ratio, normalized slip, terrain rumble, side hit, collision strength, and engine vibration are never read from JSON.

## Shared Formulas

Telemetry fade:

```text
age <= 300 ms:  fade = 1 - (ageMs / 300 * 0.05)
300..1000 ms:   fade = 0.95 * (1 - ((ageMs - 300) / 700))
age > 1000 ms:  fade = 0
```

Curves:

```text
Linear:      curve(x) = x
Aggressive: curve(x) = x^0.65
Smooth:     curve(x) = x * x * (3 - 2 * x)
```

Base effect cap:

```text
maxCapped(effect) = clamp(effect.StrengthPercent, 0, 100)
                  * clamp(effect.MaxOutputPercent, 0, 100) / 100
                  * clamp(fade, 0, 1)
```

## Effects

- Speed spring, speed damper, mechanical friction, load resistance, motion feedback, contact relief, and speed stability combine into DirectInput condition effects.
- Engine vibration, surface feedback, slip feedback, and suspension terrain rumble produce continuous haptics.
- Collision, landing, left/right suspension hit, bump, gear shift, drivetrain jerk, and engine start/stop share one finite pulse bus. Articulated vehicles suppress left/right suspension-hit selection so pivot/pendulum movement falls back to the softer bump/rumble path.
- Event priority is `Collision > Landing > Left/RightSuspensionHit > Bump > GearShift > DrivetrainJerk/EngineStartStop`.

DirectInput outputs:

```text
SpringPercent           -> Spring condition
DamperPercent           -> Damper condition
FrictionPercent         -> Friction condition
EngineVibrationPercent  -> Sine periodic
SurfaceVibrationPercent -> Sine periodic
TerrainRumblePercent    -> Sine periodic
SlipVibrationPercent    -> Sine periodic
BumpImpulsePercent      -> ConstantForce finite pulse
```

## Stop Behavior

Gameplay FFB outputs zero when gameplay FFB is disabled, no valid packet exists, `vehicle=null`, telemetry fade reaches zero, telemetry is lost, the DirectInput backend cannot apply effects, or Emergency Stop/Stop All is triggered.
