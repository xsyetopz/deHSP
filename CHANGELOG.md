# Changelog

deHSP follows [Semantic Versioning](https://semver.org/). Dates use ISO 8601.

## 3.0.0 (unreleased)

### Breaking changes

- deHSP now builds on .NET 10 and Avalonia 12. Hosts on .NET 8 have to upgrade.
- The built-in keyword dictionary is an XML file, `Dictionary.xml`, checked against
  `Dictionary.xsd`. It replaces the CSV table. `--dictionary` and
  `DecompilerPipeline.Initialize(string)` read only XML in that schema. If a file is
  missing, unreadable or fails the schema, `Initialize` logs the reason with the line
  number and returns `false`.
- `DecompilerOptions.DictionaryPath` and `DecompilerOptions.Verbose` are gone, because
  the pipeline never read them. Load a dictionary with `DecompilerPipeline.Initialize(string)`.
- An archive whose options block every entry now fails with a message. That covers an
  archive where every entry is encrypted, and one with encrypted entries when neither
  decryption nor skipping is allowed. Before, the run reported success and wrote nothing.
- The unused `ExeExtractor` class is removed. `DpmExtractor` finds the archive inside an
  executable.

### Added

- `--utf8` (`DecompilerOptions.Utf8Output`) writes the source as UTF-8 without a BOM
  instead of Shift_JIS. `DecompilerPipeline.DecodeAsync` has an overload that takes the output
  encoding. Lines the encoding cannot hold are logged as warnings.
- Strings compiled as UTF-8 decode as UTF-8 and emit `#cmpopt utf8 1`. hsp64 programs
  from hspcmp 3.8β2 hold UTF-8 strings without the utf8 boot flag; they decode as UTF-8
  too and emit no `#cmpopt utf8 1`, which would set the flag.
- The decompiled source restores the compiler options the AX records:
  - `#bootopt` lines for hsp64, nogdip, float32, orgrnd, ioresume and utf8.
  - `#bootopt notimer 1`, written only next to an `await`.
  - `#runtime`.
  - `#cmpopt varname 1` and `#cmpopt optprm 0`.
  - `#cmpopt optcode 0`, written only when the AX shows optcode was off: an uncalled
    `#func` or `#cfunc`, a `#module` nothing uses, or a string stored twice.
- Debug builds (`hspcmp -d`) decompile to their source layout. Recompiled with `hspcmp -d`, the
  output gives back the same AX, debug segment included:
  - Each included file that held code is written beside the main source, and its `#include`
    goes back on its line.
  - Each statement goes back on its source line. Statements of one line join with ` : `, and
    `{"..."}` strings get their line breaks back.
  - Variables, labels and parameters get their recorded names.
  - `#module` and `#global` blocks come back, with `#modinit`, `#modfunc`, `#modcfunc`,
    `#modterm`, `#deffunc local`, nameless `#module` and names written for the block's scope.
- Normal builds from hspcmp 3.1 to 3.32 decompile to the lines their records name. Those
  releases record each `#module` and `#global` line and, from 3.3, the line each `{"..."}`
  string ends on. The output puts each on its line and splits into `<name>_N.hsp` includes
  where the line numbers restart, so a recompile without `-d` gives back the same AX.
- `--common <dir>` (`DecompilerOptions.CommonDirectory`) points at the HSP `common` directory.
  A debug build gets back the `#include` of a common header that left only definitions, and
  the imports that header declares leave the output.
- Style options (`DecompilerOptions.Style`, a `SourceStyle`) pick the spelling of constructs
  that compile to the same bytes either way: `--if-parens`, `--one-line-if`,
  `--paren-negatives`, `--logical`, `--compact`, `--no-macros` and `--hex`. Each spelling was
  checked to compile to the same bytes on hspcmp 3.0a through 3.8β2, in normal and debug builds.
- A debug build writes an `if` or `else` block that stood on one line as `if x : y`, and a
  label pushed onto a statement by a closing brace goes back on its own line.
- Calls that an `hspdef.as` macro expands to are written as the macro, such as `dir_exe` for
  `dirinfo(1)`. `ginfo_newid`, `dir_tv` and the sensor macros are written only for AX
  version 3.5 or later, since some earlier compilers lack them.
- The CLI exits with 0 on success and 1 on any failure.
- The public API has XML documentation in English and Japanese.

### Changed

- The default HSP 3 spelling follows common HSP style. An `if` condition has no
  parentheses, a negative literal stays bare (`a - -1`), `hspdef.as` macros replace their
  expansions, and one-line blocks of a debug build use `:`. `--if-parens spaced`,
  `--paren-negatives`, `--no-macros` and `--one-line-if braces` give back the old spelling.
- HSP 3 output is closer to what hspcmp compiled:
  - Imports sit where hspcmp registered them.
  - Labels no code refers to stay in the output; labels made by `repeat` and `foreach`
    stay hidden.
  - Doubles print in the shortest form that reads back to the same bits.
- The test fixtures recompile to byte-identical AX files with hspcmp 3.5, 3.7 and 3.8β2.
- HSP 2 output keeps literals and labels as hspcmp read them, and writes each include file as
  its own source file.
- The keyword table covers HSP 3.8β2.

### Fixed

- Output of HSP 3.0 to 3.4 programs compiles with the compiler that built them. `#cmpopt
  optprm 0`, which hspcmp 3.1 to 3.21 reject, is written only for AX format 3.5 or later.
  `#cmpopt optcode 0` is no longer inferred from a repeated string before format 3.6, since
  older compilers never pool strings, nor written at all for format 3.0, which has no optcode.
- `--skip-encrypted` skips encrypted entries by itself. Before, it worked only together
  with `--no-decrypt`.
- Executables packed with UPX are read correctly. The PE section table is now read by its
  declared count, not up to the first all-zero entry.
- An executable with an `e_lfanew` of zero is rejected instead of being misread.
- Assignment RPN checks report failures, and `if`/`else` code types are compared exactly.
- `int64` parameters from hspcmp 3.8β2 decompile as `int64` instead of an invalid-type comment
  that does not compile.
- `#func` and `#cfunc` declared in the old numeric form (`#func name "name" 6`) come back in
  that form, because only it rebuilds the same AX. A bare name that hspcmp 3.7 and earlier
  decorated as `_name@16` comes back bare.
- `#func onexit` and `#cfunc onexit` are no longer dropped from the output.
- `thismod` decompiles as `thismod` instead of a comment that does not compile.
- A `#module` used only through `newmod` counts as used, so its output no longer gets a
  needless `#cmpopt optcode 0`.
- The `#struct` warning is logged only for a `#struct` line the output holds, with its line.
- The GUI logs errors from Open, drag and drop, and About instead of crashing, and keeps
  decompile continuations on the UI thread.
