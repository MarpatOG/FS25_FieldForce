# Gameplay FFB Mechanics

This document is the implementation reference for gameplay-driven force feedback in the Windows bridge. Keep it in sync with changes to telemetry inputs, calculation formulas, DirectInput output mapping, defaults, and stop/safety behavior.

Entry points:

- Calculator: `windows-app/src/App/Services/GameplayFfbCalculator.cs`
- Pipeline models: `windows-app/src/App/Models/FfbPipelineModels.cs`
- DirectInput output: `windows-app/src/App/Services/DirectInputFfbBackend.cs`
- Settings defaults: `windows-app/src/App/Models/FfbEffectSettings.cs`
- Telemetry packet: `windows-app/src/App/Models/TelemetryPacket.cs`
- FS25 sender: `fs25-mod/src/FS25RealFfbTelemetry.lua`

## Data From The Game

The FS25 Lua mod sends one JSON telemetry frame over UDP at up to `125 Hz`. If Lua socket support is unavailable, it writes the same packet to `modSettings/FS25_RealFfbTelemetry/telemetry.json` at up to `30 Hz`.

Core state:

```text
timestamp, gameState, isPlayerInVehicle
vehicleName, vehicleType, vehicleCategory
engineStarted
```

Vehicle and steering:

```text
speedKmh, steeringAngle, steeringRate
rpm, throttle, brake, clutch, gear
mass, totalMass
```

Wheel, surface, and weather:

```text
wheelTireTypes, wheelTireProfile
isOnField, surfaceType, surfaceAttribute
groundWetness, rainScale
wheelSlip, maxWheelSlip, groundContactRatio
steeringWheelSlip, steeringGroundContactRatio
```

Motion and suspension:

```text
pitchDeg, rollDeg, yawRateDegPerSec, slopeDeg
localAccelerationX, localAccelerationY, localAccelerationZ
verticalImpactImpulse, landingImpulse, collisionImpulse, longitudinalJerkImpulse
bumpImpulse, suspensionImpulse
leftSuspensionImpulse, rightSuspensionImpulse
```

Important source details:

- `speedKmh` is primarily derived from root-node horizontal world-position delta. Movement below `2 km/h` is treated as standstill, and values are capped at `300 km/h`. Raw FS25 speed APIs are fallback only.
- `steeringAngle` is the first available steering value from vehicle/wheel fields. `steeringRate` is `(currentSteeringAngle - previousSteeringAngle) / dt` for the same vehicle when the sample gap is `0..1 s`.
- `surfaceType` is strict exact detection from wheel physics/surface sound data. Recognized values include `asphalt`, `field`, `wetField`, `grass`, `shallowWater`, `snow`, `dirt`, `gravel`, `mud`, and `unknown`. The mod does not guess dirt/gravel/mud from raw terrain numbers.
- `wheelSlip` is average wheel `physics.netInfo.slip`; `maxWheelSlip` is the maximum wheel slip; `steeringWheelSlip` and `steeringGroundContactRatio` use wheels that currently report steering movement.
- `groundContactRatio` counts wheels with ground, water, or snow contact divided by wheel count.
- `wheelTireTypes` is the unique normalized tire type list. `wheelTireProfile` is `street`, `agricultural`, `mixed`, `tracked`, or `unknown`.
- `pitchDeg`, `rollDeg`, `yawRateDegPerSec`, local acceleration, and `verticalImpactImpulse` come from root-node transform history. `verticalImpactImpulse = min(abs(localAccelerationY) / 9.81, 2)`.
- `bumpImpulse` is kept as a legacy alias for `verticalImpactImpulse`.
- `landingImpulse` is emitted when wheel contact returns after a loss of contact, `collisionImpulse` comes from hard horizontal local acceleration, and `longitudinalJerkImpulse` is acceleration/braking jerk without suspension-hit evidence.
- `leftSuspensionImpulse` and `rightSuspensionImpulse` are side-specific best-effort values. The sender prefers wheel suspension/load fields when exposed by FS25; otherwise it distributes the root-node `verticalImpactImpulse` by left/right wheel contact ratio.

