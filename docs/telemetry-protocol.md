# Telemetry Protocol v1.0

The FS25 telemetry mod sends UDP JSON over localhost when Lua socket support is available. If socket support is unavailable, it writes the same v1 packet to the file fallback path.

## Transport

- Protocol: UDP JSON
- Default host: `127.0.0.1`
- Default port: `34325`
- Default UDP target rate: `125 Hz`
- File fallback target rate: `30 Hz`
- File fallback: `Documents/My Games/FarmingSimulator2025/modSettings/FS25_RealFfbTelemetry/telemetry.json`

The Windows receiver accepts only packets with:

```json
{ "protocol": { "name": "FS25_REAL_FFB_TELEMETRY", "version": "1.0.0" } }
```

Flat legacy JSON is rejected and does not replace the last valid packet.

## Packet Shape

The top-level wire object contains only these blocks:

```text
protocol, frame, game, player, vehicle, controls, motion, steering,
engine, transmission, wheels, suspension, surface, environment,
attachments, collisions, diagnostics
```

Example:

```json
{
  "protocol": { "name": "FS25_REAL_FFB_TELEMETRY", "version": "1.0.0" },
  "frame": {
    "sequence": 1,
    "dtMs": 8,
    "telemetryRateHz": 125,
    "timestampMs": 123456,
    "isDuplicate": false,
    "isInterpolated": false
  },
  "game": { "state": "mission" },
  "player": { "isInVehicle": true },
  "vehicle": {
    "name": "Tractor",
    "type": "tractor",
    "category": "TractorWheeled",
    "wheelTireTypes": "street,mud",
    "wheelTireProfile": "mixed",
    "massT": 6.2,
    "totalMassT": 8.8
  },
  "controls": { "throttle": 0.6, "brake": 0.0, "clutch": 0.0 },
  "motion": {
    "speedMps": 3.444,
    "speedKmh": 12.4,
    "pitchDeg": 3.1,
    "rollDeg": -2.4,
    "yawRateRadPerSec": 0.14835,
    "slopeDeg": 4.0,
    "localAccelerationMps2": { "x": 0.3, "y": 1.8, "z": -0.6 }
  },
  "steering": { "angle": 0.13, "rate": 0.8 },
  "engine": { "rpm": 850, "started": true },
  "transmission": { "gear": 3 },
  "wheels": [
    { "index": 0, "side": "left", "isSteering": true, "slip": 0.24, "hasGroundContact": true, "suspensionImpulse": 0.18 }
  ],
  "suspension": {
    "impulse": 0.30,
    "verticalImpactImpulse": 0.46,
    "landingImpulse": null,
    "leftImpulse": 0.18,
    "rightImpulse": 0.06
  },
  "surface": { "isOnField": true, "type": "field", "attribute": 1 },
  "environment": { "groundWetness": 0.35, "rainScale": 0.2 },
  "attachments": [],
  "collisions": { "collisionImpulse": null, "longitudinalJerkImpulse": 0.21 },
  "diagnostics": { "payloadBytes": 1800, "buildTimeMs": 0.4, "warnings": [] }
}
```

## Nullability

When no driveable vehicle is active:

- `vehicle=null`
- `controls`, `motion`, `steering`, `engine`, `transmission`, `suspension`, `surface`, and `collisions` are `null`
- `wheels=[]`
- `attachments=[]`

The receiver treats that as a valid no-vehicle state and emits no gameplay FFB.

## Units

- `massT`, `totalMassT`: metric tonnes.
- `speedMps`: meters per second.
- `speedKmh`: kilometers per hour for UI and profile thresholds.
- `localAccelerationMps2`: vehicle-local acceleration in meters per second squared.
- `yawRateRadPerSec`: radians per second.
- `steering.angle`: normalized/raw steering angle from FS25 source data.
- `steering.rate`: steering angle delta per second.

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

## Manual File Fallback Test

```powershell
$path = "$env:USERPROFILE\Documents\My Games\FarmingSimulator2025\modSettings\FS25_RealFfbTelemetry\telemetry.json"
New-Item -ItemType Directory -Force -Path (Split-Path $path) | Out-Null
Get-Content samples/telemetry-packets/milestone2-valid.json -Raw | Set-Content -Encoding UTF8 $path
```
