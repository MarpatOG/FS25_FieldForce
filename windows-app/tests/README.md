# Tests

Automated tests live in `FieldForce.App.Tests`.

Run from the repository root:

```powershell
dotnet test windows-app/tests/FieldForce.App.Tests/FieldForce.App.Tests.csproj
```

Current coverage focuses on telemetry receiver transport behavior: file receive, hidden UDP receive, invalid JSON handling, lost timeout, and UDP bind failure diagnostics.
