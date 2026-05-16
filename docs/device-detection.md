# Device Detection

The Windows app scans attached DirectInput game-control devices through `Vortice.DirectInput`.

For each detected device it shows:

- Product or instance name.
- Stable ID made from product GUID, instance GUID, and product name.
- DirectInput device type.
- Reported axes.
- Reported force feedback capability.
- Supported effect names when DirectInput exposes them.

The selected device stable ID and resolved wheel profile id are saved to `%APPDATA%/FS25FFBBridge/config.json`. On the next launch, the app auto-selects the device only when the same stable ID is present.

Built-in wheel profiles are resolved from DirectInput product/instance aliases for Logitech MOMO Racing Wheel, Driving Force GT/Pro/EX, G25, G27, G29, G920, and G923. Unknown devices use `Generic FFB Wheel`.

If the saved wheel is missing, the app stays idle and does not attempt to send forces.

## Safety Behavior

- Selecting a new device stops all active effects first.
- Rescanning with a missing selected device triggers a safety stop.
- Non-FFB devices can be inspected but test effect commands remain disabled.
- DirectInput acquisition failures are logged and leave the backend inactive.

## Logitech Notes

The first confirmed hardware baseline is `Logitech MOMO Racing`. The wheel reports one force feedback actuator on the wheel axis at DirectInput offset `0`, supports condition, constant, periodic, ramp, and custom effects, and works with Cartesian force directions `-10000/+10000`.
