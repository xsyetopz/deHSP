# HSP Decompiler (deHSP)

Converts HSP (Hot Soup Processor) 2 and 3 compiled files back to source code. Accepts `.ax`, `.exe`, and `.dpm` files and produces `.hsp` or `.as` output.

## Features

- Decompiles HSP3 (`.ax` → `.hsp`) and HSP2 (`.ax` → `.as`) compiled files
- Extracts files from DPM archives
- Extracts DPM from HSP executables (`.exe`)
- Decrypts encrypted files in DPM archives (brute-force XOR+ADD)
- Cross-platform: Windows, macOS, Linux
- CLI tool and GUI application

## Requirements

- .NET 8.0 SDK or later

## Building

```bash
dotnet build HspDecompiler.sln
```

## Usage

### CLI

```bash
# Decompile an .ax file
dotnet run --project src/HspDecompiler.Cli -- input.ax

# Decompile with output directory
dotnet run --project src/HspDecompiler.Cli -- input.ax -o ./output

# Extract DPM archive
dotnet run --project src/HspDecompiler.Cli -- archive.dpm -o ./extracted

# Extract from HSP executable
dotnet run --project src/HspDecompiler.Cli -- game.exe -o ./extracted
```

Options:

```text
dehsp <input-file> [options]
  -o, --output <output>          Output directory
  -d, --dictionary <dictionary>  Custom dictionary XML (default: built-in HSP 3.8beta2 table)
  --no-decrypt                   Skip encrypted file decryption
  --skip-encrypted               Extract only non-encrypted files
  -v, --verbose                  Verbose logging
  -?, -h, --help                 Show help and usage information
  --version                      Show version information
```

### GUI

```bash
dotnet run --project src/HspDecompiler.Gui
```

Or drag-and-drop files onto the application window.

## Project Structure

| Project | Description |
| --- | --- |
| `HspDecompiler.Core` | Core decompiler library — all algorithms |
| `HspDecompiler.Cli` | Command-line interface |
| `HspDecompiler.Gui` | Avalonia cross-platform GUI |

## Dictionary

The HSP 3 keyword table is built into the decompiler, so no file has to sit next to it. It
covers HSP 3.8beta2: the built-in commands, functions, and parameter types. The source is
[`Dictionary.xml`](src/HspDecompiler.Core/Ax3/Dictionary.xml).

To use your own table, copy `Dictionary.xml`, edit it, and pass it with `--dictionary`.
[`Dictionary.xsd`](src/HspDecompiler.Core/Ax3/Dictionary.xsd) defines the format, and an
editor with XML schema support checks your file as you type. Numbers are fixed-width uppercase
hex (`0x0F`, `0x001E`). The decompiler validates the file before it reads it and stops with the
line of the first error.

## History

| Date | Name | Version | Notes |
| --- | --- | --- | --- |
| 2006-01-28 | HSP Decompiler (HSP逆コンパイラ) | 1.0 | Initially released as shareware by Kitsutsuki |
| 2007-09-10 | HSP Decompiler | 1.1 | HSP3 support added |
| 2010-09-12 | HSP Decompiler | 1.2 | Released as PDS/OSS; bug fixes |
| 2012-01-13 | HSPdeco | 1.0 | `.ax` decode feature exposed; bug fixes. Author: minorshift (Mia) |
| 2015-12-15 | HSPdecom | 1.0 | Variable name restoration, dictionary data. Authors: Sitapuru, YSRKEN |
| 2016-08-16 | HSPdecom | 1.1 | Decryption patches applied; republished on GitHub |
| 2026-03-13 | deHSP | 2.0 | .NET 8.0 + Avalonia; CLI tool; cross-platform |

## License

PDS (Public Domain Software) for the HSPdecom parts.
HSPdeco parts under zlib/libpng license.
See the [LICENSE](LICENSE) file for details.