## Feature Extraction

The Windows app converts raw telemetry into `TelemetryFeatures` before calculating effects:

```text
speed = packet.speedKmh < 2 ? 0 : max(packet.speedKmh, 0)
speedRatio = clamp(speed / max(1, SpeedSpring.SpeedReferenceKmh), 0, 1)
loadFactor = clamp(totalMass / mass, 1, 4) when both masses are valid, otherwise 1
loadRatio = clamp((loadFactor - 1) / 2, 0, 1)
slip = clamp(max(steeringWheelSlip, maxWheelSlip, wheelSlip, 0), 0, 1)
contactRatio = clamp(steeringGroundContactRatio ?? groundContactRatio ?? 1, 0, 1)
surfaceClass = field/wetField when exact field surface or legacy isOnField fallback matches, otherwise road
wetness = max(groundWetness, rainScale) when either telemetry value exists; wetField fallback is 0.6
rpmRatio = clamp((rpm - EngineVibration.MinRpm) / (EngineVibration.MaxRpm - EngineVibration.MinRpm), 0, 1)
yawRateRatio = clamp(abs(yawRateDegPerSec) / MotionFeedback.FullYawRateDegPerSec, 0, 1)
slopeRatio = max(abs(pitchDeg), abs(slopeDeg)) normalized by MotionFeedback.FullPitchDeg
suspensionImpulse = clamp(abs(suspensionImpulse ?? bumpImpulse ?? 0), 0, 2)
verticalImpactImpulse = clamp(abs(verticalImpactImpulse ?? suspensionImpulse ?? bumpImpulse ?? 0), 0, 2)
landingImpulse = clamp(abs(landingImpulse ?? 0), 0, 2)
collisionImpulse = clamp(abs(collisionImpulse ?? 0), 0, 2)
longitudinalJerkImpulse = clamp(abs(longitudinalJerkImpulse ?? local horizontal acceleration fallback), 0, 2)
```

Some layers carry confidence values. For example, steering-specific contact telemetry has confidence `1.0`; fallback all-wheel contact has confidence `0.55`; missing contact has confidence `0.0`. Surface confidence is `1.0` when `surfaceType` exists and `0.7` for legacy `isOnField` fallback.

## Shared Formulas

Telemetry fade:

```text
age <= 300 ms:  fade = 1 - (ageMs / 300 * 0.05)
300..1000 ms:   fade = 0.95 * (1 - ((ageMs - 300) / 700))
age > 1000 ms:  fade = 0
```

Curves:

```text
Linear:     curve(x) = x
Aggressive: curve(x) = x^0.65
Smooth:     curve(x) = x * x * (3 - 2 * x)
```

Base effect cap:

```text
maxCapped(effect) = clamp(effect.StrengthPercent, 0, 100)
                  * clamp(effect.MaxOutputPercent, 0, 100) / 100
                  * clamp(fade, 0, 1)
```

Speed condition effects:

```text
speedRatio = clamp(speedKmh / max(1, SpeedReferenceKmh), 0, 1)
normalized = clamp(StandstillFloor + (1 - StandstillFloor) * curve(speedRatio), 0, 1)
output = maxCapped(effect) * normalized
```

Modifier mixing:

```text
SpringGain, DamperGain, FrictionGain start at 1
SpringRelief and DamperAdditive start at 0
each layer is blended toward its modifier by layer confidence

spring = baseSpring * SpringGain * (1 - clamp(SpringRelief, 0, 0.95))
damper = baseDamper * DamperGain + DamperAdditive
friction = baseFriction * FrictionGain * (1 - clamp(SpringRelief * 0.5, 0, 0.7))
```

All steering percentages are clamped to `0..100`. Bump pulse output is signed and clamped to `-100..100`.

## Steering Effects

`Speed Spring` maps to a DirectInput Spring condition:

```text
springBase = speedCondition(SpeedSpring)
spring = springBase
```

`Speed Damper` maps to a DirectInput Damper condition:

