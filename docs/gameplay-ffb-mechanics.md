# Gameplay FFB Mechanics

This document is the implementation reference for gameplay-driven force feedback in the Windows bridge. Keep it in sync with telemetry inputs, calculation formulas, DirectInput output mapping, defaults, and stop/safety behavior.

Entry points:

- Calculator: `windows-app/src/App/Services/GameplayFfbCalculator.cs`
- Pipeline models: `windows-app/src/App/Models/FfbPipelineModels.cs`
- Telemetry packet: `windows-app/src/App/Models/TelemetryPacketV1.cs`
- FS25 sender: `fs25-mod/src/FS25RealFfbTelemetry.lua`

## Telemetry Input

The Lua mod sends nested `FS25_REAL_FFB_TELEMETRY` v1.4 packets. The wire contract contains raw or normalized telemetry only; FFB-specific features are derived in Windows. The receiver still accepts legacy `1.3.0` and `1.2.0` packets.

Core blocks:

```text
protocol, frame, game, player
vehicle, controls, motion, steering, engine, transmission
wheels, suspension, surface, environment, attachments, collisions
diagnostics
```

Important source details:

- `motion.speedKmh` is primarily the stable FS25 vehicle speed from `vehicle:getLastSpeed()` / `vehicle.lastSpeedReal`. Root-node horizontal world-position delta is kept as a fallback/diagnostic source and is ignored for acceleration/impact on spike frames.
- `motion.speedMps` is the same stable speed in meters per second.
- `motion.localAccelerationMps2` is vehicle-local acceleration.
- `motion.yawRateRadPerSec` is radians per second.
- `vehicle.massT` and `vehicle.totalMassT` are metric tonnes.
- `attachments[]` contains recursively attached implements with `name`, `massT`, `totalMassT`, `lateralOffsetM`, and `depth`.
- `vehicle.isArticulated` marks articulated-frame vehicles whose frame/pivot motion should not be treated as a left/right suspension hit.
- `wheels[]` carries per-wheel slip, steering flag, side, contact, suspension impulse, wheel type, tire type/profile, and raw surface/ground context.
- `suspension.verticalImpactImpulse`, `suspension.landingImpulse`, and `collisions.collisionImpulse` remain telemetry inputs; they are not effect percentages.
- `engine.state` reports `off`, `ignition`, `starting`, `running`, or `unknown`; `engine.isStarting` is true during `MotorState.STARTING`.
- `engine.startDurationMs` and `engine.startRemainingMs` describe the FS25 starter/cranking interval when available.
- `events.engineStartSeq` increments when starter cranking begins (`OFF/IGNITION -> STARTING`). `events.engineStopSeq` keeps the existing stop semantics.
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
tireSurfaceMultiplier = profile Tire x Surface matrix lookup after surface alias resolution
wetness = max(environment.groundWetness, environment.rainScale), with wetField fallback of 0.6
rpmRatio = clamp((engine.rpm - MinRpm) / (MaxRpm - MinRpm), 0, 1)
yawRateRatio = clamp(abs(motion.yawRateRadPerSec converted to deg/s) / FullYawRateDegPerSec, 0, 1)
slopeRatio = max(abs(motion.pitchDeg), abs(motion.slopeDeg)) normalized by FullPitchDeg
rollRatio = abs(motion.rollDeg) normalized by SideSlopeBias min/full roll thresholds
rollDirection = sign(motion.rollDeg)
attachedMassRatio = sum(attachments[].massT) / vehicle.massT
implementLateralOffsetRatio = mass-weighted attachments[].lateralOffsetM normalized by ImplementBias.FullLateralOffsetM
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

- Speed spring, speed damper, mechanical friction, load resistance, motion feedback, hill standstill load, side slope bias, implement bias, contact relief, and speed stability combine into DirectInput condition effects.
- Slew smoothing is a tunable dt-based rate limiter over spring, damper, friction, and center offset. It reports active only when it actually clamps a frame-to-frame change.
- Hill standstill load activates at `speed <= 2 km/h` with pitch/slope input and adds extra spring, damper, and friction scaled by load.
- Side slope bias uses roll direction to produce signed `CenterOffsetPercent`, plus a small damper/friction load.
- Implement bias uses attached mass and mass-weighted lateral offset. Centered implements add load only; lateral implements also produce signed `CenterOffsetPercent`.
- Engine vibration, surface feedback, slip feedback, and suspension terrain rumble produce continuous haptics.
- Tire/surface tuning is stored per wheel effects profile in `GameplayFfbSettings.TireSurfaceTuning`. `SurfaceAliases` maps raw map/mod surface names to normalized surfaces. `Matrix[tireProfile][surfaceType]` is clamped to `0..200%`, defaults to `100%`, and uses `50%` for unknown tire/surface fallback.
- The tire/surface matrix scales only continuous `SurfaceVibrationPercent` and `TerrainRumblePercent`. Collision, landing, suspension-hit, bump, drivetrain, gear, and engine start/stop pulses are not scaled by this matrix.
- Default matrix highlights incompatible pairs: street tires on asphalt `20%`, street tires on dirt/gravel/field/plowed field `85%`, agricultural/mud/off-road tires on field/dirt/plowed field `25%`, agricultural/mud/off-road tires on asphalt `90%`, tracked vehicles on field/dirt/plowed field `20%`, tracked vehicles on asphalt `80%`, mixed `60%`, unknown `50%`.
- Collision, landing, left/right suspension hit, bump, gear shift, drivetrain jerk, and engine start/stop share one finite pulse bus. Articulated vehicles suppress left/right suspension-hit selection so pivot/pendulum movement falls back to the softer bump/rumble path.
- Engine start vibration is driven primarily by `events.engineStartSeq`, so it starts during starter cranking instead of waiting for `engine.started=true`. `engine.startDurationMs` can shorten the start vibration duration within the configured start-pulse cap. RPM-rise detection remains a legacy fallback only when `engineStartSeq` is absent.
- Brake-only standstill input is not treated as engine load or drivetrain jerk. This prevents `EL`/`LG` and clutch/brake jerk feedback from activating when the vehicle is stopped and the driver only presses the brake pedal.
- Event priority is `Collision > Landing > Left/RightSuspensionHit > Bump > GearShift > DrivetrainJerk/EngineStartStop`.

DirectInput outputs:

```text
SpringPercent           -> Spring condition
CenterOffsetPercent     -> Spring condition offset
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
