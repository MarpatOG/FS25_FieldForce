# FS25 Mod Versioning

Current FS25 telemetry mod version: `0.5.0.5`

The version above must match `fs25-mod/modDesc.xml`:

```xml
<version>0.5.0.5</version>
```

## Required Rule

Any change to files shipped inside `fs25-mod/` must either:

- bump the FS25 mod version in both this file and `fs25-mod/modDesc.xml`; or
- explicitly state in the commit message why the change does not affect the shipped mod package.

Telemetry protocol changes, Lua behavior changes, config defaults, overlay changes, and `modDesc.xml` changes always require a version bump.

## Version Format

Use GIANTS-style four-part versions:

```text
major.minor.patch.build
```

- `major`: breaking install/package identity or protocol compatibility change.
- `minor`: new telemetry fields, overlay features, or user-visible behavior.
- `patch`: bug fixes that preserve behavior and protocol compatibility.
- `build`: packaging-only or documentation-only shipped-mod updates.
