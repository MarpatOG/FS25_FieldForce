# FS25 Mod Installation

## Install Folder

Create this folder:

```text
Documents/My Games/FarmingSimulator2025/mods/FS25_RealFfbTelemetry
```

Copy the contents of repository `fs25-mod/` into it:

```text
FS25_RealFfbTelemetry/
  modDesc.xml
  config/
    TelemetryConfig.lua
  src/
    FS25RealFfbTelemetry.lua
```

Do not copy the parent repository folder itself. `modDesc.xml` must be directly inside `FS25_RealFfbTelemetry`.

## Configure

Default config is in `config/TelemetryConfig.lua`:

```lua
FS25RealFfbTelemetryConfig = {
    host = "127.0.0.1",
    port = 34325,
    updateRateHz = 30,
    debug = false,
    fileFallback = true,
    fileName = "telemetry.json",
    overlay = {
        enabled = true,
        x = 0.015,
        y = 0.965,
        fontSize = 0.014,
        lineHeight = 0.018,
        maxRawLength = 120
    }
}
```

Keep `debug=false` for normal use.
Set `debug=true` only while diagnosing transport issues; the mod will then print sent UDP/file payloads and extra file write details.
Set `overlay.enabled=false` if the in-game debug overlay is not needed.

## Run

1. Launch the Windows app.
2. Open the `Telemetry` tab and confirm it shows `Waiting`.
3. Launch FS25.
4. Enable `FS25 Real FFB Telemetry` in the savegame.
5. Enter a vehicle.
6. Confirm the Windows app changes to `Connected`.

If UDP sockets are unavailable in FS25 Lua, the mod logs the `require("socket")` error, `package.path`, `package.cpath`, host, and port, then continues without crashing the game.
In that case, file fallback is used automatically when possible:

```text
Documents/My Games/FarmingSimulator2025/modSettings/FS25_RealFfbTelemetry/telemetry.json
```

If file fallback also fails, check the FS25 log for the exact missing API or filesystem failure, such as unavailable `io.open`, unavailable `getUserProfileAppPath`, folder creation failure, or atomic rename failure.
