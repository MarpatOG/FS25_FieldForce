# Telemetry Protocol

The telemetry mod uses UDP JSON over localhost when FS25 Lua socket support is available. If socket support is unavailable, the Lua mod writes the same JSON packet to a file in `modSettings`, and the Windows app reads that file.

## Transport

- Protocol: UDP
- Default host: `127.0.0.1`
- Default port: `34325`
- Sender: FS25 Lua mod
- Receiver: Windows app
- Default UDP target send rate: `125 Hz`
- File fallback target write rate: `30 Hz`
- File fallback: `Documents/My Games/FarmingSimulator2025/modSettings/FS25_RealFfbTelemetry/telemetry.json`
- Effect status return file: `Documents/My Games/FarmingSimulator2025/modSettings/FS25_RealFfbTelemetry/effectStatus.json`

Packet loss is acceptable for UDP. The receiver always keeps the last valid packet visible and changes status to `Lost` when packets or file updates stop.
The Windows receiver reports UDP status, file fallback status, last valid packet source, parser status, and the latest transport error separately so a UDP bind failure does not hide file fallback diagnostics. Fresh UDP packets are the primary source; file fallback is accepted only before UDP is available, when UDP bind failed, or after UDP has timed out.

## Packet Shape

```json
{
  "timestamp": 123456,
  "gameState": "mission",
  "isPlayerInVehicle": true,
  "vehicleName": "Tractor",
  "vehicleType": "tractor",
  "vehicleCategory": "TractorWheeled",
  "wheelTireTypes": "street,mud",
  "wheelTireProfile": "mixed",
  "speedKmh": 12.4,
  "steeringAngle": 0.13,
  "steeringRate": 0.8,
  "rpm": 850,
  "engineStarted": true,
  "mass": 6200,
  "totalMass": 8800,
  "isOnField": false,
  "surfaceType": "field",
  "surfaceAttribute": 1,
  "groundWetness": 0.35,
  "rainScale": 0.2,
  "wheelSlip": 0.12,
  "maxWheelSlip": 0.24,
  "groundContactRatio": 1.0,
  "steeringGroundContactRatio": 1.0,
  "steeringWheelSlip": 0.18,
  "pitchDeg": 3.1,
  "rollDeg": -2.4,
  "yawRateDegPerSec": 8.5,
  "slopeDeg": 4.0,
  "localAccelerationX": 0.3,
  "localAccelerationY": 1.8,
  "localAccelerationZ": -0.6,
  "bumpImpulse": 0.42,
  "suspensionImpulse": 0.42,
  "leftSuspensionImpulse": 0.18,
  "rightSuspensionImpulse": 0.06,
  "throttle": 0.6,
  "brake": 0.0,
  "clutch": 0.0,
  "gear": 3
}
```

## Field Notes

- `timestamp`: FS game time when available, otherwise Lua clock.
- `vehicleType`: raw FS25 `typeName`/`typeDesc` value kept as a legacy/debug field.
- `vehicleCategory`: normalized category used by the Windows app to select a full category effect profile. Values are `TractorWheeled`, `TractorTracked`, `HeavyTractorWheeled`, `HeavyTractorTracked`, `Harvester`, `Truck`, `LoaderTelehandler`, `LightVehicle`, and `Unknown`.
- `wheelTireTypes`: comma-separated unique FS25 wheel tire type names read from `wheel.physics.tireType` through `WheelsUtil.getTireTypeName(...)` when available, for example `street`, `mud`, `offRoad`, or `crawler`.
- `wheelTireProfile`: normalized tire profile: `street`, `agricultural`, `mixed`, `tracked`, or `unknown`.
- `speedKmh`: in-game vehicle speed in km/h. The Lua mod primarily derives this from rootNode world-position delta with a `2 km/h` standstill deadband scaled by sample `dt`, so FFB speed effects do not react to stale API spikes or physics jitter while parked. When rootNode position exists but there is not enough history yet, speed is reported as `0`. Raw game speed fields are used only as fallbacks when rootNode position is unavailable and are not multiplied by `3600`.
- `steeringAngle`: first available vehicle or wheel steering value.
- `steeringRate`: steering delta per second for the same vehicle, derived from consecutive `steeringAngle` samples when the sample gap is valid.
- `rpm`: best-effort motor RPM.
- `engineStarted`: best-effort motor running state from vehicle/motorized APIs.
- `mass` and `totalMass`: best-effort vehicle mass values.
- `isOnField`: legacy compatibility field; new surface logic prefers `surfaceType`.
- `surfaceType`: strict exact surface label. Supported exact labels are `asphalt`, `field`, `wetField`, `grass`, `shallowWater`, `snow`, `dirt`, `gravel`, `mud`, and `unknown`. `dirt`, `gravel`, and `mud` are emitted only if FS25 returns those exact names or an exact wheel surface sound mapping for the raw terrain attribute.
- `surfaceAttribute`: raw terrain attribute number. It is not mapped to dirt/gravel/mud by guesswork.
- `groundWetness` and `rainScale`: best-effort normalized `0..1` weather values when available.
- `wheelSlip`, `maxWheelSlip`, and `groundContactRatio`: aggregated wheel physics values. `wheelSlip` is average wheel slip, `maxWheelSlip` is the maximum wheel slip, and `groundContactRatio` is contacted wheels divided by wheel count.
- `steeringWheelSlip` and `steeringGroundContactRatio`: steering-wheel-specific slip and contact values. The Windows app prefers these for steering-load decisions and falls back to all-wheel values.
- `pitchDeg`, `rollDeg`, `yawRateDegPerSec`, and `slopeDeg`: vehicle attitude and terrain slope values.
- `localAccelerationX/Y/Z`: acceleration in vehicle-local axes when enough motion history is available.
- `bumpImpulse` and `suspensionImpulse`: normalized vertical impulse derived from local acceleration. The current sender writes the same value to both fields.
- `leftSuspensionImpulse` and `rightSuspensionImpulse`: side-specific best-effort suspension impulse fields. The sender prefers wheel suspension/load fields when available, and falls back to distributing `bumpImpulse` by left/right wheel contact ratio.
- `throttle`, `brake`, `clutch`, and `gear`: best-effort drivetrain/control fields. The Windows calculator uses transitions in these fields for drivetrain event pulses.
- Missing values are sent as `null`.

