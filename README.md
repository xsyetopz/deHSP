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
  -o, --output <output>              Output directory
  -d, --dictionary <dictionary>      Custom dictionary XML (default: built-in table for hspcmp 2.4 to 3.8beta2)
  --no-decrypt                       Skip encrypted file decryption
  --skip-encrypted                   Extract only non-encrypted files
  --utf8                             Write UTF-8 without a BOM instead of Shift_JIS; recompile with hspcmp -i or -j
  --common <common>                  HSP common directory; restores the #include of a matching common header in a debug build
  --if-parens <None|Spaced|Tight>    Parentheses around an if condition: none, tight '(x)', spaced '( x )' [default: None]
  --one-line-if <Braces|Colon>       An if block on one source line (debug builds): colon 'if x : y', braces [default: Colon]
  --paren-negatives                  Write a negative literal beside an operator as 'a - (-1)'
  --logical <Doubled|Symbols|Words>  & and | between conditions: symbols, doubled '&&', words 'and' [default: Symbols]
  --compact                          Write binary operators without spaces: 'a+1'
  --no-macros                        Write hspdef.as macro calls expanded: 'dirinfo(1)' for 'dir_exe'
  --hex <All|None|Poke>              Integers in hexadecimal: none, poke (poke values of 128 or more), all [default: None]
  -v, --verbose                      Verbose logging
  -?, -h, --help                     Show help and usage information
  --version                          Show version information
```

`dehsp` exits with 0 on success and 1 on any failure: a bad argument, a dictionary that
does not load, or an input it cannot decompile.

### GUI

```bash
dotnet run --project src/HspDecompiler.Gui
```

Or drag-and-drop files onto the application window. The Style menu holds the same source style
settings as the CLI options in [Source style](#source-style).

## Output

An HSP 3 program becomes one `.hsp` file. An HSP 2 program becomes an `.as` file, and each
file it included becomes its own `.as` file next to it, referenced by `#include`.

### Recompiling

The output aims to compile back to the same AX bytes. The first lines restore the options
the AX records, so hspcmp builds it the same way:

| Line | Written when the AX |
| --- | --- |
| `#cmpopt optcode 0` | has an uncalled `#func` or `#cfunc` or a `#module` nothing uses (format 3.1 or later), or stores a string twice (3.6 or later, the first to pool strings) |
| `#cmpopt varname 1` | holds variable names and is not a debug build |
| `#cmpopt optprm 0` | has no single-literal parameter flags (format 3.5 or later; hspcmp 3.1 to 3.21 reject the line) |
| `#cmpopt utf8 1` | holds UTF-8 strings, unless it is an hsp64 program without the utf8 boot flag |
| `#bootopt ...` | has boot flags set (hsp64, nogdip, float32, orgrnd, ioresume, utf8, notimer) |
| `#runtime "..."` | names a runtime |

The test fixtures recompile byte for byte with hspcmp 3.5, 3.7 and 3.8β2.

### Debug builds

A program compiled in debug mode (`hspcmp -d`) carries the file names, the source line of
every statement, and the names of its variables, labels and parameters. deHSP rebuilds the
source from them:

- Each file that held code is written next to the main source under its recorded name, and
  its `#include` goes back on its original line.
- Each statement goes back on its source line. Statements that shared a line are joined with
  ` : `, and `{"..."}` strings get their line breaks back.
- Variables, labels and parameters get their original names.
- `#module` and `#global` blocks come back, with `#modinit`, `#modfunc`, `#modcfunc`,
  `#modterm`, `#deffunc local`, nameless `#module` and `thismod`. Names inside a block are
  written for its scope: `val` for its own variable, `gcount@` for a global one, `*init@` for
  a global label.
- `--common` points at the HSP `common` directory. A common header that left only
  definitions behind, such as `hspcmp.as`, gets its `#include` back, and the output drops the
  imports it declares. Without `--common` that `#include` stays as a comment.

Recompile the output with `hspcmp -d` to get the same debug segment. The debug fixtures and a
41 KB program built by hspcmp 3.8β2 rebuild byte for byte. Two limits remain:

- The AX does not hold comments, `#define` macros, the case of names, or the alias of a
  `#func` that no common header declares, so those cannot come back.
- The 64-bit hspcmp.exe of 3.8β2 sets hsp64 and `#runtime "hsp3_64"` without being asked.
  An AX names that compiler when a user function shows its 64-bit parameter frames, or when a
  debug build has code on every source line, so no line could have held a `#runtime`. Such
  output has no `#runtime` and rebuilds with that hspcmp.exe. Any other hsp64 output gets
  `#runtime "hsp3_64"`, which sets hsp64 as well, in the first line without code. A source
  with no such line and an `#include` gets it after the last line, and a recompile records
  that one extra line. The 32-bit hspcmp.dll writes 32-bit parameter frames, so it cannot
  rebuild a 64-bit AX that has user functions.

### Normal builds from hspcmp 3.1 to 3.32

hspcmp 3.1 to 3.32 write line records even without `-d`: one for each `#module` and `#global`
line, and from 3.3 one for the line each `{"..."}` string ends on. The records hold no file
names, and their line numbers restart inside each included file. deHSP matches each record to
a module block or a string and rebuilds the source around them:

- The `#module` and `#global` blocks come back on their recorded lines. A normal build keeps
  no module names, so a block is named after its `#modinit` or `#modfunc` members or a
  `local` function where it has them, and is written as a nameless `#module` otherwise.
- `{"..."}` strings end on their recorded lines.
- The other lines keep a line each where they fit. Otherwise blank lines drop, then a
  statement joins the line before it with ` : `.
- Where the line numbers restart, the rest of the source goes to `<name>_1.hsp`,
  `<name>_2.hsp` and so on, each included at the end of the file before it.

Recompiled without `-d` by the same hspcmp, the output gives back the same AX, records
included; the normal-build fixtures and 80 probe programs checked on hspcmp 3.1, 3.21, 3.3
and 3.32 do. hspcmp 3.4 and later write no such records. When no layout fits the records,
deHSP writes the lines in order; a recompile then gives the same code and data, and only the
records differ.

deHSP tells these builds apart by the AX format word, the records and the `optprm` flags, and
narrows the candidate releases from them. A directive or a macro that only some candidates
accept is written only when every candidate accepts it. A record that no block or string
explains came from a macro whose expansion broke into more lines than the source took; the AX
keeps no macro bodies, so the output defines `#define global __line_record(%1) %c` on line 1
and writes `__line_record` on the line before each such record.

### HSP 2 programs

deHSP reads the AX of every hspcmp from 2.4 to 2.61. Each format word names its release
(0x203 for 2.4, 0x205 for 2.5, 0x255 for 2.55, 0x206 for 2.6 and 2.61), so none needs
`--compiler`. HSP 2.4 has no `{ }` blocks, `#deffunc` or `#module`: an `if` takes the rest of
its line, so its block comes back as `if x : a : else : b`, and a string longer than the 248
bytes 2.4 keeps on one line comes back as a `{"..."}` block. The 2.4 samples that compile
with 2.4 rebuild to the same AX, apart from the first label word, which hspcmp 2.4 leaves
uninitialized. The AX records the main file's name as hspcmp got it, which can be a full
path; a rebuild from another directory writes a different name.

### Source style

Some constructs compile to the same bytes however they are spelled. The style options pick
the spelling; every choice recompiles to the same AX. The defaults follow common HSP style:

| Option | Default | Other spellings |
| --- | --- | --- |
| `--if-parens` | `if x == 1 {` | `tight`: `if (x == 1) {`, `spaced`: `if ( x == 1 ) {` |
| `--one-line-if` | `if x : mes "a"` | `braces`: `if x { mes "a" }` |
| `--paren-negatives` | `a - -1` | `a - (-1)` |
| `--logical` | `a == 1 & b == 2` | `doubled`: `(a == 1) && (b == 2)`; `words`: `(a == 1) and (b == 2)` |
| `--compact` | `a + 1` | `a+1` |
| `--no-macros` | `dir_exe`, `ginfo_mx` | `dirinfo(1)`, `ginfo(0)` |
| `--hex` | `poke b, 0, 239` | `poke`: `poke b, 0, 0xEF` for a `poke`, `wpoke` or `lpoke` value of 128 or more; `all`: every integer |

- `--one-line-if colon` applies to debug builds, where the line table shows which blocks
  stood on one line. A block keeps its braces when a multi-line `{"..."}` string runs through
  its line, when its body holds anything but statements, or when its `else` starts on another
  line.
- Without `--no-macros`, an `hspdef.as` macro is written only where the compiler that built
  the AX has it. hspcmp 3.0a to 3.4 lack `ginfo_newid`, `dir_tv` or the sensor macros such
  as `ginfo_accelx` in some releases, and 3.1 to 3.4 share one AX version, so those macros
  are written only for AX version 3.5 or later.

Each spelling was checked against hspcmp 3.0a, 3.1, 3.2, 3.21, 3.3, 3.31, 3.32, 3.4, 3.5,
3.51, 3.6, 3.7 and 3.8β2, in normal and debug builds: each pair of spellings compiled to
the same bytes on every one. The only exceptions were the macros above on compilers before 3.5,
which do not define them.

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

One keyword table covers every hspcmp release the decompiler reads, HSP 2.4 to 3.8β2, and is
built into the decompiler, so no file has to sit next to it. It holds the built-in commands,
functions, and parameter types, and the hspdef.as macros such as `dir_exe`. The source is
[`Dictionary.xml`](src/HspDecompiler.Core/Keywords/Dictionary.xml).

Each entry names the releases that have it with `since` and `until`, and a `codes` or `macros`
section gives the default for the entries inside it. One code can therefore carry a different
name in different releases: `0x0F 0x1E` is `resume` up to 3.6 and `varprop` from 3.7. The
decompiler uses the table twice. The codes a program uses rule out the releases that lack
them, so a program that calls `strrep` is not taken for 3.3. Once the release is inferred, each
code takes the name that release gives it. When the program cannot tell two releases apart,
the newer name wins, and `--compiler` settles it.

To use your own table, copy `Dictionary.xml`, edit it, and pass it with `--dictionary`.
[`Dictionary.xsd`](src/HspDecompiler.Core/Keywords/Dictionary.xsd) defines the format, and an
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
