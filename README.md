# HSP Decompiler (deHSP)

Converts HSP (Hot Soup Processor) 2 and 3 compiled files back to source code. Accepts `.ax`, `.exe`, and `.dpm` files and produces `.hsp` or `.as` output.

## Features

- Decompiles HSP3 (`.ax` → `.hsp`) and HSP2 (`.ax` → `.as`) compiled files
- Extracts files from DPM archives
- Extracts DPM from HSP executables (`.exe`), including ones packed with UPX
- Decrypts encrypted files in DPM archives (brute-force XOR+ADD)
- Cross-platform: Windows, macOS, Linux
- CLI tool and GUI application

## Requirements

- .NET 10 SDK

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
  --utf8                         Write UTF-8 without a BOM instead of Shift_JIS; recompile with hspcmp -i or -j
  -v, --verbose                  Verbose logging
  -?, -h, --help                 Show help and usage information
  --version                      Show version information
```

`dehsp` exits with 0 on success and 1 on any failure: a bad argument, a dictionary that
does not load, or an input it cannot decompile.

### GUI

```bash
dotnet run --project src/HspDecompiler.Gui
```

Or drag-and-drop files onto the application window.

## Output

An HSP 3 program becomes one `.hsp` file. An HSP 2 program becomes an `.as` file, and each
file it included becomes its own `.as` file next to it, referenced by `#include`.

### Recompiling

The output aims to compile back to the same AX bytes. The first lines restore the options
the AX records, so hspcmp builds it the same way:

| Line | Written when the AX |
| --- | --- |
| `#cmpopt optcode 0` | has an uncalled `#func` or `#cfunc`, or stores a string twice |
| `#cmpopt varname 1` | holds variable names |
| `#cmpopt optprm 0` | has no single-literal parameter flags |
| `#cmpopt utf8 1` | holds UTF-8 strings, unless it is an hsp64 program without the utf8 boot flag |
| `#bootopt ...` | has boot flags set (hsp64, nogdip, float32, orgrnd, ioresume, utf8, notimer) |
| `#runtime "..."` | names a runtime |

The test fixtures recompile byte for byte with hspcmp 3.5, 3.7 and 3.8β2.

### Text encoding

The source is written in Shift_JIS by default. hspcmp reads source as Shift_JIS, so that
is the output that rebuilds the same bytes for a Shift_JIS program.

`--utf8` writes UTF-8 without a BOM. Use it for programs compiled with UTF-8 strings, and
compile the result with `hspcmp -i` or `-j`. Without one of those switches a UTF-8 source
breaks:

- hspcmp 3.5 and 3.7 read the bytes as Shift_JIS. A UTF-8 byte such as `0x86` looks like
  the first half of a Shift_JIS character, takes the closing quote with it, and the string
  runs to the end of the line.
- hspcmp 3.8β2 detects UTF-8 and converts it to the Windows code page, so characters
  outside that code page turn into `?`.

The Shift_JIS output of a program with UTF-8 strings also recompiles, but hspcmp converts
its strings to UTF-8 through the Windows code page. Only a Japanese system (code page 932)
rebuilds the same bytes that way; on any other system use `--utf8`. hspcmp 3.8β2 stores
UTF-8 strings for every hsp64 program, so this applies to its output even without
`#cmpopt utf8 1`.

## Project Structure

| Project | Description |
| --- | --- |
| `HspDecompiler.Core` | Core decompiler library with every algorithm |
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

## Development

Tasks run through [`just`](https://github.com/casey/just); `just` lists the recipes.

```bash
just setup     # install the pinned tools
just test      # run the tests
just lint      # build with analyzers and warnings as errors
just inspect   # fail on any ReSharper InspectCode warning
just verify    # everything CI checks
```

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
| Unreleased | deHSP | 3.0 | .NET 10 + Avalonia 12; output that recompiles to the same AX; `--utf8`; see [CHANGELOG](CHANGELOG.md) |

## License

PDS (Public Domain Software) for the HSPdecom parts.
HSPdeco parts under zlib/libpng license.
See the [LICENSE](LICENSE) file for details.
