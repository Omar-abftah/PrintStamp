#!/usr/bin/env bash
set -euo pipefail
OUT=deploy/PrintStamp/bin
rm -rf deploy && mkdir -p "$OUT"

for app in PrintApp AdminApp; do
  dotnet publish PrintStamp.$app -c Release -r win-x64 --self-contained \
    -p:DebugType=none -p:SatelliteResourceLanguages=en \
    -o "$OUT"
done
cp vendor/SumatraPDF-3.6.1-64.exe "$OUT/"
echo "Copy deploy/PrintStamp to C:\\PrintStamp on the PC"