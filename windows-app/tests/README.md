# Tests

Automated tests live in `FS25FfbBridge.App.Tests`.

Run from the repository root:

```powershell
dotnet test windows-app/tests/FS25FfbBridge.App.Tests/FS25FfbBridge.App.Tests.csproj
```

Current coverage focuses on telemetry receiver transport behavior: UDP receive, file fallback receive, invalid JSON handling, lost timeout, and UDP bind failure diagnostics.
