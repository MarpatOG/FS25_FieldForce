# Windows App

Avalonia/.NET 8 desktop app for the FieldForce App. It provides DirectInput device scanning, Logitech wheel profile detection, safe test effects, telemetry reception, gameplay FFB calculation, and live effect status output.

Run from the repository root:

```powershell
dotnet run --project windows-app/src/App/FieldForce.App.csproj
```

Build the self-contained Windows app zip from the repository root:

```powershell
.\scripts\Build-Artifacts.ps1
```

The compiled app archive is written to `artifacts/FieldForceApp-win-x64.zip`.
