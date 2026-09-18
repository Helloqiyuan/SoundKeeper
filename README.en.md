# SoundKeeper

[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)
[![.NET](https://img.shields.io/badge/.NET-10.0-512BD4.svg)](https://dotnet.microsoft.com/)
[![Platform](https://img.shields.io/badge/platform-Windows-0078D6.svg)](#requirements)
[![Release](https://img.shields.io/badge/release-v1.0.0-brightgreen.svg)](CHANGELOG.md)

> A lightweight Windows tray utility that keeps Bluetooth headphones awake by
> continuously playing an **inaudible** audio stream, preventing them from
> entering sleep mode or disconnecting during long periods of silence.

No window, no taskbar entry, no administrator privileges. Just double-click and go.

English | [简体中文](README.md)

---

## Table of Contents

- [The Problem](#the-problem)
- [Features](#features)
- [Quick Start](#quick-start)
- [How It Works](#how-it-works)
- [Requirements](#requirements)
- [Build from Source](#build-from-source)
- [Configuration Storage](#configuration-storage)
- [Customizing Signal Strength](#customizing-signal-strength)
- [FAQ](#faq)
- [Project Structure](#project-structure)
- [Contributing](#contributing)
- [License](#license)

---

## The Problem

Many Bluetooth headphones (especially ANC models) enter sleep mode to save power
after a period without audio data. The symptoms are familiar: you have to play
something to "wake" them, or they disconnect entirely with noticeable latency.

The common workaround — looping a silent audio file — is unreliable because:

- Some Bluetooth stacks and headphone firmware treat **all-zero silent frames** as
  idle and therefore do not reset the sleep timer;
- Looping an audio file introduces decoding, resampling, and gaps that can actually
  cause the stream to break.

SoundKeeper instead **generates strictly non-zero, extremely low-amplitude samples
in-process** and pushes them directly to the audio device. The audio session stays
alive, and the headphone's sleep timer is continuously reset.

## Features

- 🎧 **Continuous keep-alive** — streams ~-90 dBFS non-zero samples to the default
  output device; completely inaudible to the human ear
- 🖱️ **Tray-only operation** — start/stop from the context menu, double-click to
  toggle; no windows at all
- 🚀 **Auto-start on boot** — one click, writes to the `HKCU` registry with
  **no administrator privileges and no UAC prompt**
- 💾 **State memory** — remembers the last keep-alive state and restores it on launch
- 🔄 **Hot-plug recovery** — automatically rebuilds the stream after your headphones
  disconnect/reconnect or the default device changes
- 🔒 **Single instance** — no duplicate tray icons
- 🎨 **Zero asset dependencies** — tray icons are drawn in code
  (green = running / grey = stopped), no image files needed
- 🔍 **Built-in self-test** — `--selftest` verifies the audio pipeline and registry I/O
- 📦 **Single-file release** — self-contained exe; no .NET runtime required on the target machine

## Quick Start

1. Download `SoundKeeper.exe` from [Releases](https://github.com/Helloqiyuan/SoundKeeper/releases).
2. Double-click it. A **grey dot** icon appears in the system tray (stopped state).
3. **Right-click** the icon:

   | Menu item | Description |
   | --- | --- |
   | **音频保活** (Keep-alive) | Checked = streaming (icon turns **green**); unchecked = stop and release the device |
   | **开机自动启动** (Auto-start) | Checked = register auto-start; unchecked = remove |
   | **退出** (Exit) | Stop the stream, clean up the tray icon, and exit |

4. Click **Keep-alive**; the icon turning green means it is active.

> Tip: **double-clicking** the tray icon toggles keep-alive as a shortcut.

For first-time setup, also enable **Auto-start** so the app silently enters the tray
on every boot.

> Note: the tray menu is currently in Simplified Chinese. Pull requests adding
> localization are welcome.

## How It Works

### Why it prevents headphone sleep

Bluetooth headphones sleep or disconnect after a period without audio data.
SoundKeeper continuously streams to the default output device, so the audio session
never goes away and the headphone's timer keeps getting reset, keeping the link active.

### Why you cannot hear it

The sample value is `1/32768` — the smallest value in the float range `[-1, 1]` —
which is roughly **-90 dBFS**, far below any audible threshold. The stream uses
**shared mode**, so it does not interfere with other applications playing audio.

### Why not just output silence

Some Bluetooth stacks and headphone firmware treat all-zero silent frames as idle
and therefore do not reset the sleep timer — this is exactly why many "play a silent
file" approaches fail. The output must be **strictly non-zero**.

### Key technical detail

The audio format must be normalized to pure `IeeeFloat`:

```csharp
// The device returns a WaveFormatExtensible (32-bit float wrapped in an
// extensible container). Passing it directly to WasapiOut throws
// "Must be already floating point". You must build a pure float format.
var mixFormat = device.AudioClient.MixFormat;
var format = WaveFormat.CreateIeeeFloatWaveFormat(mixFormat.SampleRate, mixFormat.Channels);
```

The sample rate and channel count come from the device's own `MixFormat`, which avoids
WASAPI inserting a resampling layer that could break the stream.

## Requirements

| Item | Requirement |
| --- | --- |
| OS | Windows 10 / 11 (x64) |
| Runtime | **Not required** (release is a self-contained single file) |
| Privileges | Standard user, **no administrator rights needed** |
| Dependency | [NAudio](https://github.com/naudio/NAudio) 2.2.1 (bundled) |

## Build from Source

Requires the **.NET SDK 10** (or 8+).

```bash
# Restore dependencies
dotnet restore -r win-x64

# Run in debug
dotnet run

# Publish as a self-contained single file
dotnet publish -c Release -r win-x64 --self-contained -p:PublishSingleFile=true
```

Output: `bin\Release\net10.0-windows\win-x64\publish\SoundKeeper.exe`

### Command-line arguments

| Argument | Description |
| --- | --- |
| (none) | Normal launch into the tray |
| `--silent` | Enter the tray silently (used by auto-start) |
| `--selftest` | Self-test: verifies the audio pipeline and registry I/O, writes `selftest.log` next to the exe, then exits |

To confirm the audio pipeline works after deployment:

```bash
SoundKeeper.exe --selftest
```

Check `selftest.log` — `RESULT: OK` means the core functionality is working.

## Configuration Storage

All settings live under the **current user** registry hive, which is why no
administrator rights are needed and no UAC prompt appears:

| Purpose | Registry location | Value |
| --- | --- | --- |
| Keep-alive state | `HKCU\Software\SoundKeeper` → `Enabled` | `1` / `0` |
| Auto-start | `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` → `SoundKeeper` | `"<exe path>" --silent` |

On launch the app reads `Enabled` and automatically resumes keep-alive if it was on.

> ⚠️ **Re-check auto-start after moving the exe**: the registry stores an absolute path,
> so moving the executable invalidates it. A balloon tip will warn you — uncheck and
> re-check the menu item to refresh the path.

## Customizing Signal Strength

The default amplitude is roughly -90 dBFS (`1/32768`), which is enough to keep the
link alive for the vast majority of Bluetooth headphones.

If your headphone firmware requires more signal energy (rare — the symptom is that the
headphones still sleep), change the provider construction in `AudioKeepAlive.cs`:

```csharp
// Default (-90 dBFS, completely inaudible)
new TinySampleProvider(format)

// Boosted (-60 dBFS, for firmware sensitive to signal energy)
new TinySampleProvider(format, TinySampleProvider.StrongAmplitude)
```

`TinySampleProvider` ships with two predefined amplitude constants:
`DefaultAmplitude` and `StrongAmplitude`.

## FAQ

<details>
<summary><b>My headphones still sleep after enabling keep-alive.</b></summary>

Possible causes:

1. **Your headphone firmware needs more signal energy.** Try switching to
   `StrongAmplitude` (-60 dBFS) per [Customizing Signal Strength](#customizing-signal-strength)
   and rebuild.
2. **The default output device is not your headphones.** The app is only "one audio
   source on the default output device" — make sure your headphones are set as the
   system default playback device.
3. **Your headphones have an independent power-saving policy.** Some TWS models power
   off based on firmware logic unrelated to the audio stream; software cannot prevent that.
</details>

<details>
<summary><b>Will it affect my music or videos?</b></summary>

No. The app uses WASAPI **shared mode**, coexisting with other applications, and the
signal is as low as -90 dBFS, which is inaudible. You will see a SoundKeeper session
in the system volume mixer — that is expected.
</details>

<details>
<summary><b>Why is there no window in the taskbar?</b></summary>

By design. The app creates no forms; it exists only in the system tray via `NotifyIcon`.
If the icon is hidden, click the "show hidden icons" arrow in the tray area.
</details>

<details>
<summary><b>Does it trigger a UAC prompt on boot?</b></summary>

It should not. The app only writes to `HKCU` (current user) and declares
`requestedExecutionLevel level="asInvoker"` in its manifest, so it never requests
administrator rights. If you see a UAC prompt, check whether security software is
blocking the auto-start entry.
</details>

<details>
<summary><b>Is it resource-hungry?</b></summary>

No. The samples are a constant value produced by a simple assignment loop, so CPU usage
is negligible; measured memory usage is around 40–50 MB.
</details>

## Project Structure

```
SoundKeeper/
├── Program.cs                  # Entry point: --silent / --selftest, single-instance guard
├── TrayApplicationContext.cs   # Tray lifetime, context menu, state refresh
├── AudioKeepAlive.cs           # NAudio stream wrapper (with hot-plug recovery)
├── TinySampleProvider.cs       # Tiny non-zero sample generator
├── AppSettings.cs              # Registry persistence (HKCU)
├── TrayIconFactory.cs          # Code-drawn two-state tray icons
├── NativeMethods.cs            # Win32 interop (icon handle cleanup)
├── app.manifest                # App manifest (asInvoker + compatibility)
├── SoundKeeper.csproj          # Project file
├── NuGet.Config                # Package source configuration
├── LICENSE                     # MIT
└── CHANGELOG.md                # Changelog
```

## Contributing

Issues and pull requests are welcome.

1. Fork this repository
2. Create a feature branch: `git checkout -b feature/your-feature`
3. Commit your changes: `git commit -m "feat: add some feature"`
4. Push the branch: `git push origin feature/your-feature`
5. Open a pull request

Before submitting, please make sure:

- `dotnet build -c Release` completes with no errors and no warnings
- If you changed audio-related logic, run `SoundKeeper.exe --selftest` and confirm
  it reports `RESULT: OK`

Commit messages should follow the
[Conventional Commits](https://www.conventionalcommits.org/) convention.

## License

Released under the [MIT License](LICENSE).

## Acknowledgements

- [NAudio](https://github.com/naudio/NAudio) — a powerful .NET audio library and the
  core dependency of this project
