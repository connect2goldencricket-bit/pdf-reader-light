# PdfReaderLight

A lightweight PDF reader for Windows (x64): a native WinForms app on top of PDFium.
The framework-dependent build is about **5 MB** (`PdfReaderLight.exe` ~0.3 MB + `pdfium.dll` ~4.6 MB).

## Features

- Open PDFs via file dialog, command line (`PdfReaderLight.exe file.pdf`), **drag & drop**, or the Recent menu
- Page navigation: toolbar, `PageUp`/`PageDown`, `Home`/`End`, jump-to-page box
- Zoom: 50%–400% presets, zoom in/out (`Ctrl +/-`), fit-width (`Ctrl+1`), fit-page (`Ctrl+2`), 100% (`Ctrl+0`); fit modes re-adjust on window resize
- **Full-text search** (`Ctrl+F`, `F3` / `Shift+F3` next/previous) with orange hit highlighting and auto-scroll to the match
- Thumbnail sidebar (`F8` to toggle), capped at 300 pages
- Remembers the last page per file and your zoom level (`%AppData%\PdfReaderLight\settings.json`)
- Single-page view rendered on a background thread, so the UI never freezes on big pages

## Installer (recommended — “normal app” setup)

Prebuilt installer in [`releases/`](releases/PdfReaderLight-1.0.0-x64.msi):

- **`releases/PdfReaderLight-1.0.0-x64.msi`** (~66 MB) — double-click to install. Installs to
  `C:\Program Files\PdfReaderLight`, adds Start Menu + Desktop shortcuts, an entry in
  Settings → Apps (with uninstall), and registers PdfReaderLight under .pdf “Open with”.
  **Self-contained: no .NET runtime needed on the PC.**
- **`releases/PdfReaderLight.exe`** (~74 MB) — the same app as a portable single-file exe
  (self-contained, works without installing anything).

Rebuild the installer yourself: `installer/build-installer.sh` (Linux/macOS, needs `msitools`)
or `installer/build-installer.bat` (Windows, needs .NET 8 SDK + WiX v3).

Note: the exe/MSI are unsigned, so Windows SmartScreen may show “Windows protected your PC”
on first run — click **More info → Run anyway**.

## Build (on Windows)

Requires the [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0).

```bat
build.bat
```

or manually (framework-dependent, ~5 MB, needs the .NET 8 Desktop Runtime on the target PC):

```bat
dotnet publish src\PdfReaderLight\PdfReaderLight.csproj -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o dist
```

For a standalone exe that needs no runtime (~74 MB compressed), use
`--self-contained true -p:EnableCompressionInSingleFile=true` (this is what the installer bundles).

## Build & test from Linux/macOS

The app targets `net8.0-windows` with `EnableWindowsTargeting`, so it cross-compiles from Linux:

```bash
dotnet build src/PdfReaderLight/PdfReaderLight.csproj -c Release
dotnet publish src/PdfReaderLight/PdfReaderLight.csproj -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o dist
```

The PDF engine (`PdfReaderCore`) is UI-free and runs on any OS — the `PdfiumCore` NuGet package
ships native PDFium binaries for Windows, Linux and macOS. Headless smoke tests:

```bash
python3 testdata/make_sample.py   # regenerate testdata/sample.pdf (needs: pip install pymupdf)
dotnet run --project tests/CoreSmoke -- testdata/sample.pdf
```

## Project layout

```
src/PdfReaderCore/    UI-free PDFium wrapper: open, page info, render to BGRA, search, text, metadata
src/PdfReaderLight/   WinForms UI (Program, MainForm, RenderUtil, AppSettings)
tests/CoreSmoke/      console test harness for the engine (exit code 0 = pass)
testdata/             sample.pdf fixture + generator
build.bat             one-click Windows build
dist/                 publish output: single-file PdfReaderLight.exe (gitignored)
```

## How it works

`PdfReaderCore.PdfEngine` wraps PDFium through the `PdfiumCore` NuGet package (P/Invoke into
`pdfium.dll`). `RenderPage` rasterizes a page with `FPDF_RenderPageBitmap` into a BGRA buffer
(white background, annotations on), and the UI copies it into a `Bitmap` via `LockBits`.
Search uses `FPDFText_FindStart/Next` and converts match rectangles to highlights.
All engine calls are serialized behind a lock, and rendering runs on `Task.Run`.

## Limitations (v1)

- Single-page view (no continuous scroll), no print, no annotation editing
- Encrypted/password-protected PDFs are rejected with an error message
- Very large pages are capped only by memory (zoom out to reduce)

## License

App code: MIT. PDFium is BSD-3-Clause (see `LICENSE` next to `pdfium.dll` in `dist/`).
