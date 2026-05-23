# Telemetry Protocol v1.6

The FS25 telemetry mod writes JSON packets to a file transport by default. UDP remains available as a hidden diagnostic transport.

## Transport

- Protocol: file JSON by default
- Default file target rate: `60 Hz`
- Allowed target rates: `1`, `10`, `30`, `60 Hz`
- Default file path watched by the Windows app: `%USERPROFILE%/Documents/My Games/FarmingSimulator2025/modSettings/FS25_FieldForceTelemetry/telemetry.json`
- If FS25 uses another profile folder, choose it in the app's Telemetry tab. Selecting `Documents`, `My Games`, `FarmingSimulator2025`, `modSettings`, or `FS25_FieldForceTelemetry` resolves to the matching `telemetry.json`.
- Hidden UDP host: `127.0.0.1`
- Hidden UDP port: `34325`

The Windows receiver accepts the current `FIELDFORCE_TELEMETRY` protocol name and legacy `FS25_REAL_FFB_TELEMETRY` protocol name. It accepts current `1.6.0` packets and legacy `1.5.0` / `1.4.0` / `1.3.0` / `1.2.0` packets:

```json
{ "protocol": { "name": "FIELDFORCE_TELEMETRY", "version": "1.6.0" } }
```

Flat legacy JSON is rejected and does not replace the last valid packet.

## Packet Shape

The top-level wire object contains only these blocks:

```text
protocol, frame, game, player, vehicle, controls, motion, bodyAttitude,
roadSlope, steering, engine, transmission, events, wheels, suspension,
impact, surface, environment, attachments, collisions, diagnostics
```

Example:

```json
{
  "protocol": { "name": "FIELDFORCE_TELEMETRY", "version": "1.6.0" },
  "frame": {
    "sequence": 1,
    "dtMs": 8,
    "telemetryRateHz": 125,
    "timestampMs": 123456,
    "isDuplicate": false,
    "isInterpolated": false
  },
  "game": { "state": "mission" },
  "player": { "isInVehicle": true, "isDriver": true, "isPassenger": false },
  "vehicle": {
    "name": "Tractor",
    "type": "tractor",
    "category": "TractorWheeled",
    "wheelTireTypes": "street,mud",
    "wheelTireProfile": "mixed",
    "isArticulated": false,
    "massT": 6.2,
    "totalMassT": 8.8,
    "aiWorkerActive": false
  },
  "controls": { "throttle": 0.6, "brake": 0.0, "clutch": 0.0 },
  "motion": {
    "speedMps": 3.444,
    "speedKmh": 12.4,
    "pitchDeg": 3.1,
    "rollDeg": -2.4,
    "yawRateRadPerSec": 0.14835,
    "slopeDeg": null,
    "localAccelerationMps2": { "x": 0.3, "y": 1.8, "z": -0.6 }
  },
  "bodyAttitude": { "pitchDeg": 3.1, "rollDeg": -2.4, "yawRateRadPerSec": 0.14835, "confidence": 0.65 },
  "roadSlope": { "longitudinalDeg": 2.2, "lateralDeg": -1.4, "confidence": 0.9, "source": "heightSampling" },
  "steering": { "angle": 0.13, "rate": 0.8 },
  "engine": {
    "isRunning": false,
    "started": false,
    "state": "starting",
    "isStarting": true,
    "startDurationMs": 1200,
    "startRemainingMs": 980,
    "rpm": 850,
    "motorType": "diesel",
    "powertrainType": "combustion",
    "energySources": ["diesel"]
  },
  "transmission": { "gear": 3 },
  "events": { "engineStartSeq": 4, "engineStopSeq": 1, "gearChangeSeq": 8, "gearChangeKind": "up", "gearChangeTimeMs": 650 },
  "wheels": [
    {
      "index": 0,
      "side": "left",
      "isSteering": true,
      "slip": 0.24,
      "hasGroundContact": true,
      "suspensionImpulse": 0.18,
      "wheelType": "wheel",
      "tireType": "street",
      "tireProfile": "street",
      "surfaceType": "asphalt",
      "surfaceAttribute": 3,
      "groundType": "asphalt",
      "groundDepth": 0.0,
      "isOnField": false,
      "rawSuspensionLength": 0.42,
      "suspTravel": 0.18,
      "suspensionVelocity": -0.35,
      "suspensionLoad": 4200,
      "tireLoad": 3900,
      "contactForce": 4100,
      "contactPoint": { "x": 10.1, "y": 73.2, "z": -18.7 },
      "contactNormal": { "x": 0.0, "y": 1.0, "z": 0.0 },
      "hasContact": true,
      "axleRole": "front",
      "wheelRole": "steered",
      "steeringInfluence": 1.0,
      "compressionRatio": null
    }
  ],
  "suspension": {
    "impulse": 0.30,
    "hitImpulse": 0.18,
    "bottomOutImpulse": null,
    "verticalImpactImpulse": 0.46,
    "landingImpulse": null,
    "leftImpulse": 0.18,
    "rightImpulse": 0.06,
    "leftHitImpulse": 0.18,
    "rightHitImpulse": 0.06,
    "leftBottomOutImpulse": null,
    "rightBottomOutImpulse": null,
    "suspensionConfidence": 1.0,
    "bottomOutConfidence": 0.0,
    "source": "wheel"
  },
  "impact": {
    "localAccelerationMps2": { "x": 0.3, "y": 1.8, "z": -0.6 },
    "verticalBodyImpulse": 0.46,
    "horizontalBodyImpulse": 0.21
  },
  "surface": { "isOnField": true, "type": "field", "attribute": 1 },
  "environment": { "groundWetness": 0.35, "rainScale": 0.2 },
  "attachments": [
    {
      "name": "Seeder",
      "massT": 1.6,
      "totalMassT": 1.6,
      "lateralOffsetM": 0.35,
      "depth": 1
    }
  ],
  "collisions": { "collisionImpulse": null, "longitudinalJerkImpulse": 0.21 },
  "diagnostics": { "payloadBytes": 1800, "buildTimeMs": 0.4, "warnings": [] }
}
```

