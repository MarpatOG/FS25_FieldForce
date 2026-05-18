# Device Detection

The Windows app scans attached DirectInput game-control devices through `Vortice.DirectInput`.

For each detected device it shows:

- Product or instance name.
- Stable ID made from product GUID, instance GUID, and product name.
- DirectInput device type.
- Reported axes.
- Reported force feedback capability.
- Supported effect names when DirectInput exposes them.

The selected device stable ID and resolved wheel profile id are saved to `%APPDATA%/FieldForce/config.json`. On the next launch, the app auto-selects the device only when the same stable ID is present.

Built-in wheel profiles are resolved from DirectInput product/instance aliases for Logitech Momo Racing, Driving Force GT/Pro/EX, G25, G27, G29, G920, and G923. Unknown devices use `Generic FFB Wheel`.

The primary force feedback axis is auto-detected from DirectInput axis and actuator offsets. Logitech HID devices with VID `046D` use DirectInput X axis offset `0` as a hard fallback, even when legacy Logitech drivers enumerate axes without `DIDOI_FFACTUATOR`. Other recognized wheels prefer DirectInput X axis offset `0` when it is enumerated. Set `"PrimaryFfbAxisOffset": 0` in `config.json` to force an axis manually. `null` or an omitted field keeps auto-detection enabled.

If the saved wheel is missing, the app stays idle and does not attempt to send forces.

## Safety Behavior

- Selecting a new device stops all active effects first.
- Rescanning with a missing selected device triggers a safety stop.
- Non-FFB devices can be inspected but test effect commands remain disabled.
- DirectInput acquisition failures are logged and leave the backend inactive.

## Logitech Notes

The first confirmed hardware baseline is `Logitech MOMO Racing`. The wheel reports one force feedback actuator on the wheel axis at DirectInput offset `0`, supports condition, constant, periodic, ramp, and custom effects, and works with Cartesian force directions `-10000/+10000`.

Logitech G25/G27-era legacy driver stacks can register `WmJoyFrc.dll` as the force feedback driver while Vortice/DirectInput object enumeration reports no force feedback actuators. For Logitech VID `046D`, the app now tries explicit offset `0` first and retries effect creation with implicit axes (`cAxes = 0`) if DirectInput rejects explicit axis parameters with `DIERR_INVALIDPARAM`.
