# QuickFile

<p align="center">
  <img src="assets/logo.png" width="120" height="120" alt="QuickFile Logo" />
</p>

> **Quick file actions, right from Windows File Explorer.**

A lightweight, zero-overhead Windows desktop utility that embeds image, audio, video, and PDF operations directly into Windows File Explorer's right-click context menu. Perform conversions, compression, resizing, audio extraction, and PDF manipulation locally and instantly—without launching heavy software suites or keeping background services running.

---

![QuickFile Early Preview](assets/quickfile-preview.png)
*Early preview & product concept mockup of QuickFile's context-menu integration.*

---

## What QuickFile Does

Modern desktop file workflows frequently require quick, repetitive tasks—converting a PNG to WebP for the web, transcoding a video to MP4, compressing an audio file for sharing, extracting an audio track from a video clip, or splitting pages from a PDF. Traditional tools force you to launch slow editing software, open cluttered ad-filled web converters, or run permanent memory-hogging background daemons.

**QuickFile removes all friction:**
1. **Right-click** any supported file in File Explorer.
2. **Hover** over **QuickFile**.
3. **Select an action**—convert immediately or configure via a compact mini window.
4. **Done.** Output files are generated alongside your original with atomic safety, and a silent, fluent notification appears with one-click **Open File** and **Open Folder** actions.

---

## Key Features

- **Intelligent Category-Specific Menus**: Explorer automatically detects the file type and surfaces only relevant operations:
  - Right-click an **image** &rarr; see Image conversion, compression, resizing, and PDF export.
  - Right-click an **audio track** &rarr; see Audio conversion, bitrate presets, and custom settings.
  - Right-click a **video clip** &rarr; see Video transcoding, resolution presets, and audio extraction.
  - Right-click a **PDF document** &rarr; see Page splitting, page rotation, and image extraction.
- **Direct Windows 11 Shell Integration**: In Windows 11, bypasses the "Show more options" extra step so actions appear on first right-click. Fully compatible with Windows 10.
- **Zero Runtime Overhead**: **0 MB RAM and 0% CPU at idle**. QuickFile does not run background services, tray pollers, or daemons. It launches on-demand and terminates immediately upon task completion.
- **Real-Time Progress & Cancellation**: For long-running video encodings and multi-page conversions, a sleek, floating progress dialog displays real-time percent completion, encoding speed, and an instant **Cancel** button with atomic temp-file cleanup.
- **Silent Acrylic HUD**: Completed operations show a modern Windows 11 fluent popup featuring an animated vector checkmark in a circular reveal, slide-and-fade entrance, and "Open File" / "Open Folder" action buttons. **Zero annoying system beeps or alerts.**
- **Guaranteed File Safety**: Never overwrites originals by default. Uses temporary staging files and atomic moves so interrupted tasks never leave corrupt or incomplete files behind.
- **100% Local & Private**: All image processing, audio/video encoding, and PDF rendering executes locally on your machine. No cloud uploads, no telemetry, no tracking, and no internet connection required.

---

## Supported Formats & Capabilities

### 🖼️ Images
- **Input Formats**: `PNG`, `JPG / JPEG`, `WEBP`, `BMP`, `TIFF`, `GIF`
- **Output Formats**: `PNG`, `JPG`, `WEBP`, `BMP`, `PDF`
- **Features**:
  - 1-click format conversion with alpha channel preservation.
  - Smart transparency handling: automatically composites on white when converting transparent images to opaque formats (JPG/BMP).
  - Quality compression (10%–100%) with live size reduction estimation.
  - Compress to target budget (e.g. fit under `250 KB` or `1 MB`).
  - Resizing with aspect-ratio lock and instant presets (`25%`, `50%`, `75%`, `1080p`, `720p`).
  - Convert single image or combine multiple images directly into a PDF.

### 🎵 Audio
- **Input Formats**: `MP3`, `WAV`, `FLAC`, `AAC`, `M4A`, `OGG`, `WMA`
- **Output Formats**: `MP3`, `WAV`, `FLAC`, `AAC`, `M4A`, `OGG`
- **Features**:
  - Direct 1-click conversion between major audio formats.
  - Quality presets: **Smaller file** (128 kbps), **Balanced** (192 kbps), **High quality** (320 kbps / Lossless FLAC).
  - Advanced audio settings: configurable bitrate, sample rates (`44.1 kHz`, `48.0 kHz`, `96.0 kHz`), and channel configuration (Stereo / Mono).

### 🎬 Video
- **Input Formats**: `MP4`, `MKV`, `MOV`, `WEBM`, `AVI`, `WMV`
- **Output Formats**: `MP4`, `MKV`, `MOV`, `WEBM`, `AVI`
- **Features**:
  - Transcoding with optimized codec presets: H.264/AAC for MP4/MKV/MOV, VP9/Opus for WEBM.
  - Quality presets: **Smaller file** (CRF 28), **Balanced** (CRF 23), **High quality** (CRF 18).
  - Target resolutions: `Original`, `1080p Full HD`, `720p HD`, `480p SD` (automatic aspect-ratio preservation).
  - **Extract Audio**: 1-click audio extraction from any video file to high-quality MP3 or WAV.