## Nullability

When no driveable vehicle is active:

- `vehicle=null`
- `controls`, `motion`, `bodyAttitude`, `roadSlope`, `steering`, `engine`, `transmission`, `events`, `suspension`, `impact`, `surface`, and `collisions` are `null`
- `wheels=[]`
- `attachments=[]`

The receiver treats that as a valid no-vehicle state and emits no gameplay FFB.

## Units

- `massT`, `totalMassT`: metric tonnes.
- `attachments[]`: recursively attached implements. `lateralOffsetM` is measured in the active vehicle local coordinate system; negative/positive sign follows the local X axis.
- `attachments[].depth`: attachment-tree depth where direct implements are `1`.
- `vehicle.isArticulated`: true for articulated-frame vehicles, used by the FieldForce App to avoid treating articulation suspension movement as a sharp left/right suspension hit.
- `player.isDriver`: true when the active vehicle is controlled by the local player.
- `player.isPassenger`: true when the player is in a vehicle but not controlling it.
- `vehicle.aiWorkerActive`: true when FS25 reports an active AI helper for the active vehicle.
- `speedMps`: meters per second.
- `speedKmh`: stable FS25 vehicle speed in kilometers per hour for UI and profile thresholds. The Lua mod may calculate a root-node position-delta speed for fallback/diagnostics, but position spikes are not the primary wire value.
- `localAccelerationMps2`: vehicle-local acceleration in meters per second squared.
- `yawRateRadPerSec`: radians per second.
- `motion.pitchDeg`, `motion.rollDeg`, `motion.slopeDeg`: legacy compatibility fields. Windows uses them for old packets and body-motion feedback fallback.
- `bodyAttitude`: body pitch/roll/yaw-rate with confidence, kept separate from road geometry.
- `roadSlope`: primary road geometry. `source` is one of `"heightSampling"`, `"terrainNormal"`, `"bodyFallback"`, or `"none"`. `contactNormal` is diagnostic wheel data and must not be used as `roadSlope.source`.
- `steering.angle`: normalized/raw steering angle from FS25 source data.
- `steering.rate`: steering angle delta per second.
- `engine.state`: `"off"`, `"ignition"`, `"starting"`, `"running"`, or `"unknown"` from `Motorized:getMotorState()`.
- `engine.isStarting`: true while the FS25 motor state is `MotorState.STARTING`.
- `engine.startDurationMs`: optional `spec_motorized.motorStartDuration`.
- `engine.startRemainingMs`: optional remaining time until FS25 promotes `STARTING` to `ON`.
- `engine.motorType`: raw motor/type string from FS25 or the vehicle, kept for diagnostics and legacy heuristics.
- `engine.powertrainType`: normalized `"combustion"`, `"electric"`, `"hybrid"`, or `"unknown"` value derived from FS25 motorized consumer/fill-unit sources when available.
- `engine.energySources`: optional array of detected energy sources such as `"diesel"`, `"electricCharge"`, or `"methane"`.
- `events.engineStartSeq`: increments when starter cranking begins (`OFF/IGNITION -> STARTING`), not when the engine has already reached `ON`.
- `events.engineStopSeq`: increments on stop events.
- `wheels[].wheelType`: `"wheel"`, `"crawler"`, or `"unknown"`.
- `wheels[].tireType`: normalized tire type: `"street"`, `"agricultural"`, `"mud"`, `"offRoad"`, `"crawler"`, or `"unknown"`.
- `wheels[].tireProfile`: Windows tuning bucket: `"street"`, `"agricultural"`, `"mud"`, `"tracked"`, `"mixed"`, or `"unknown"`.
- `wheels[].surfaceType`, `surface.type`: normalized surface such as `"asphalt"`, `"dirt"`, `"gravel"`, `"mud"`, `"grass"`, `"snow"`, `"shallowWater"`, `"field"`, `"plowedField"`, `"cultivatedField"`, `"wetField"`, or `"unknownMixed"`.
- `wheels[].surfaceAttribute`, `surface.attribute`: raw/engine terrain attribute when available.
- `wheels[].groundType` and `wheels[].groundDepth`: raw ground context from wheel physics when available.
- `wheels[].rawSuspensionLength`, `suspTravel`, `suspensionVelocity`: raw suspension motion values when available.
- `wheels[].suspensionLoad`, `tireLoad`, `contactForce`: optional load/contact force readings.
- `wheels[].contactPoint`, `contactNormal`, `hasContact`: optional wheel contact diagnostics.
- `wheels[].axleRole`: `"front"`, `"rear"`, `"center"`, or `"unknown"`.
- `wheels[].wheelRole`: `"steered"`, `"drive"`, `"trailer"`, `"implement"`, `"crawler"`, or `"unknown"`.
- `wheels[].compressionRatio`: nullable. It is populated only after the sender has observed a plausible suspension range; otherwise it remains `null`.
- `suspension.hitImpulse`: sharp suspension hit channel.
- `suspension.bottomOutImpulse`: near-end travel bottom-out channel. It requires compression ratio, compression velocity, load/contact confirmation, and contact.

