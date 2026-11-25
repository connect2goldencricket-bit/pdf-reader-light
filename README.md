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

## Build (on Windows)

Requires the [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0).

```bat
build.bat
```

or manually:

```bat
dotnet publish src\PdfReaderLight\PdfReaderLight.csproj -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o dist
```

Output in `dist\`. Run `dist\PdfReaderLight.exe` (the target PC needs the
[.NET 8 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0)).
For a standalone exe that needs no runtime (~150 MB), use `--self-contained true`.

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
dist/                 publish output (gitignored)
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
