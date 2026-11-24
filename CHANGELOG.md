# Changelog

All notable changes to this project are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this
project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [3.0.0] - 2026-11-24

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
  - The candidate releases are narrowed from the AX format word, the records and the `optprm`
    flags, and a directive or macro is written only when every candidate accepts it.
  - A record that no block or string explains came from a macro that broke into more lines;
    the output defines `__line_record` and writes it where such a macro stood.
  - A label on the code a `#global` falls on goes after the `#global`, as `*label_1`.
  - When no layout fits the records, the lines come out in order and recompile to the same
    code and data.
- `--common <dir>` (`DecompilerOptions.CommonDirectory`) points at the HSP `common` directory.
  A debug build gets back the `#include` of a common header that left only definitions, and
  the imports that header declares leave the output.
- Style options (`DecompilerOptions.Style`, a `SourceStyle`) pick the spelling of constructs
  that compile to the same bytes either way: `--if-parens`, `--one-line-if`,
  `--paren-negatives`, `--logical`, `--compact`, `--no-macros` and `--hex`. Each spelling was
  checked to compile to the same bytes on hspcmp 3.0a through 3.8β2, in normal and debug builds.
- The GUI has a Style menu with the same settings as the CLI style options.
- A debug build writes an `if` or `else` block that stood on one line as `if x : y`, and a
  label pushed onto a statement by a closing brace goes back on its own line.
- Calls that an `hspdef.as` macro expands to are written as the macro, such as `dir_exe` for
  `dirinfo(1)`. `ginfo_newid`, `dir_tv` and the sensor macros are written only for AX
  version 3.5 or later, since some earlier compilers lack them.
- Programs from hspcmp 2.5 and 2.55 decompile and recompile to the same AX:
  - Each release has its own AX format word, so neither needs `--compiler`. 2.5 packs a token
    value in one byte and follows a larger one with 16 or 32 bits.
  - Neither release includes `hspdef.as` by itself, so the first file switch names the main
    file. A 2.5 `repeat` carries no loop-end label, so the label after it is read as a label
    of its own.
  - `#module` and `#global` blocks come back from the module records these releases write.
    A nameless block stays nameless, `#deffunc` names lose the `@module` suffix hspcmp stores,
    and a variable or `#func` name written in a block that does not own it gets `@` or
    `@module`.
  - One limit remains: hspcmp can write no file switch back from an include, as after the
    `#include "loadlib.as"` inside `#deffunc setpoke` in llmod's `_poke.as`. Where that
    include ends cannot be told from the AX, so `llmod/mcn/_ddrop_mcn.as` on 2.55 recompiles
    to a different AX.
- Programs from hspcmp 2.3d to 2.4h decompile and recompile to the same AX:
  - 2.3d to 2.4f write format word 0x203, and 2.4g and 2.4h write 0x204. The header is 64
    bytes, without the `#deffunc` and `#module` tables 2.5 added, and its tokens pack as in
    2.5.
  - The keyword table marks the keywords each 2.4 update added or renamed, so the commands a
    program calls narrow the release: `strmid` means 2.4d or later and `continue` 2.4f or
    later. Code 0x48 0x42 is `hspmode` up to 2.4e and `keysend` from 2.4f; when the AX does not
    settle which, the newer name is written with a warning, and `--compiler` picks the other.
  - 2.3d and 2.4 have no `{ }` blocks, so each `if` and `else` block goes on its `if` line,
    joined with ` : `. A string longer than the 248 bytes these releases read on one line goes
    in a `{"..."}` block.
  - The 16 programs of the 2.4 SAMPLE and SSAVER folders that compile, a fixture of the 2.4
    tokens and one of the 2.4d and 2.4f keywords rebuild with START.AX on each of 2.3d, 2.4,
    2.4c, 2.4e, 2.4f and 2.4h to the same AX, apart from the first label word. SAMPLES.AS
    defines `*endbtn` and `*owari` twice, so no release compiles it.
  - The `obj` that the 2.3d run command writes holds a line table, and the output does not
    keep the source's blank and comment lines, so a rebuild differs from it in that table.