### 📄 PDF Documents
- **Input Formats**: `PDF`
- **Output Formats**: `PDF`, `PNG`, `JPG`, `WEBP`
- **Features**:
  - **Images to PDF**: Combine multiple images into a multi-page PDF with exact dimension matching.
  - **PDF to Images**: Render pages into high-resolution PNG or JPG images (`150 DPI` or `300 DPI` print-grade).
  - **Split PDF**: Split a multi-page PDF into individual 1-page PDF files.
  - **Extract Pages**: Extract custom page ranges (e.g. `1-3, 5, 8-10`) into a new PDF.
  - **Rotate Pages**: Rotate orientation by 90° clockwise, 180°, or 270°.
  - **Merge PDFs**: Combine multiple PDF documents into a single unified file.

---

## How It Works

QuickFile uses Windows Shell native static cascading context menu verbs registered under:
- `HKCU\Software\Classes\SystemFileAssociations\image\shell\QuickFile`
- `HKCU\Software\Classes\SystemFileAssociations\audio\shell\QuickFile`
- `HKCU\Software\Classes\SystemFileAssociations\video\shell\QuickFile`
- `HKCU\Software\Classes\SystemFileAssociations\.pdf\shell\QuickFile`

- **No In-Process DLL Injection**: Explorer remains 100% stable; no third-party code is injected into `explorer.exe`.
- **User-Space Installation**: Installs and uninstalls entirely within current user registry (`HKCU`), requiring no administrator privileges.
- **Engine Stack**:
  - **SkiaSharp (Google Skia)**: High-performance 2D vector & raster graphics for lightning-fast image encoding/decoding.
  - **FFmpeg**: Industry-standard local media engine for video transcoding, audio conversion, and audio stream extraction.
  - **PDFtoImage & PDFsharp**: Google PDFium and PDFsharp engine for fast page rendering, splitting, page extraction, and merging.

---

## Getting Started

### Prerequisites
- Windows 10 (version 1809+) or Windows 11 (64-bit).
- [.NET 9.0 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/9.0) (or SDK if building from source).
- [FFmpeg](https://ffmpeg.org/download.html) in your system PATH (installed automatically by `winget install Gyan.FFmpeg`).

### Installation & Context Menu Setup

1. Download or build `QuickFile.exe`.
2. Register the context menu by running:
   ```cmd
   QuickFile.exe --register
   ```
   Or launch `QuickFile.exe` to open the Settings window and click **Register**.
3. Right-click any image, audio, video, or PDF file in Windows Explorer to see QuickFile!

### Clean Uninstallation
To completely remove QuickFile from your context menu and registry:
```cmd
QuickFile.exe --unregister
```
Leaves zero registry artifacts and requires no restart.

---

## CLI & Automation

QuickFile supports automated and direct command-line execution:

```cmd
:: Convert an image to WEBP
QuickFile.exe --action convert --format webp "C:\Photos\photo.png"

:: Transcode video to MP4 with 1080p resolution
QuickFile.exe --action video-convert --format mp4 --res 1080 "C:\Videos\clip.mkv"

:: Extract audio from video to MP3
QuickFile.exe --action extract-audio --format mp3 "C:\Videos\interview.mov"

:: Convert audio format with high quality preset
QuickFile.exe --action audio-convert --format flac --preset highquality "C:\Music\song.wav"

:: Convert PDF pages to PNG images
QuickFile.exe --action pdf-to-images --format png "C:\Docs\report.pdf"

:: Rotate PDF pages 90 degrees clockwise
QuickFile.exe --action pdf-rotate --angle 90 "C:\Docs\scan.pdf"

:: Open interactive Mini Action Window near cursor
QuickFile.exe --action compress "C:\Photos\photo.png"
QuickFile.exe --action audio-options "C:\Music\track.mp3"
QuickFile.exe --action video-options "C:\Videos\movie.mp4"
QuickFile.exe --action pdf-tools "C:\Docs\document.pdf"

:: Copy path or view file information
QuickFile.exe --action copy-path "C:\Files\document.pdf"
QuickFile.exe --action info "C:\Videos\sample.mp4"
```

---

## Building from Source

```bash
# Clone repository
git clone https://github.com/Naveen-io/QuickFile.git
cd QuickFile

# Restore dependencies and build Release binaries
dotnet build QuickFile.sln -c Release
```

The compiled binary will be located in:
`src/QuickFile.App/bin/Release/net9.0-windows/QuickFile.exe`

---

## License

This project is licensed under the [MIT License](LICENSE).
