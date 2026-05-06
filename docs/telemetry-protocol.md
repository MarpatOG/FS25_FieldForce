# Telemetry Protocol

The telemetry mod uses UDP JSON over localhost when FS25 Lua socket support is available. If socket support is unavailable, the Lua mod writes the same JSON packet to a file in `modSettings`, and the Windows app reads that file.

## Transport

- Protocol: UDP
- Default host: `127.0.0.1`
- Default port: `34325`
- Sender: FS25 Lua mod
- Receiver: Windows app
- Default send rate: `30 Hz`
- File fallback: `Documents/My Games/FarmingSimulator2025/modSettings/FS25_RealFfbTelemetry/telemetry.json`

Packet loss is acceptable for UDP. The receiver always keeps the last valid packet visible and changes status to `Lost` when packets or file updates stop.
The Windows receiver reports UDP status, file fallback status, last valid packet source, parser status, and the latest transport error separately so a UDP bind failure does not hide file fallback diagnostics.

## Packet Shape

```json
{
  "timestamp": 123456,
  "gameState": "mission",
  "isPlayerInVehicle": true,
  "vehicleName": "Tractor",
  "vehicleType": "tractor",
  "speedKmh": 12.4,
  "steeringAngle": 0.13,
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
  "pitchDeg": 3.1,
  "rollDeg": -2.4,
  "yawRateDegPerSec": 8.5,
  "slopeDeg": 4.0,
  "localAccelerationX": 0.3,
  "localAccelerationY": 1.8,
  "localAccelerationZ": -0.6,
  "bumpImpulse": 0.42
}
```

## Field Notes

- `timestamp`: FS game time when available, otherwise Lua clock.
- `steeringAngle`: best-effort normalized/vehicle steering value for Milestone 2.
- `rpm`: best-effort motor RPM.
- `mass` and `totalMass`: best-effort vehicle mass values.
- `isOnField`: legacy compatibility field; new surface logic prefers `surfaceType`.
- `surfaceType`: strict exact surface label. Supported exact labels are `asphalt`, `field`, `wetField`, `grass`, `shallowWater`, `snow`, `dirt`, `gravel`, `mud`, and `unknown`. `dirt`, `gravel`, and `mud` are emitted only if FS25 returns those exact names.
- `surfaceAttribute`: raw terrain attribute number. It is not mapped to dirt/gravel/mud by guesswork.
- `groundWetness` and `rainScale`: best-effort normalized `0..1` weather values when available.
- `wheelSlip`, `maxWheelSlip`, and `groundContactRatio`: aggregated wheel physics values.
- `pitchDeg`, `rollDeg`, `yawRateDegPerSec`, and `slopeDeg`: vehicle attitude and terrain slope values.
- `localAccelerationX/Y/Z`: acceleration in vehicle-local axes when enough motion history is available.
- `bumpImpulse`: normalized vertical impulse derived from local acceleration.
- Missing values are sent as `null`.

## Receiver States

- `Waiting`: app is listening, no packet has arrived.
- `Connected`: packets are arriving within the timeout.
- `Lost`: at least one packet arrived, then packets stopped for more than the configured timeout.

Malformed JSON updates the raw packet preview and parser status, but it does not replace the last valid decoded packet.
If the UDP port is already in use, the app reports the bind error and continues watching the fallback file.

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