```text
damperBase = speedCondition(SpeedDamper)
```

`Mechanical Friction` maps to a DirectInput Friction condition:

```text
frictionNormalized = clamp(
    BaseFriction
    + LoadInfluence * curve(loadRatio)
    + (FieldInfluence when surfaceClass is field/wetField),
    0, 1)
frictionBase = maxCapped(MechanicalFriction) * frictionNormalized
```

`Surface Feedback` modifies steering only on `field` or `wetField`, above `SurfaceFeedback.MinSpeedKmh`, and when the effect is enabled:

```text
SpringGain *= 1 + FieldSpringModifierPercent / 100
DamperGain *= 1 + FieldDamperModifierPercent / 100
FrictionGain *= 1 + FieldFrictionModifierPercent / 100
```

`Wetness Feedback` applies only on field surfaces when enabled and `wetness > MinWetness`:

```text
wetnessRatio = clamp((wetness - MinWetness) / max(0.01, 1 - MinWetness), 0, 1)
wetnessEffect = maxCapped(WetnessFeedback) / 100 * curve(wetnessRatio)
SpringGain *= lerp(1, 0.92, wetnessEffect)
DamperGain *= 1 + wetnessEffect * WetnessFeedback.DamperModifierPercent / 100
```

`Load Resistance` modifies spring, damper, and friction when enabled:

```text
loadResistance = StrengthPercent / 100
               * MaxOutputPercent / 100
               * curve(loadRatio)

SpringGain *= 1 + loadResistance * SpringScale
DamperGain *= 1 + loadResistance * DamperScale
FrictionGain *= 1 + loadResistance * FrictionScale
```

Each target is only modified if the matching `AffectsSpring`, `AffectsDamper`, or `AffectsFriction` flag is enabled.

`Motion Feedback` is the only yaw/slope steering layer. It is fully disabled when `MotionFeedback.Enabled == false`, and `StrengthPercent`, `MaxOutputPercent`, and `Curve` weight the effect:

```text
motionRatio = max(yawRateRatio * speedRatio, slopeRatio)
motionEffect = maxCapped(MotionFeedback) / 100 * curve(motionRatio)
SpringGain *= 1 + motionEffect * MotionFeedback.SpringModifierPercent / 100
DamperGain *= 1 + motionEffect * MotionFeedback.DamperModifierPercent / 100
```

`Contact Traction` reduces centering when steering contact is lost or slip rises:

```text
contactLoss = (1 - contactRatio) * contactConfidence
slipRelief = clamp((slip - SlipFeedback.MinSlip) / max(0.01, SlipFeedback.FullSlip - SlipFeedback.MinSlip), 0, 1) * 0.30
SpringRelief += clamp(contactLoss * 0.65 + slipRelief, 0, 0.85)
```

`Speed Stability` always adds damper after modifier mixing:

```text
speedDamping = speedRatio * 2.0
rateDamping = clamp(abs(steeringRate) / 2.0, 0, 1) * 8.0 * DeviceProfile.SteeringRateDamperScale
antiOscillation = speedRatio > 0.45 and abs(steeringAngle) < 0.04 ? 3.0 : 0.0
damper += speedDamping + rateDamping + antiOscillation
```

## Continuous Haptics

`RPM Vibration` maps to a DirectInput Sine periodic effect:

```text
rpmRatio = clamp((rpm - MinRpm) / (MaxRpm - MinRpm), 0, 1)
percent = maxCapped(EngineVibration) * clamp(0.35 + 0.65 * curve(rpmRatio), 0, 1)
hz = quantize2(MinFrequencyHz + (MaxFrequencyHz - MinFrequencyHz) * rpmRatio)
```

It is active only when Engine Vibration is enabled, `engineStarted == true`, `rpm` exists, and `rpmRatio > 0`.

`Surface Feedback` maps to a DirectInput Sine periodic effect on field surfaces:

