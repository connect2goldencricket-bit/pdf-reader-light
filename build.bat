@echo off
REM Builds PdfReaderLight for Windows (x64) into dist\
REM Requires the .NET 8 SDK: https://dotnet.microsoft.com/download/dotnet/8.0
REM
REM Default output: dist\PdfReaderLight.exe + dist\pdfium.dll (~5 MB total).
REM The target PC needs the .NET 8 Desktop Runtime
REM (https://dotnet.microsoft.com/download/dotnet/8.0 - "Desktop Runtime").
REM
REM For a fully standalone exe (no runtime needed, ~150 MB), replace
REM "--self-contained false" below with "--self-contained true".

dotnet publish src\PdfReaderLight\PdfReaderLight.csproj -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o dist

echo.
echo Done. Run: dist\PdfReaderLight.exe
echo Drag and drop a .pdf onto the window, or pass it as an argument.
pause