- Programs from HSP 1.1 and 1.2f decompile and recompile to the same AX. deHSP reads the
  "Aspo" AX that ASC.EXE writes, in debug and `/r` builds, and the plain DPM that DPM.EXE packs:
  - 1.1 (ASC ver.A6) and 1.2f (ver.A5) share one format; the index of the first user variable,
    0x10 or 0x20, tells them apart, since 1.2f gives map commands codes that are operators in
    1.1.
  - ASC reads at most 255 bytes of a line. A string that would make a line longer, and one
    with a `\"` inside it, which ends an ASC literal, come back as `"text"+` continuation
    lines, which ASC appends to the string before them.
  - TEST.AS, MAPED.AS and SAMP.AS from both releases, and the shipped TEST.AX and TEST.DPM,
    rebuild to the same AX as debug and `/r` builds on the ASC that wrote them.
  - deHSP warns where a rebuild can differ: stored bytes no literal produces, label references
    ASC left as indices after the first `end`, labels or locals out of ASC's order, a debug
    build with too few free lines for a string's continuations, and unknown tokens.
- The CLI exits with 0 on success and 1 on any failure.
- The public API has XML documentation in English and Japanese.

### Changed

- **Breaking:** deHSP builds on .NET 10 and Avalonia 12. Hosts on .NET 8 have to upgrade.
- **Breaking:** the built-in keyword dictionary is an XML file, `Dictionary.xml`, checked
  against `Dictionary.xsd`. It replaces the CSV table. `--dictionary` and
  `DecompilerPipeline.Initialize(string)` read only XML in that schema. If a file is
  missing, unreadable or fails the schema, `Initialize` logs the reason with the line
  number and returns `false`.
- **Breaking:** the keyword dictionary covers every release from HSP 1.1 to 3.8β2 and moved
  to `src/HspDecompiler.Core/Keywords/`. Entries carry `since` and `until`, `codes` and
  `macros` sections set their defaults, and a code can have one entry per release range. The
  HSP 2 built-in names and the hspdef.as macros moved from code into the file, so a custom
  dictionary without a `macros` section writes no macro names. One code listed twice for the
  same release is an error that names its line.
- **Breaking:** an archive whose options block every entry fails with a message. That covers
  an archive where every entry is encrypted, and one with encrypted entries when neither
  decryption nor skipping is allowed. Before, the run reported success and wrote nothing.
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

### Removed

- **Breaking:** `DecompilerOptions.DictionaryPath` and `DecompilerOptions.Verbose`, which the
  pipeline never read. Load a dictionary with `DecompilerPipeline.Initialize(string)`.
- **Breaking:** the unused `ExeExtractor` class. `DpmExtractor` finds the archive inside an
  executable.

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
- An hsp64 program from hspcmp 3.8β2 gets `#runtime "hsp3_64"` without `#bootopt hsp64 1`,
  since the runtime line sets the bit by itself. When user functions show 64-bit parameter
  frames, or a debug build has code on every source line, the program is taken for the 64-bit
  hspcmp.exe, which writes that runtime by itself, so the output has no `#runtime` and
  rebuilds byte for byte.
- HSP 2 include files are placed by the file switches even when a return skips a level, as
  after an include whose last line is an `#include`. An include that ends in `#func` lines or
  in `#global` ends there, so the lines after it stay in the parent.
- Only hspcmp 2.61 accepts a `#deffunc` named after a built-in, so such a program is taken for
  2.61 instead of 2.6.
- The `#struct` warning is logged only for a `#struct` line the output holds, with its line.
- The GUI logs errors from Open, drag and drop, and About instead of crashing, and keeps
  decompile continuations on the UI thread.

[Unreleased]: https://github.com/xsyetopz/deHSP/compare/v3.0.0...HEAD
[3.0.0]: https://github.com/xsyetopz/deHSP/compare/v2.2.0...v3.0.0
