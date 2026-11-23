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
- Strings compiled as UTF-8 decode as UTF-8 and emit `#cmpopt utf8 1`.
- The decompiled source restores the compiler options the AX records:
  - `#bootopt` lines for hsp64, nogdip, float32, orgrnd, ioresume and utf8.
  - `#bootopt notimer 1`, written only next to an `await`.
  - `#runtime`.
  - `#cmpopt varname 1` and `#cmpopt optprm 0`.
  - `#cmpopt optcode 0`, written only when the AX shows optcode was off: an uncalled
    `#func` or `#cfunc`, or a string stored twice.
- The CLI exits with 0 on success and 1 on any failure.
- The public API has XML documentation in English and Japanese.

### Changed

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

- `--skip-encrypted` skips encrypted entries by itself. Before, it worked only together
  with `--no-decrypt`.
- Executables packed with UPX are read correctly. The PE section table is now read by its
  declared count, not up to the first all-zero entry.
- An executable with an `e_lfanew` of zero is rejected instead of being misread.
- Assignment RPN checks report failures, and `if`/`else` code types are compared exactly.
- The GUI logs errors from Open, drag and drop, and About instead of crashing, and keeps
  decompile continuations on the UI thread.
