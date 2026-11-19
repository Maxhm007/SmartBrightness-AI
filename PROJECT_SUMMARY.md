# SmartBrightness AI — Project Summary

## Purpose

SmartBrightness AI is a local-first Windows 10/11 x64 brightness controller built with C# and .NET 8 WPF. The current app combines a webcam light estimate, screen pixel-luminance estimate, a simple time-of-day preference, and supported internal-display brightness control.

## Architecture

- `src/SmartBrightness.Core` contains the platform-independent weighted fusion, smoothing, hysteresis, and bounded brightness policy.
- `src/SmartBrightness.App` contains the WPF shell, local settings/history, Windows WMI internal-display adapter, sensor samplers, app opacity, and notification-area lifecycle.
- Webcam frames are sampled with OpenCvSharp only while automatic mode is enabled. Camera handles and image buffers are disposed after each sample.
- Screen content uses a 48 × 36 in-memory sample for each connected display, excluding the app window region; the sample buffer is discarded after computing a numeric value.
- Numeric measurements and brightness history are stored under `%LOCALAPPDATA%\SmartBrightnessAI`; images, screenshots, and telemetry are not stored or uploaded.
- Automatic mode starts only after the user turns it on and a WMI-compatible internal display is detected. Sampling runs once per minute and stops when auto mode is turned off or the process exits.
- Closing or minimizing hides the main window and leaves the app in the notification area. The tray menu can reopen it or exit.
- `tests/SmartBrightness.Core.Tests` contains deterministic sensor-fusion tests.
- `scripts/build-windows-installer.ps1` publishes the app self-contained for x64 and compiles the per-user Inno Setup installer.
- `BackgroundUpdateService` checks public GitHub Releases at launch and every six hours while running, downloads only the installer matching a newer semantic version, verifies GitHub's SHA-256 release-asset digest, then launches a silent per-user upgrade. Inno Setup uses Windows Restart Manager to close and restart the app around file replacement.
- `.github/workflows/release-windows.yml` builds and publishes the installer when a `vX.Y.Z` tag is pushed.

Sensor weights, camera exposure mapping, screen-content bias, and time-of-day values are provisional. The webcam is not a lux meter and the screen sample is not measured physical panel luminance.

## Setup

Requires Windows and the .NET 8 SDK to build. Run `dotnet build SmartBrightness.sln` and `dotnet test SmartBrightness.sln`. For an installer, install Inno Setup 7 and run `scripts/build-windows-installer.ps1`.

## Verified state

- Full solution build succeeded with zero warnings and errors after the current changes.
- All 7 core tests passed.
- This Windows machine returned numeric webcam and screen samples from a one-shot local probe.
- A close-message check confirmed the app window hides while its process remains running; it was then stopped for cleanup.
- The self-contained installer successfully installed to a temporary directory, launched the app window, uninstalled successfully, and removed that directory.
- The 0.4.0 app and installer build succeeded locally with zero warnings and errors; the v0.4.0-to-future-version update path has not yet been exercised on a separate installed machine.
- The WMI provider detected a supported internal display, but changing physical brightness from the new automatic loop has not been hardware-verified.
- Visual opacity quality, Windows sleep/resume, multi-monitor protected content, and installation on another machine remain unverified.
- Installer is unsigned; SmartScreen and code-signing behavior remain unverified.
