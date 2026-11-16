# SmartBrightness AI — Project Summary

## Purpose

SmartBrightness AI is intended to be a lightweight, local-first Windows 10/11 x64 brightness controller built with C# and .NET 8 WPF. The planned controller combines webcam light estimates, screen luminance, time-of-day preferences, hardware capability detection, and user controls.

## Architecture

- `src/SmartBrightness.Core` contains platform-independent sensor fusion and brightness policy.
- `src/SmartBrightness.App` is the Windows-only WPF shell.
- `tests/SmartBrightness.Core.Tests` contains deterministic engine tests.
- Sensor acquisition and display-control adapters will be isolated behind interfaces so unsupported devices can fail safely.

The GitHub repository was confirmed empty before initialization. This is an initial foundation, not a completed hardware-integrated release.

## Setup

Requires the .NET 8 SDK and Windows. Run `dotnet build SmartBrightness.sln` and `dotnet test SmartBrightness.sln` from the repository root. WPF execution and hardware integration require Windows.

## Verified state

- Workspace inspected on 16-Nov-2026.
- Repository source and initial architecture added on 16-Nov-2026.
- A .NET 8 SDK was installed locally under the ignored `.dotnet` folder for verification.
- `dotnet build src/SmartBrightness.App/SmartBrightness.App.csproj` succeeded with zero warnings and errors.
- `dotnet test SmartBrightness.sln` passed all 5 core tests.
- WPF interactive execution, Windows hardware behavior, webcam access, display control, and packaging are currently unverified.
