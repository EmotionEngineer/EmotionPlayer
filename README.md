# EmotionPlayer

**EmotionPlayer** is a simple desktop video player for Windows with **content analysis and age rating**.  
It uses WPF `MediaElement` for playback and [EmotionLib](https://github.com/EmotionEngineer/EmotionLib) for:

- per‑frame **positiveness** analysis,
- video **content filtering**,
- final **MPAA‑style rating** / “Unsafe” classification via SAMP.

> Requires **Windows Media Player** (uses the WPF MediaElement stack).  
> Target framework: **.NET Framework 4.7.2**  
> Platform: **x64** (Windows 10/11)

---

## Table of Contents

1. [Features](#features)  
2. [Screenshots](#screenshots)  
3. [Architecture Overview](#architecture-overview)  
4. [Build from Source](#build-from-source)  
   - [Prerequisites](#prerequisites)  
   - [Clone Repositories](#clone-repositories)  
   - [Configure NuGet](#configure-nuget)  
   - [Build EmotionLib (native DLLs)](#build-emotionlib-native-dlls)  
   - [Build EmotionPlayer](#build-emotionplayer)  
   - [Deploy Native Libraries](#deploy-native-libraries)  
5. [Running EmotionPlayer](#running-emotionplayer)  
6. [Usage](#usage)  
   - [File selection & recognition](#file-selection--recognition)  
   - [Timeline emotions](#timeline-emotions)  
   - [Keyboard shortcuts](#keyboard-shortcuts)  
7. [Troubleshooting](#troubleshooting)  
8. [Notes](#notes)

---

## Features

- **Simple video playback**
  - Based on WPF `MediaElement` (Windows Media stack).
  - Basic controls: play/pause, previous/next, volume, mute, fullscreen toggle.

- **Content analysis & age rating**
  - Uses models from [EmotionLib](https://github.com/EmotionEngineer/EmotionLib):
    - `positiveness.dll` — positiveness / negativity model.
    - `filter.dll` — content filter model.
    - `samp.dll` — combines both into a **scalar rating** and **MPAA label** (`G`, `PG`, `PG-13`, `R`) or `Unsafe`.
  - Results are stored in `.epp` and `.efp` binary files in an `Output` folder.

- **Emotion timeline**
  - Video frames are sampled **every N seconds** (interval depends on total video duration).
  - For each sampled point, positiveness is shown as a **happy/sad** icon when hovering the timeline.
  - Interval is consistent with what is used in `.epp` / `.efp` files and SAMP.

- **Batch processing**
  - “Files” window allows loading multiple videos.
  - “Recognize” runs analysis sequentially for each selected video.
  - A **progress window** shows:
    - current video name,
    - current stage: `Positiveness` / `Filter`,
    - progress percentage.

---

## Screenshots

![screenshot 5](https://raw.githubusercontent.com/EmotionEngineer/EmotionPlayer/master/Screenshots/5.png)
![screenshot 4](https://raw.githubusercontent.com/EmotionEngineer/EmotionPlayer/master/Screenshots/4.png)
![screenshot 1](https://raw.githubusercontent.com/EmotionEngineer/EmotionPlayer/master/Screenshots/1.png)
![screenshot 2](https://raw.githubusercontent.com/EmotionEngineer/EmotionPlayer/master/Screenshots/2.png)
![screenshot 3](https://raw.githubusercontent.com/EmotionEngineer/EmotionPlayer/master/Screenshots/3.png)

---

## Architecture Overview

EmotionPlayer consists of:

- **WPF UI** (C#, .NET Framework 4.7.2)
  - `MainWindow` – video playback, controls, emotion indicator.
  - `FileWindow` – file list management and recognition trigger.
  - `ProgressBarWindow` – per‑video progress (stage + percentage).
  - `About` / `DarkMsgBox` – info and rating result windows.
- **Inference bridge (`Inferencer.cs`)**
  - Loads video frames via **OpenCvSharp4**.
  - Samples frames at dynamic interval (e.g., every 1–6 seconds depending on duration).
  - Runs native models over frame batches in **parallel chunks** (CPU‑only).
  - Produces `.epp` and `.efp` files in `Output`.
  - Calls `samp.dll` for final rating and reports interpreted result to UI.
- **Native libraries from EmotionLib**
  - `filter.dll`, `positiveness.dll`, `samp.dll` — must be built from [EmotionLib](https://github.com/EmotionEngineer/EmotionLib) and copied next to `EmotionPlayer.exe`.
- **OpenCvSharp runtime**
  - Requires `OpenCvSharpExtern.dll` from `OpenCvSharp4.runtime.win`.

---

## Build from Source

### Prerequisites

- **OS**: Windows 10/11 x64  
- **Runtime**: [.NET Framework 4.7.2](https://dotnet.microsoft.com/en-us/download/dotnet-framework/net472)  
- **Media**: Windows Media Player installed (for WPF `MediaElement`)  
- **IDE**: [Visual Studio 2022](https://visualstudio.microsoft.com/) (Community or higher)
  - Workload: **.NET desktop development**

### Clone Repositories

Clone both repositories (preferably into sibling folders):

```bash
git clone https://github.com/EmotionEngineer/EmotionLib.git
git clone https://github.com/EmotionEngineer/EmotionPlayer.git
```

Example layout:

```text
D:\Projects\EmotionLib
D:\Projects\EmotionPlayer
```

### Configure NuGet

If NuGet packages fail to restore, ensure that the official nuget.org source is configured:

1. Open **Visual Studio 2022**.
2. Menu:
   - **Tools → NuGet Package Manager → Package Manager Settings**
3. Select **Package Sources**.
4. Click **“+”** to add a new source.
5. Set:
   - Name: `nuget.org`
   - Source: `https://api.nuget.org/v3/index.json`
6. Click **Update/OK**.

Then, in the Solution Explorer for `EmotionPlayer.sln`, you can:

- Right‑click the solution → **Restore NuGet Packages**, or
- Just build the solution – packages will be restored automatically.

### Build EmotionLib (native DLLs)

`EmotionPlayer` depends on native libraries from [EmotionLib](https://github.com/EmotionEngineer/EmotionLib):

- `filter.dll`
- `positiveness.dll`
- `samp.dll`

Steps:

1. Open `EmotionLib.sln` in Visual Studio 2022.
2. Select configuration:
   - Configuration: **Release**
   - Platform: **x64**
3. Build the solution:
   - Menu: **Build → Build Solution** (or `Ctrl+Shift+B`).

After a successful build, in one of the output directories (e.g.):

```text
EmotionLib\bin\x64\Release\
```

you should find:

- `filter.dll`
- `positiveness.dll`
- `samp.dll`

(see the EmotionLib repository for the exact output location if it differs.)

### Build EmotionPlayer

1. Open `EmotionPlayer/EmotionPlayer.sln` in Visual Studio 2022.
2. Ensure NuGet packages are restored (see [Configure NuGet](#configure-nuget)).
3. In the toolbar or “Configuration Manager”, select:
   - Configuration: **Release**
   - Platform: **x64**
4. Build the project:
   - Menu: **Build → Build Solution** (or `Ctrl+Shift+B`).

The default output path will be similar to:

```text
EmotionPlayer\EmotionPlayer\bin\x64\Release\
```

(or `bin\x64\Debug\` for Debug configuration).

### Deploy Native Libraries

#### 1. Copy EmotionLib DLLs

Copy the three DLLs built from EmotionLib:

- `filter.dll`
- `positiveness.dll`
- `samp.dll`

into the same folder as `EmotionPlayer.exe`, for example:

```text
EmotionPlayer\EmotionPlayer\bin\x64\Release\
```

So that this directory contains:

- `EmotionPlayer.exe`
- `filter.dll`
- `positiveness.dll`
- `samp.dll`
- other managed dependencies…

Without these, EmotionPlayer will start but will fail when trying to run inference.

#### 2. Copy OpenCvSharpExtern.dll (if needed)

The project uses `OpenCvSharp4` and `OpenCvSharp4.runtime.win`.  
On some systems, the native `OpenCvSharpExtern.dll` is not automatically copied to the output folder, resulting in an error:

> `OpenCvSharpExtern.dll` is missing  
> or similar: “Could not load file or assembly OpenCvSharpExtern.dll …”

To fix this:

1. Locate `OpenCvSharpExtern.dll` in your NuGet cache, e.g.:

   ```text
   %USERPROFILE%\.nuget\packages\opencvsharp4.runtime.win\4.8.0.20230708\runtimes\win-x64\native\OpenCvSharpExtern.dll
   ```

2. Copy `OpenCvSharpExtern.dll` into the same directory as `EmotionPlayer.exe`, for example:

   ```text
   EmotionPlayer\EmotionPlayer\bin\x64\Release\
   ```

After this, the OpenCvSharp runtime dependency will be satisfied.

---

## Running EmotionPlayer

Once all of the above steps are completed:

1. Navigate to the output folder, e.g.:

   ```text
   EmotionPlayer\EmotionPlayer\bin\x64\Release\
   ```

2. Double‑click `EmotionPlayer.exe`  
   or run it via Visual Studio:

   - Set `EmotionPlayer` as the startup project.
   - Press **F5** (Debug) or **Ctrl+F5** (Run without debugging).

Make sure the folder contains:

- `EmotionPlayer.exe`
- `filter.dll`
- `positiveness.dll`
- `samp.dll`
- `OpenCvSharpExtern.dll`
- Other managed DLLs from NuGet.

---

## Usage

### File selection & recognition

1. Launch **EmotionPlayer**.
2. Click **Files** in the top bar (or press `O`).
3. In the **Files** window:
   - Use the “+” button or `A`/`+` key to add video files.
   - Supported formats: any that WPF/Windows Media can handle (e.g. `.mp4`, `.avi`, `.wmv`, `.mkv` with proper codecs).
4. Click **Recognize**:
   - A **progress window** appears.
   - Each video is processed sequentially:
     - Stage 1: **Positiveness** model
     - Stage 2: **Filter** model
     - Then **SAMP** is called to determine the final MPAA rating or “Unsafe”.
   - The progress window shows:
     - `Video: <name>`
     - `Stage: Positiveness` / `Stage: Filter`
     - Percent value (0–100).

5. After recognition completes:
   - You return to the main window.
   - You can play the videos with the usual playback controls.
   - Click **MPAA Content Rating** to see a dark popup window with:
     - Final rating (e.g. `G`, `PG-13`, `R`, `Unsafe`)
     - Basic statistics (positive/negative frames based on positiveness model).

### Timeline emotions

- During playback:
  - Move the mouse over the bottom control bar (timeline).
  - The slider shows the current time.
  - A small **emoji icon** (happy/sad) is updated according to the positiveness prediction for the nearest sampled time.
- Sampling interval:
  - Depends on video length (1–6 seconds between frames).
  - The same interval is used for `.epp` and `.efp` and for SAMP.
  - The UI converts timeline seconds to the correct index based on that interval.

### Keyboard shortcuts

Core shortcuts in the main window:

- Playback:
  - `Space` / `P` — Play/Pause
  - `W` / `S` — Stop
  - `MediaPlayPause` / `MediaStop` / `MediaNextTrack` / `MediaPreviousTrack` (multimedia keys)

- Navigation:
  - `→` / `←` — small skip forward/backward within current video
  - `Ctrl + →` — next video in playlist
  - `Ctrl + ←` — previous video in playlist
  - `Esc` — toggle maximized / normal window

- Volume:
  - `+` (`OemPlus`) — increase volume
  - `-` (`OemMinus`) — decrease volume

- Windows & dialogs:
  - `O` — open Files window
  - `H` — open About window

In the **Files** window:

- `S` — accept and start recognition
- `Esc` — cancel
- `A` / `+` — add files
- `D` / `-` — delete selected file
- `F` — clear selection
- `C` — clear list
- `N` — move selected item down
- `U` — move selected item up

---

## Troubleshooting

### “OpenCvSharpExtern.dll is missing” on startup

**Symptoms**: Application fails to start or throws a `DllNotFoundException` for `OpenCvSharpExtern.dll`.

**Fix**:

- Find `OpenCvSharpExtern.dll` in NuGet cache:

  ```text
  %USERPROFILE%\.nuget\packages\opencvsharp4.runtime.win\4.8.0.20230708\runtimes\win-x64\native\OpenCvSharpExtern.dll
  ```

- Copy it into the same folder as `EmotionPlayer.exe` (`bin\x64\Release\` or `bin\x64\Debug\`).

### “filter.dll / positiveness.dll / samp.dll could not be loaded”

**Symptoms**: On running recognition, native P/Invoke calls fail; console output may show errors loading those DLLs.

**Fix**:

1. Build EmotionLib in `x64` configuration (Release or Debug).
2. Copy:

   - `filter.dll`
   - `positiveness.dll`
   - `samp.dll`

   to the output directory of EmotionPlayer (next to `EmotionPlayer.exe`).

### Progress window stuck at 0%

**Possible reasons**:

- Native DLLs are missing or mismatched (wrong architecture, e.g. x86 vs x64).
- Video is extremely short / broken and contains no valid frames.

**What to check**:

- Ensure you built EmotionLib in **x64** and run EmotionPlayer as **x64**.
- Verify that the input video plays in Windows Media Player / standard players.
- Check the console output (if run from Visual Studio) for messages like:
  - “Failed to open video”
  - “Video has no frames”

---

## Notes

- Inference is CPU‑only; processing long videos can take significant time.
- The code uses a **parallel chunking strategy** over frame batches, but the native DLLs themselves are not multi‑threaded.
- `.epp` and `.efp` files are written into an `Output` directory next to the executable; they can be reused across runs (SAMP only needs those files, not the original video).