```text
surfacePercent = maxCapped(SurfaceFeedback)
if wetnessEffect > 0:
    surfacePercent *= 1 + wetnessEffect * WetnessFeedback.SurfaceVibrationModifierPercent / 100

surfaceHz = quantize2(FieldFrequencyMinHz
          + (FieldFrequencyMaxHz - FieldFrequencyMinHz)
          * curve(clamp(speedKmh / SpeedDamper.SpeedReferenceKmh, 0, 1)))
```

`Slip Feedback` maps to a DirectInput Sine periodic effect:

```text
ratio = clamp((slip - MinSlip) / (FullSlip - MinSlip), 0, 1)
percent = maxCapped(SlipFeedback) * curve(ratio)
hz = quantize2(MinFrequencyHz + (MaxFrequencyHz - MinFrequencyHz) * curve(ratio))
```

It is active only when Slip Feedback is enabled, speed is at least `MinSpeedKmh`, and slip is above `MinSlip`.

`Suspension Terrain Rumble` is a low-frequency continuous haptic derived from suspension impulse. It never creates a finite pulse by itself. Haptics classify `field`, `wetField`, `grass`, `dirt`, `gravel`, `mud`, `snow`, and `shallowWater` as off-road. `asphalt`, ordinary roads, and `unknown` without `isOnField == true` are treated conservatively as road:

```text
roadMinImpulse = max(TerrainRumble.MinImpulse, 0.42)
roadFullImpulse = max(TerrainRumble.FullImpulse, 1.15)
offroadLoadScale = clamp(1 + sqrt(max(loadFactor - 1, 0)) * 0.24, 1, 1.42)
surfaceScale = offroad ? 1.10 : 0.14
ratio = clamp((suspensionImpulse - surfaceMinImpulse)
        / (surfaceFullImpulse - surfaceMinImpulse), 0, 1)
terrainRumblePercent = maxCapped(TerrainRumble) * curve(ratio) * surfaceScale * offroadLoadScale
terrainRumbleHz = quantize2(TerrainRumble.MinFrequencyHz
        + (TerrainRumble.MaxFrequencyHz - TerrainRumble.MinFrequencyHz) * curve(ratio))
```

This keeps asphalt nearly quiet except for large real hits, while the same suspension impulse is stronger on fields, dirt, gravel, mud, snow, grass, wet fields, or shallow water. Extra `totalMass / mass` from an attached implement or trailer amplifies off-road terrain/suspension haptics with the soft cap above.

Continuous haptic layers are mixed by taking the highest percent per channel after confidence weighting.

## Event Pulses

Finite event pulses share one DirectInput ConstantForce bus. The calculator chooses one event per frame by priority:

```text
Collision > Landing > Left/RightSuspensionHit > Bump > DrivetrainJerk > EngineStartStop
```

Each pulse kind has its own cooldown timestamp in the backend.

`Bump Feedback` is now only the road-bump pulse:

```text
impulse = verticalImpactImpulse
suppressed when contactRatio is near zero
suppressed when longitudinalJerkImpulse is high and side suspension does not confirm a hit
surfaceMinImpulse = offroad ? BumpFeedback.MinImpulse : max(BumpFeedback.MinImpulse, 0.58)
surfaceScale = (offroad ? 1.05 : 0.18) * offroadLoadScale
ratio = clamp((abs(impulse) - surfaceMinImpulse) / (BumpFeedback.FullImpulse - surfaceMinImpulse), 0, 1)
percent = maxCapped(BumpFeedback) * curve(ratio) * surfaceScale
direction = sign(localAccelerationX ?? steeringAngle ?? 1)
durationMs = clamp(BumpFeedback.DurationMs, 20, 250)
cooldownMs = clamp(BumpFeedback.CooldownMs, 20, 500)
```

`LeftSuspensionHit` and `RightSuspensionHit` use `SuspensionHitFeedback` only when one side clearly dominates the other side. Off-road uses a lower side threshold and `1.45x` dominance; road uses a higher threshold and `1.80x` dominance, then applies the same surface/load pulse scaling.

`Landing` uses `LandingFeedback` from `landingImpulse`.

