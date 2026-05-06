# FS25 Real FFB Telemetry Mod

Milestone 2 telemetry sender for Farming Simulator 25.

## Install

Copy the contents of this `fs25-mod` folder into a mod folder named:

```text
FS25_RealFfbTelemetry
```

Place that folder in:

```text
Documents/My Games/FarmingSimulator2025/mods/
```

The resulting layout should be:

```text
FS25_RealFfbTelemetry/
  modDesc.xml
  config/
  src/
```

Enable `FS25 Real FFB Telemetry` in a savegame. Start the Windows bridge before or after FS25; if the bridge is not running, the mod must not crash the game.

## Config

Edit `config/TelemetryConfig.lua`:

- `host`: default `127.0.0.1`
- `port`: default `34325`
- `updateRateHz`: default `30`
- `debug`: default `false`

## Transport Diagnostics

The mod tries UDP first through `require("socket")`. If FS25 does not expose LuaSocket, the log includes the `require` error plus `package.path` and `package.cpath`, then the mod tries the file fallback.

File fallback writes the same JSON packet to:

```text
Documents/My Games/FarmingSimulator2025/modSettings/FS25_RealFfbTelemetry/telemetry.json
```

If file fallback cannot start, the log reports the exact missing API or filesystem step, such as `io.open`, `getUserProfileAppPath`, `createFolder`, or `os.rename`.
