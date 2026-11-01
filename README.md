# Timer

A cross-platform timer application built with Avalonia UI for Windows and Android. This repository is a portfolio project in progress.

## Project status

The shared Avalonia application and both platform entry points build successfully. The current screen is the starter view. Timer modes and the finished interface are planned for the next development steps.

Planned modes:

- Stopwatch
- Count up to a target duration and countdown from a target duration
- Interval timer with configurable work, rest, and rounds

The finished interface will use English labels and a neutral, warm palette.

## Technology

- .NET 10
- Avalonia UI 12.1.4
- CommunityToolkit.Mvvm
- Android 6.0 (API 23) or later

## Build and run

Install the .NET 10 SDK. To build the Android project, also install the .NET Android workload and Android SDK.

```powershell
dotnet restore Timer.sln
dotnet build Timer.sln -c Debug
dotnet run --project Timer.Desktop
```

The Android project can be built separately:

```powershell
dotnet build Timer.Android/Timer.Android.csproj -c Debug
```

To run on Android, select the `Timer.Android` project and a connected device or emulator in an IDE with Android tooling.

## Repository layout

| Path | Purpose |
| --- | --- |
| `Timer/` | Shared Avalonia UI and view models |
| `Timer.Desktop/` | Windows desktop entry point |
| `Timer.Android/` | Android entry point and resources |

The application currently uses the template icons; original branding will be added with the finished interface.