## Vehicle Categories

The Lua mod classifies vehicles from FS25 `vehicle.typeName` and `vehicle.typeDesc` with token/alias matching, so `roadTractor` and `semiTruck` are treated as road trucks instead of matching the generic tractor token. Truck aliases include `truck`, `trucks`, `semiTruck`, `roadTractor`, `lkw`, and `semi truck`. Tractor categories are split into wheeled/tracked variants by the FS25 `Crawlers` specialization data (`vehicle.spec_crawlers.crawlers` and wheel-configuration crawler tables). GIANTS documents `Crawlers` as the specialization for crawlers and tracks with rotating or scrolling elements: https://gdn.giants-software.com/documentation_scripting_fs25.php?category=77&class=655&version=script

If a raw truck category uses agricultural or tracked tire profiles from FS25 wheel physics, the Lua mod temporarily emits `TractorWheeled` or `TractorTracked` respectively. Model names such as Volvo, Mack, Unimog, or Heizomat are not parsed.

Heavy tractor detection uses explicit raw type/category names such as large/heavy tractor equivalents. Mass is not used as the primary criterion, and model names are not parsed. If the raw type/category data is missing or unexpected, the mod emits `Unknown`.

## Receiver States

- `Waiting`: app is listening, no packet has arrived.
- `Connected`: packets are arriving within the timeout.
- `Lost`: at least one packet arrived, then packets stopped for more than the configured timeout.

Malformed JSON updates the raw packet preview and parser status, but it does not replace the last valid decoded packet.
If the UDP port is already in use, the app reports the bind error and continues watching the fallback file.

## Effect Status Return File

The Windows app writes `effectStatus.json` at up to 10 Hz, plus immediate zero-status writes on Stop All, telemetry loss, and shutdown/dispose. The in-game overlay reads this file and marks all lamps stale/red if the status timestamp is older than one second or the file is unavailable.

```json
{
  "timestampMs": 1715000000000,
  "activeCategory": "Truck",
  "activeEffectsText": "Spring, Damper",
  "speedSpring": true,
  "speedDamper": true,
  "friction": false,
  "rpmVibration": false,
  "surfaceFeedback": false,
  "slipFeedback": false,
  "bump": false,
  "steeringLoad": true,
  "speedStability": true,
  "surfaceTraction": false,
  "suspensionTerrain": false,
  "loadSlopeImplement": true,
  "engineDrivetrain": false
}
```

## Manual UDP Test

PowerShell example:

```powershell
$udp = [System.Net.Sockets.UdpClient]::new()
$endpoint = [System.Net.IPEndPoint]::new([System.Net.IPAddress]::Parse("127.0.0.1"),34325)
$json = Get-Content samples/telemetry-packets/milestone2-valid.json -Raw
$bytes = [Text.Encoding]::UTF8.GetBytes($json)
[void]$udp.Send($bytes,$bytes.Length,$endpoint)
$udp.Dispose()
```

## Manual File Fallback Test

PowerShell example:

```powershell
$path = "$env:USERPROFILE\Documents\My Games\FarmingSimulator2025\modSettings\FS25_RealFfbTelemetry\telemetry.json"
New-Item -ItemType Directory -Force -Path (Split-Path $path) | Out-Null
Get-Content samples/telemetry-packets/milestone2-valid.json -Raw | Set-Content -Encoding UTF8 $path
```
