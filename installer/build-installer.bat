@echo off
REM Builds the MSI installer on Windows.
REM Requires: .NET 8 SDK  and  WiX v3 (candle.exe + light.exe from https://github.com/wixtoolset/wix3/releases)
REM (put candle.exe/light.exe on PATH, or edit the paths below)
setlocal
cd /d "%~dp0.."

echo [1/2] Publishing self-contained single-file exe (no .NET runtime needed on target PC)...
dotnet publish src\PdfReaderLight\PdfReaderLight.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -o dist-selfcontained || exit /b 1

echo [2/2] Building MSI with WiX v3...
candle.exe -arch x64 installer\PdfReaderLight.wxs -o installer\PdfReaderLight.wixobj || exit /b 1
light.exe installer\PdfReaderLight.wixobj -o dist\PdfReaderLight-1.0.0-x64.msi || exit /b 1

echo.
echo Built: dist\PdfReaderLight-1.0.0-x64.msi
echo Double-click it on Windows to install (Program Files, Start Menu + Desktop shortcuts,
echo entry in Settings ^> Apps, .pdf "Open with" entry). Uninstall via Settings ^> Apps.
endlocal
pause
