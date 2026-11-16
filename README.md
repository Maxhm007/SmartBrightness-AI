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

## Build the Windows installer

On Windows, install the .NET 8 SDK and Inno Setup 7, then run:

```powershell
.\scripts\build-windows-installer.ps1
```

The script publishes a self-contained x64 app and creates `artifacts/windows/installer/SmartBrightnessAI-Setup-0.2.0.exe`. The installer is per-user and does not require administrator privileges. It creates a Start menu shortcut and offers an optional desktop shortcut. Uninstall leaves the user's settings and numeric history under `%LOCALAPPDATA%\SmartBrightnessAI` intact.

This unsigned development installer may show a Windows SmartScreen warning. Code signing is not configured.

## Current limitations

Manual brightness control is available only when Windows exposes a supported internal monitor through WMI. External monitor DDC/CI control is not implemented. Automatic mode remains disabled until sensor sources and their sampling lifecycle are implemented. Core engine inputs are normalized to the 0–100 brightness scale; sensor adapters must map their measurements to that scale and apply product-specific calibration. The initial weights are provisional.

No webcam image, screenshot, or telemetry feature is implemented. Webcam/screen sensing and automatic control are not available. Hardware behavior, installer execution on a second Windows machine, code signing, and SmartScreen behavior have not been verified.