## Derived Features

The wire packet must not contain FFB-derived fields such as speed ratio, normalized slip, terrain bump, collision strength, side hit strength, engine vibration, or any effect percentage. Windows derives those values in `TelemetryFeatureExtractor` from the nested raw telemetry.

## Diagnostics

Payload budget:

- target: under `16 KB`
- warning: above `24 KB`
- hard warning: above `48 KB`

Lua records payload and build diagnostics in `diagnostics`. If packet build time is greater than `2 ms`, a warning is appended to `diagnostics.warnings`.

## Manual UDP Test

```powershell
$udp = [System.Net.Sockets.UdpClient]::new()
$endpoint = [System.Net.IPEndPoint]::new([System.Net.IPAddress]::Parse("127.0.0.1"),34325)
$json = Get-Content samples/telemetry-packets/milestone2-valid.json -Raw
$bytes = [Text.Encoding]::UTF8.GetBytes($json)
[void]$udp.Send($bytes,$bytes.Length,$endpoint)
$udp.Dispose()
```

## Manual File Test

```powershell
$path = "$env:USERPROFILE\Documents\My Games\FarmingSimulator2025\modSettings\FS25_FieldForceTelemetry\telemetry.json"
New-Item -ItemType Directory -Force -Path (Split-Path $path) | Out-Null
Get-Content samples/telemetry-packets/milestone2-valid.json -Raw | Set-Content -Encoding UTF8 $path
```
