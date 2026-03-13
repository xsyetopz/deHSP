#!/usr/bin/env bash
set -euo pipefail

VERSION="${1:-2.0.0}"
ARTIFACTS_DIR="artifacts"
PUBLISH_FLAGS=(
    -c Release
    --self-contained true
    -p:PublishSingleFile=true
    -p:IncludeNativeLibrariesForSelfExtract=true
    -p:EnableCompressionInSingleFile=true
    -p:DebugType=none
    -p:DebugSymbols=false
)

RIDS=(
    osx-x64
    osx-arm64
    win-x64
    win-arm64
    linux-x64
    linux-arm64
)

PROJECTS=(
    "src/HspDecompiler.Gui/HspDecompiler.Gui.csproj"
    "src/HspDecompiler.Cli/HspDecompiler.Cli.csproj"
)

rm -rf "$ARTIFACTS_DIR"
mkdir -p "$ARTIFACTS_DIR"

for rid in "${RIDS[@]}"; do
    echo "=== Publishing $rid ==="
    stage_dir="$ARTIFACTS_DIR/stage-$rid"
    mkdir -p "$stage_dir"

    for proj in "${PROJECTS[@]}"; do
        dotnet publish "$proj" -r "$rid" -o "$stage_dir" "${PUBLISH_FLAGS[@]}"
    done

    # Remove PDB files that may slip through
    find "$stage_dir" -name '*.pdb' -delete 2>/dev/null || true

    zip_name="deHSP-${VERSION}-${rid}.zip"
    (cd "$stage_dir" && zip -r "../../$ARTIFACTS_DIR/$zip_name" .)
    echo "  -> $ARTIFACTS_DIR/$zip_name"

    rm -rf "$stage_dir"
done

echo ""
echo "=== Build complete ==="
ls -lh "$ARTIFACTS_DIR"/*.zip
