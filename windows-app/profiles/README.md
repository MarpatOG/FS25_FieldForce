# Device Profiles

Built-in wheel profiles live in code in `windows-app/src/App/Models/FfbPipelineModels.cs` under `WheelProfileCatalog`.

MVP built-ins:

- Logitech MOMO Racing Wheel
- Logitech Driving Force GT
- Logitech Driving Force Pro
- Logitech Driving Force EX
- Logitech G25
- Logitech G27
- Logitech G29
- Logitech G920
- Logitech G923
- Generic FFB Wheel

Runtime user effect profiles are saved outside the repository under:

```text
%APPDATA%/FieldForce/effect-profiles/<wheel-profile-id>.json
```

The stable wheel profile id is used for filenames, so DirectInput display-name changes do not create a new tuning file.
