# Gameplay FFB Mechanics

This document is the implementation reference for gameplay-driven force feedback in the FieldForce App. Keep it in sync with telemetry inputs, calculation formulas, DirectInput output mapping, defaults, and stop/safety behavior.

Entry points:

- Calculator: `windows-app/src/App/Services/GameplayFfbCalculator.cs`
- Pipeline models: `windows-app/src/App/Models/FfbPipelineModels.cs`
- Telemetry packet: `windows-app/src/App/Models/TelemetryPacketV1.cs`
- FS25 sender: `fs25-mod/src/FieldForceTelemetry.lua`

## Telemetry Input

The Lua mod sends nested `FIELDFORCE_TELEMETRY` v1.5 packets. The wire contract contains raw or normalized telemetry only; FFB-specific features are derived in Windows. The receiver still accepts legacy `1.4.0`, `1.3.0`, and `1.2.0` packets.

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
- `motion.slopeDeg` is legacy-only. Current Lua writes `null`; Windows derives slope from `motion.pitchDeg`.
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
slopeRatio = abs(motion.slopeDeg ?? motion.pitchDeg) normalized by FullPitchDeg
rollRatio = abs(motion.rollDeg) normalized by SideSlopeBias min/full roll thresholds
rollDirection = -sign(motion.rollDeg), so steering bias points downhill on side slopes
attachedMassRatio = sum(attachments[].massT) / vehicle.massT
implementLateralOffsetRatio = mass-weighted attachments[].lateralOffsetM normalized by ImplementBias.FullLateralOffsetM
accelerationRatio = magnitude(motion.localAccelerationMps2) normalized by MotionFeedback.FullAcceleration
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
- Wheel profiles are resolved through `WheelProfileCatalog` by stable id or DirectInput aliases. Built-in profiles cover Logitech Momo Racing, Driving Force GT/Pro/EX, G25, G27, G29, G920, G923, and Generic FFB Wheel. Each profile supplies display name, aliases, rotation, recommended mode, default global force limit, and `DeviceHapticProfile` caps. User effect profiles are saved under the stable wheel profile id, not the raw DirectInput display name.
- Slew smoothing is a tunable dt-based rate limiter over spring, damper, friction, and center offset. It reports active only when it actually clamps a frame-to-frame change.
- Motion feedback is a separate gated output layer. When `MotionFeedback.Enabled=false`, yaw-rate load, pitch/slope load, roll-derived center offset, local-acceleration load, hill standstill load, and side slope bias all output zero. Motion does not create finite event pulses.
- Hill standstill load activates at `speed <= 2 km/h` with pitch/slope input and adds extra spring, damper, and friction scaled by load. It has its own UI toggle but is still blocked by disabled Motion.
- Side slope bias uses downhill roll direction to produce signed `CenterOffsetPercent`, plus a small damper/friction load. If the right side is higher, the offset is left; if the left side is higher, the offset is right. It has its own UI toggle but is still blocked by disabled Motion. The roll threshold is kept conservative (`6 deg` minimum) so heavy centered trailer loads that report 3-5 degrees of body roll on straight roads do not pull the wheel sideways.
- Implement bias uses attached mass and mass-weighted lateral offset. Centered implements add load only; lateral implements also produce signed `CenterOffsetPercent`.
- Engine vibration, surface feedback, slip feedback, and suspension terrain rumble produce continuous haptics.
- Tire/surface tuning is stored per wheel effects profile in `GameplayFfbSettings.TireSurfaceTuning`. `SurfaceAliases` maps raw map/mod surface names to normalized surfaces. `Matrix[tireProfile][surfaceType]` is clamped to `0..200%`, defaults to `100%`, and uses `50%` for unknown tire/surface fallback.
- The tire/surface matrix scales only continuous `SurfaceVibrationPercent` and `TerrainRumblePercent`. Collision, landing, suspension-hit, bump, drivetrain, gear, and engine start/stop pulses are not scaled by this matrix.
- Default matrix uses a UI scale from `1..10`, stored internally as `20..200%` in 20% steps. Lower values mean the tire/surface pair is expected to absorb more detail; higher values mean it should transmit more vibration. Examples: street tires on asphalt `2`, street tires on field/plowed field `8..9`, agricultural/mud/off-road tires on field `2..3`, agricultural/mud/off-road tires on asphalt `6..7`, tracked vehicles on field/plowed field `2`, unknown surfaces/tires `4..5`.
- Collision, landing, left/right suspension hit, bump, gear shift, drivetrain jerk, and engine start/stop share one finite pulse bus. Articulated vehicles suppress left/right suspension-hit selection so pivot/pendulum movement falls back to the softer bump/rumble path.
- Engine start vibration is driven primarily by `events.engineStartSeq`, so it starts during starter cranking instead of waiting for `engine.started=true`. `engine.startDurationMs` can shorten the start vibration duration within the configured start-pulse cap. RPM-rise detection remains a legacy fallback only when `engineStartSeq` is absent.
- Brake-only standstill input is not treated as engine load or drivetrain jerk. This prevents `EL`/`LG` and clutch/brake jerk feedback from activating when the vehicle is stopped and the driver only presses the brake pedal.
- Event priority is `Collision > Landing > Left/RightSuspensionHit > Bump > GearShift > DrivetrainJerk/EngineStartStop`.
- `effectStatus.json` keeps `loadSlopeImplement` as an aggregate indicator for load, Motion, hill standstill, side slope, and implement-bias activity. The Windows overlay has separate lamps for Motion, Hill Standstill Load, Side Slope Bias, and Implement Bias.

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

Gameplay FFB outputs zero when gameplay FFB is disabled, no valid packet exists, `vehicle=null`, telemetry fade reaches zero, telemetry is lost, the DirectInput backend cannot apply effects, an AI helper is active, the local player is a passenger or explicitly not the driver, or Emergency Stop/Stop All is triggered.
