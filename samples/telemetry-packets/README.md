# Telemetry Packets

`milestone2-valid.json` is a sample UDP JSON packet for testing the Windows telemetry receiver without FS25.

Calibration packets:

- `calibration-asphalt-10kmh.json`
- `calibration-asphalt-30kmh.json`
- `calibration-field-10kmh.json`
- `calibration-field-25kmh.json`
- `calibration-wet-field.json`
- `calibration-heavy-implement.json`
- `calibration-high-slip.json`
- `calibration-bump-landing.json`

Regression packets:

- `regression-position-speed-spike-filtered.json`: stable FS25 speed after a simulated position-derived speed spike; acceleration and collision impulses stay suppressed for the noisy frame.
