#!/bin/sh
# Builds the MSI installer on Linux/macOS using wixl from msitools.
# Debian/Ubuntu: sudo apt-get install msitools   macOS: brew install msitools
set -e
cd "$(dirname "$0")/.."
dotnet publish src/PdfReaderLight/PdfReaderLight.csproj -c Release -r win-x64 \
  --self-contained true -p:PublishSingleFile=true \
  -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true \
  -o dist-selfcontained
mkdir -p dist
wixl -a x64 -o dist/PdfReaderLight-1.0.0-x64.msi installer/PdfReaderLight.wxs
echo "Built: dist/PdfReaderLight-1.0.0-x64.msi"
