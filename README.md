# SmartBrightness AI

A local-first Windows brightness utility under active development. The repository contains a WPF dashboard, a platform-independent sensor-fusion engine, manual WMI brightness control for supported internal displays, and local numeric-only settings/history. Webcam sensing, screen analysis, DDC/CI, automatic adjustment, tray integration, and packaging are not implemented yet.

## Requirements

- Windows 10 or 11 x64 to run the WPF application.
- .NET 8 SDK to build and test.

## Build and test

```powershell
dotnet build SmartBrightness.sln
dotnet test SmartBrightness.sln
```

## Current limitations

Manual brightness control is available only when Windows exposes a supported internal monitor through WMI. External monitor DDC/CI control is not implemented. Automatic mode remains disabled until sensor sources and their sampling lifecycle are implemented. Core engine inputs are normalized to the 0–100 brightness scale; sensor adapters must map their measurements to that scale and apply product-specific calibration. The initial weights are provisional.

No webcam image, screenshot, or telemetry feature is implemented. Hardware support, Windows execution, installer behavior, code signing, and SmartScreen behavior have not been verified.
