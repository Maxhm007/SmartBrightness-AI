# Changelog

v0.4.0 - 19-Nov-2026 - 09:04 AM - Background Windows updates

- Added startup and six-hour update checks against the public GitHub Releases feed while the app is running.
- Downloads only a newer versioned installer over HTTPS and verifies its SHA-256 digest against GitHub release metadata before silent per-user installation.
- Enabled graceful app close/restart during updates and added a GitHub Actions workflow to build and publish installers for `vX.Y.Z` tags.
- Built the 0.4.0 self-contained installer locally; future update installation has not yet been verified on a second machine.

v0.3.0 - 19-Nov-2026 - 12:37 AM - Transparent tray app and automatic sensors

- Added adjustable window transparency and close/minimize to the notification area with reopen, pause, and exit actions.
- Added on-demand webcam and per-display screen sampling plus a one-minute auto-brightness loop using the existing WMI adapter.
- Kept automatic mode off at launch; sensor data stays local and image buffers are discarded after each measurement.
- Added damped screen-content recommendations and time-of-day input; all seven core tests pass.
- Built a self-contained x64 installer and verified temporary installation, app startup, and uninstall. Actual automatic brightness writes remain unverified.

v0.2.1 - 16-Nov-2026 - 10:42 AM - Windows x64 installation package

- Added a repeatable self-contained x64 publish and Inno Setup installer workflow.
- Built `SmartBrightnessAI-Setup-0.2.0.exe` and verified temporary install/uninstall; no administrator rights were required in the check.
- Installer remains unsigned; SmartScreen behavior and installation on a second machine are unverified.

v0.2.0 - 16-Nov-2026 - 09:33 AM - Internal display control and local preferences

- Added supported internal-display detection and manual brightness setting through the Windows WMI provider.
- Added validated local settings persistence and numeric-only activity history under LocalAppData.
- Rebuilt the WPF app with zero warnings and errors; all five core tests pass. Physical display behavior remains unverified.

v0.1.1 - 16-Nov-2026 - 09:30 AM - Brightness engine hysteresis and verification

- Corrected hysteresis to ignore small sensor fluctuations while allowing smoothed transitions to converge.
- Built the WPF app and passed all five core engine tests using the local .NET 8 SDK.

v0.1.0 - 16-Nov-2026 - 09:11 AM - Initial application foundation

- Added .NET 8 WPF shell, platform-independent brightness fusion engine, and deterministic engine tests.
- Recorded sensor and hardware-control integration as future work; no hardware behavior is claimed.

v0.0.0 - 16-Nov-2026 - 09:11 AM - Workspace audit

- Recorded the initial workspace state. No application source or buildable project was present.
