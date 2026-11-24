# HSP Decompiler (deHSP)

[日本語](README.ja.md) | English

deHSP turns compiled HSP (Hot Soup Processor) files (`.ax`, `.exe`, `.dpm`) back into source
code (`.hsp`, `.as`). It runs on Windows, macOS and Linux, as a GUI app and a command-line tool.

## What's New in 3.0

- Every HSP release from 1.1 to 3.8β2 is supported.
- Recompile the output with the compiler that built the program, and you get the same AX back.
- Debug builds get back their variable names, label names, line positions and included files.
- The compiler settings an AX keeps, such as `#bootopt`, `#runtime` and `#cmpopt`, come back.
- `--utf8` writes the source as UTF-8.
- deHSP now runs on .NET 10 and Avalonia 12.

The [CHANGELOG](CHANGELOG.md) lists every change.

## Download

Download the zip for your system from [Releases](https://github.com/xsyetopz/deHSP/releases)
and extract it. It holds the GUI app, `deHSP`, and the command-line tool, `deHSP-cli`. You
don't need to install .NET.

## Supported Versions

| HSP | Compiler | Output |
| --- | --- | --- |
| 1.1, 1.2f | ASC.EXE (debug and `/r` builds) | `.as` |
| 2.3d to 2.61 | hspcmp | `.as` (each included file as its own `.as`) |
| 3.0a to 3.8β2 | hspcmp (normal and debug builds) | `.hsp` |

deHSP also extracts the files inside `.exe` and `.dpm` files, including UPX-packed executables
and encrypted DPM entries.

## Usage

### GUI

Start the app and drag files onto the window. The Style menu sets how the output is written.

### CLI

```bash
deHSP-cli input.ax                # decompile input.ax
deHSP-cli input.ax -o ./output    # choose the output directory
deHSP-cli game.exe -o ./extracted # extract an executable and decompile what it holds
```

```text
deHSP-cli <input-file> [options]
  -o, --output <output>              Output directory
  -d, --dictionary <dictionary>      Custom dictionary XML (default: built-in table for ASC 1.1 to hspcmp 3.8beta2)
  --no-decrypt                       Skip encrypted file decryption
  --skip-encrypted                   Extract only non-encrypted files
  --utf8                             Write UTF-8 without a BOM instead of Shift_JIS; recompile with hspcmp -i or -j
  --common <common>                  HSP common directory; restores the #include of a matching common header in a debug build
  --compiler <compiler>              hspcmp release that wrote the program, such as 3.32 or 3.8b2
  --if-parens <None|Spaced|Tight>    Parentheses around an if condition: none, '(x)', '( x )' [default: None]
  --one-line-if <Braces|Colon>       An if block on one source line (debug builds): 'if x : y', braces [default: Colon]
  --paren-negatives                  Write a negative literal beside an operator as 'a - (-1)'
  --logical <Doubled|Symbols|Words>  & and | between conditions: symbols, '&&', 'and' [default: Symbols]
  --compact                          Write binary operators without spaces: 'a+1'
  --no-macros                        Write hspdef.as macro calls expanded: 'dirinfo(1)' for 'dir_exe'
  --hex <All|None|Poke>              Integers in hexadecimal: none, poke values of 128 or more, all [default: None]
  -v, --verbose                      Verbose logging
  -?, -h, --help                     Show help and usage information
  --version                          Show version information
```

`deHSP-cli` exits with 0 on success and 1 on any failure.

The style options (`--if-parens` through `--hex`) change only how the code looks. Whichever you
pick, the recompiled result is the same.

## What Comes Back and What Doesn't

The output is written so that the compiler that built the program recompiles it to the same AX.
Source is written in Shift_JIS by default, so Japanese text compiles as it is.

- **Debug builds** (`hspcmp -d`) keep names and line positions, so the source comes back close
  to the original.
- **Normal builds** keep no names, so variables become `var_0` and labels `*label_0` (`v0` and
  `*l0` in HSP 1). An HSP 3 program built with `#cmpopt varname 1` gets its variable names back.
- **Comments**, `#define` macros and the case of names are not in the AX and cannot come back.
- For a program with UTF-8 strings, write the output with `--utf8` and compile it with
  `hspcmp -i` or `-j`.

The [CHANGELOG](CHANGELOG.md) lists what was tested and the cases that rebuild to a different
AX.

## Dictionary

Command and function names come from the built-in keyword table,
[`Dictionary.xml`](src/HspDecompiler.Core/Keywords/Dictionary.xml). It covers every release
from HSP 1.1 to 3.8β2, and deHSP works out the compiler release from the commands a program
uses. If it guesses wrong, set the release with `--compiler`.

To use your own table, copy `Dictionary.xml`, edit it, and pass it with `--dictionary`.
[`Dictionary.xsd`](src/HspDecompiler.Core/Keywords/Dictionary.xsd) defines the format.

## Building from Source

You need the .NET 10 SDK.

```bash
dotnet build HspDecompiler.sln
dotnet run --project src/HspDecompiler.Cli -- input.ax   # CLI
dotnet run --project src/HspDecompiler.Gui               # GUI
```

Development tasks run through [`just`](https://github.com/casey/just). `just verify` runs every
check CI runs.

## History

| Name | Author | Notes |
| --- | --- | --- |
| [HSP逆コンパイラ](http://www.vector.co.jp/soft/win95/prog/se390297.html) | [Kitsutsuki](http://www.vector.co.jp/vpack/browse/person/an043697.html) | The original |
| [HSPdeco](https://osdn.jp/projects/hspdeco/) | [minorshift](https://osdn.jp/users/minorshift/) | An improved version of the original |
| [HSPdecom](http://stpr18.blogspot.jp/2015/10/hspdecohspelona.html) | [Sitapuru](https://www.blogger.com/profile/00794326060600750840), [YSRKEN](https://github.com/YSRKEN) | An improved version of HSPdeco |
| [HSPdeco patch](http://vivibit.net/hspdeco/) | [xx2zz](http://vivibit.net/about/) | A patch for failed decryption |

| Date | Name | Version | Notes |
| --- | --- | --- | --- |
| 2006-01-28 | HSP逆コンパイラ | 1.0 | Released as shareware |
| 2007-09-10 | HSP逆コンパイラ | 1.1 | HSP3 support |
| 2010-09-12 | HSP逆コンパイラ | 1.2 | Became PDS/OSS; bug fixes |
| 2012-01-13 | HSPdeco | 1.0 | `.ax` decoding exposed; bug fixes |
| 2015-12-15 | HSPdecom | 1.0 | Variable name restoration; dictionary data |
| 2016-08-16 | HSPdecom | 1.1 | All patches applied; republished on GitHub |
| 2026-03-13 | deHSP | 2.0 | .NET 8 + Avalonia; CLI tool; cross-platform |
| 2026-11-24 | deHSP | 3.0 | HSP 1.1 to 3.8β2; output that rebuilds the same AX; .NET 10 + Avalonia 12 |

## License

The HSPdecom parts are PDS (public domain); the HSPdeco parts are under the zlib/libpng
license. See [LICENSE](LICENSE) for details.