`Collision` uses `CollisionFeedback` from `collisionImpulse`, has the longest default cooldown, and suppresses normal bump selection in the same frame.

`Engine Drivetrain` uses `DrivetrainPulse`, not `BumpFeedback`. It emits one transition pulse after the calculator has a previous drivetrain sample:

```text
EngineStartStop: engineStarted changed
DrivetrainJerk: gear changed, abs(delta throttle/brake) >= 0.35, or longitudinalJerkImpulse >= 0.35 without vertical/collision evidence
percent = maxCapped(DrivetrainPulse) * curve(pulseRatio)
durationMs = clamp(DrivetrainPulse.DurationMs, 20, 160)
cooldownMs = clamp(DrivetrainPulse.CooldownMs, 20, 500)
```

## DirectInput Output

The backend owns one DirectInput effect per gameplay channel:

```text
SpringPercent          -> EffectGuid.Spring condition
DamperPercent          -> EffectGuid.Damper condition
FrictionPercent        -> EffectGuid.Friction condition
EngineVibrationPercent -> EffectGuid.Sine periodic
SurfaceVibrationPercent-> EffectGuid.Sine periodic
TerrainRumblePercent   -> EffectGuid.Sine periodic
SlipVibrationPercent   -> EffectGuid.Sine periodic
BumpImpulsePercent     -> EffectGuid.ConstantForce finite pulse
```

Percent output is converted to DirectInput magnitude with:

```text
directInputMagnitude = round(10000 * clamp(percent, 0, 100) / 100)
effectiveLimit = min(globalLimitPercent, deviceLimitPercent)
scaledMagnitude = round(abs(directInputMagnitude) * effectiveLimit / 100)
```

DirectInput details:

- Effects use `EffectFlags.ObjectOffsets | EffectFlags.Cartesian`.
- The primary axis is the first force-feedback actuator offset, or the first axis offset as fallback.
- Condition effects use equal positive/negative coefficient and saturation.
- Spring condition uses deadband `100`; damper and friction use deadband `0`.
- Periodic Sine period is `1_000_000 / hz`.
- Finite pulse direction is `-10000` or `+10000` from the sign of `BumpImpulsePercent`.
- Gameplay output is quantized in `2%` / `2 Hz` steps before being applied.
- Unchanged continuous outputs are skipped. Finite pulses are applied only when a non-zero pulse arrives and that pulse kind's cooldown has elapsed.

Device haptic profiles apply extra caps before DirectInput output. For example, the default Logitech MOMO profile caps engine vibration to `14%`, surface and slip haptics to `18%`, terrain rumble to `14%`, bump pulses to `34%`, and bump duration to `90 ms`.

## Effect Status Return

The Windows app writes `effectStatus.json` under:

```text
Documents/My Games/FarmingSimulator2025/modSettings/FS25_RealFfbTelemetry/effectStatus.json
```

It writes at up to `10 Hz`, plus immediate zero-status writes on Stop All, telemetry loss, and dispose. The FS25 Lua overlay does not read this file anymore; effect activation lamps are displayed in the Windows app from the live gameplay FFB output.

The status file reports user-facing effect lamps:

```text
speedSpring, speedDamper, friction, rpmVibration,
surfaceFeedback, slipFeedback, bump,
suspensionHit, landing, collision, drivetrainPulse
```

It also writes internal layer booleans:

```text
steeringLoad, speedStability, surfaceTraction,
suspensionTerrain, loadSlopeImplement, engineDrivetrain
```

## Stop Behavior

Gameplay FFB outputs zero when gameplay FFB is disabled, no valid packet exists, the player is not in a vehicle, or telemetry fade reaches zero. The controller stops gameplay effects when telemetry is lost, gameplay is inactive, DirectInput acquisition fails, the player leaves the vehicle, gameplay FFB is disabled, the backend/device is disposed, or Emergency Stop/Stop All is triggered.

Periodic and condition effects are disposed when their output reaches zero. Bump pulses are finite-duration effects and are also disposed during stop-all handling.
