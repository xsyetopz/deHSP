# Project recipes. The shared justfile imports this file.

# Resolved from the justfile directory, since hooks run some recipes in a scratch tree.
version := shell('sed -n "s:.*<Version>\(.*\)</Version>.*:\1:p" "$1"', justfile_directory() / "Directory.Build.props")
artifacts := "artifacts"
rids := "osx-x64 osx-arm64 win-x64 win-arm64 linux-x64 linux-arm64"

# Publish the GUI and CLI as self-contained single files, zipped per runtime.
publish *targets=rids:
    #!/usr/bin/env bash
    set -euo pipefail
    for rid in {{ targets }}; do
        stage="{{ artifacts }}/stage-$rid"
        mkdir -p "$stage"
        for proj in src/HspDecompiler.Gui/HspDecompiler.Gui.csproj src/HspDecompiler.Cli/HspDecompiler.Cli.csproj; do
            dotnet publish "$proj" -r "$rid" -o "$stage" -c Release --self-contained true \
                -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true \
                -p:EnableCompressionInSingleFile=true -p:DebugType=none -p:DebugSymbols=false
        done
        # Doc-comment XML serves IDEs, not the app; the dictionary is an embedded resource.
        find "$stage" \( -name '*.pdb' -o -name '*.xml' \) -delete
        (cd "$stage" && zip -qr "../deHSP-{{ version }}-$rid.zip" .)
        rm -rf "$stage"
        echo "-> {{ artifacts }}/deHSP-{{ version }}-$rid.zip"
    done

# Run only the integration tests against the sample fixtures.
integration-test:
    dotnet test HspDecompiler.sln --filter "FullyQualifiedName~IntegrationTests"

# Remove build output and published archives.
clean:
    dotnet clean HspDecompiler.sln -v q
    rm -rf {{ artifacts }}

# Fail on any ReSharper InspectCode warning in the build that `just lint` made.
inspect:
    #!/usr/bin/env bash
    set -euo pipefail
    report="${TMPDIR:-/tmp}/dehsp-inspectcode.txt"
    dotnet jb inspectcode HspDecompiler.sln --no-build --severity=WARNING --format=Text \
        --output="$report" --no-updates --caches-home="${TMPDIR:-/tmp}/dehsp-inspectcode-cache" \
        --verbosity=WARN
    findings=$(grep -E '^ {6}[^ ]' "$report" || true)
    if [ -n "$findings" ]; then
        printf '%s\n' "$findings"
        exit 1
    fi

project-lint: inspect
