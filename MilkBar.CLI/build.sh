#!/usr/bin/env bash
set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
OUTPUT_DIR="${1:-$SCRIPT_DIR/publish}"

echo "[*] Compiling MilkBar.CLI (.NET 8.0 win-x64)..."
dotnet publish "$SCRIPT_DIR/MilkBar.CLI.csproj" \
    -c Release \
    -r win-x64 \
    --no-self-contained \
    -o "$OUTPUT_DIR"

echo "[SUCCESS] Build completed -> $OUTPUT_DIR/MilkBar.CLI.exe"
