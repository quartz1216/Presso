# Presso

A Windows utility that aggressively compresses videos to fit Discord's 10 MB upload limit (or any size you set). Built around FFmpeg 2-pass encoding with proportional-feedback bitrate convergence — instead of giving up after a few attempts, it learns from each try and homes in on the target size automatically.

## Features

- **Drag & drop GUI** with a multi-file queue, per-item progress bars, and cancellation.
- Supports `mp4`, `mov`, `mkv`, `webm`, `avi` as input — output is always `<name>_presso.mp4`.
- **Right-click → "Compress with Presso"** in Explorer to compress directly into the source folder.
- Configurable target size, codec (HEVC / H.264), and output directory. Settings are persisted to `%APPDATA%\Presso\config.json`.

## Installation

Grab the latest `PressoSetup-x.y.z.exe` from [Releases](../../releases) and run it. The installer:

- places Presso under `C:\Program Files\Presso\`,
- registers the **Compress with Presso** context menu for the supported video extensions, and
- bundles its own FFmpeg/ffprobe — no separate install needed.

## Usage

- Launch **Presso** from the Start Menu and drop video files onto the window, **or**
- right-click a video file in Explorer and pick **Compress with Presso**.

Defaults: target 10 MB, HEVC enabled, output written next to the source file. Open the **Settings** dialog to change any of these. The right-click flow always writes to the source folder regardless of the GUI's output-folder setting.

## How the size targeting works

1. Read duration / dimensions via `ffprobe`.
2. Compute an initial video bitrate from `target_bytes − audio_budget`, divided by duration.
3. Run a 2-pass encode at that bitrate.
4. If the result is over budget, set `next_bitrate = current * (target_size / actual_size) * 0.97` and retry.

This proportional feedback usually converges in 1–2 retries instead of grinding through a fixed `* 0.9` decay. If five attempts still don't land it, it falls back to HEVC → H.264, then a CRF-32 last resort (size not guaranteed in CRF mode).

## Building from source

### Prerequisites

- Windows 10/11 (x64)
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [Inno Setup 6](https://jrsoftware.org/isdl.php)
- FFmpeg Windows 64-bit GPL **shared** build (e.g. [BtbN/FFmpeg-Builds](https://github.com/BtbN/FFmpeg-Builds/releases) → `ffmpeg-master-latest-win64-gpl-shared.zip`). FFmpeg binaries are not committed to the repo — fetch separately and unzip into the layout below.

### Layout

```
Presso\
├── ffmpeg\
│   └── bin\
│       ├── ffmpeg.exe
│       ├── ffprobe.exe
│       └── *.dll
├── src\Presso\...
└── installer\Presso.iss
```

### Build

```powershell
.\build.ps1
```

Output: `installer\output\PressoSetup-x.y.z.exe`.

## License

The Presso application source is [MIT-licensed](LICENSE).

The distributed installer also bundles FFmpeg binaries (compiled with `--enable-gpl --enable-version3`, including `libx264` and `libx265`). Those binaries are licensed under **GPLv3** and are governed by their own terms — see [`LICENSES/THIRD_PARTY_NOTICES.txt`](LICENSES/THIRD_PARTY_NOTICES.txt) and [`LICENSES/GPLv3.txt`](LICENSES/GPLv3.txt). Presso invokes FFmpeg only as an external process; no FFmpeg code is statically linked into Presso itself.

H.264 / HEVC are patent-encumbered formats. If you redistribute or use this commercially, check the licensing terms of the relevant patent pools (MPEG-LA, HEVC Advance, Velos Media).
