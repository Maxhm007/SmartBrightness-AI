# SmartBrightness AI

A local-first Windows brightness utility. This version includes manual brightness for internal displays exposed by Windows WMI, automatic brightness using webcam, screen-content, and time-of-day inputs, an adjustable transparent window, and close-to-notification-area behavior.

## Install

Run `SmartBrightnessAI-Setup-0.3.0.exe`. It installs per-user under `%LOCALAPPDATA%\Programs\SmartBrightnessAI`, creates a Start menu shortcut, and offers an optional desktop shortcut. It is self-contained, so a separate .NET runtime install is not needed. Uninstall preserves settings and numeric history in `%LOCALAPPDATA%\SmartBrightnessAI`.

The development installer is unsigned and may trigger a Windows SmartScreen warning. Code signing has not been configured.

## Use

- Adjust **Window transparency** between 60% and 100%.
- Choose **Automatic on** to enable sensing. It checks for a camera, then samples room light and screen content once per minute. Camera frames and sampled screen pixels are disposed after measurement.
- Choose **Automatic off** to stop both sensors. Automatic mode remains off after restart until enabled again.
- Close or minimize the window to keep SmartBrightness AI available from its notification-area icon. Choose **Exit** in the tray menu to stop the process.
- Manual brightness works only when Windows exposes a compatible internal display through WMI. External monitors are not yet controlled.

If the webcam is unavailable or permission is denied, automatic mode uses the remaining screen and time inputs. Allow desktop apps to access the camera in Windows privacy settings. The camera value is affected by automatic exposure and is only a rough room-light proxy.

## Build and tests

Requires Windows and the .NET 8 SDK.

```powershell
dotnet build SmartBrightness.sln
dotnet test SmartBrightness.sln
```

To build the Windows x64 installer, install Inno Setup 7 and run:

```powershell
.\scripts\build-windows-installer.ps1
```

The installer is generated at `artifacts/windows/installer/SmartBrightnessAI-Setup-0.3.0.exe`.

## Known limitations

- WMI control is limited to compatible internal displays; DDC/CI for external monitors is not implemented.
- Webcam pixel brightness is not calibrated to lux. Automatic exposure can change its reading.
- Screen pixel luminance is a content estimate, not measured panel output. Its influence is deliberately limited to reduce readability changes on dark content.
- Sensor weights, time profile, and adjustment interval are initial values and need broader hardware testing.
- Camera selection/calibration, manual-override expiration, battery profiles, tray startup, start-with-Windows, and portable ZIP packaging remain future work.
- Actual automatic brightness writes, sleep/resume, and behavior on other Windows devices remain unverified.
