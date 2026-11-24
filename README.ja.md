# HSP Decompiler (deHSP)

日本語 | [English](README.md)

HSP（Hot Soup Processor）でコンパイルされたファイル（`.ax`、`.exe`、`.dpm`）を、ソース
（`.hsp`、`.as`）に戻すツールです。Windows・macOS・Linux で動き、GUI 版と CLI 版があります。

## 3.0 の新機能

- HSP 1.1 から 3.8β2 まで、すべてのバージョンに対応しました。
- 出力を同じコンパイラで再コンパイルすると、元と同じ AX に戻ります。
- デバッグビルドからは、変数名・ラベル名・行の位置・`#include` したファイルを復元します。
- `#bootopt`・`#runtime`・`#cmpopt` など、AX に残るコンパイル設定を復元します。
- `--utf8` で UTF-8 のソースを書き出せます。
- .NET 10 と Avalonia 12 になりました。

変更点の一覧は [CHANGELOG](CHANGELOG.md) にあります。

## ダウンロード

[Releases](https://github.com/xsyetopz/deHSP/releases) から OS に合った zip をダウンロードして
展開してください。GUI 版の `deHSP` と CLI 版の `deHSP-cli` が入っています。.NET を
インストールする必要はありません。

## 対応するバージョン

| HSP | コンパイラ | 出力 |
| --- | --- | --- |
| 1.1、1.2f | ASC.EXE（デバッグビルドと `/r` ビルド） | `.as` |
| 2.3d〜2.61 | hspcmp | `.as`（取り込んだファイルは別の `.as`） |
| 3.0a〜3.8β2 | hspcmp（通常ビルドとデバッグビルド） | `.hsp` |

`.exe` と `.dpm` からは中のファイルを取り出します。UPX で圧縮された `.exe` と、暗号化された
DPM のファイルにも対応します。

## 使い方

### GUI

アプリを起動し、ファイルをウィンドウにドラッグ＆ドロップします。「スタイル」メニューで出力の
書き方を選べます。

### CLI

```bash
deHSP-cli input.ax                # input.ax をデコンパイル
deHSP-cli input.ax -o ./output    # 出力先を指定
deHSP-cli game.exe -o ./extracted # 実行ファイルの中身を取り出してデコンパイル
```

```text
deHSP-cli <入力ファイル> [オプション]
  -o, --output <output>              出力ディレクトリ
  -d, --dictionary <dictionary>      独自の辞書 XML（デフォルト: 組み込みの ASC 1.1〜hspcmp 3.8β2 の表）
  --no-decrypt                       暗号化されたファイルを復号しない
  --skip-encrypted                   暗号化されていないファイルだけを取り出す
  --utf8                             Shift_JIS ではなく BOM なしの UTF-8 で書く（hspcmp -i か -j で再コンパイル）
  --common <common>                  HSP の common ディレクトリ。デバッグビルドで共通ヘッダーの #include を戻す
  --compiler <compiler>              プログラムを書いた hspcmp のバージョン（3.32、3.8b2 など）
  --if-parens <None|Spaced|Tight>    if の条件の括弧: 無し、'(x)'、'( x )'（デフォルト: None）
  --one-line-if <Braces|Colon>       1 行の if ブロック（デバッグビルド）: 'if x : y'、波括弧（デフォルト: Colon）
  --paren-negatives                  演算子の隣の負の数を 'a - (-1)' と書く
  --logical <Doubled|Symbols|Words>  条件をつなぐ & と |: 記号、'&&'、'and'（デフォルト: Symbols）
  --compact                          演算子の前後に空白を入れない: 'a+1'
  --no-macros                        hspdef.as のマクロを展開して書く: 'dir_exe' ではなく 'dirinfo(1)'
  --hex <All|None|Poke>              整数を 16 進数で書く: 無し、poke の 128 以上の値、すべて（デフォルト: None）
  -v, --verbose                      詳しいログを出す
  -?, -h, --help                     ヘルプを表示
  --version                          バージョンを表示
```

`deHSP-cli` は成功すると 0、失敗すると 1 で終了します。

書き方のオプション（`--if-parens` から `--hex` まで）は見た目だけを変えます。どれを選んでも、
再コンパイルの結果は同じです。

## 戻るもの・戻らないもの

出力は、元のプログラムをビルドしたコンパイラで再コンパイルすると、同じ AX に戻るように
書かれます。ソースは既定で Shift_JIS で書き出すため、日本語の文字列もそのままコンパイル
できます。

- **デバッグビルド**（`hspcmp -d`）は名前と行の位置を持っているため、元に近いソースに戻ります。
- **通常ビルド**は名前を残さないため、変数は `var_0`、ラベルは `*label_0` のような名前に
  なります（HSP 1 では `v0`、`*l0`）。`#cmpopt varname 1` でビルドした HSP 3 のプログラムは
  変数名が戻ります。
- **コメント**、`#define` のマクロ、名前の大文字・小文字は AX に残らないため、戻せません。
- UTF-8 の文字列を持つプログラムは `--utf8` で書き出し、`hspcmp -i` か `-j` でコンパイルして
  ください。

確かめた範囲と、元と違う AX になる例外は [CHANGELOG](CHANGELOG.md) にあります。

## 辞書

命令や関数の名前は、組み込みのキーワード表
[`Dictionary.xml`](src/HspDecompiler.Core/Keywords/Dictionary.xml) から取ります。表には HSP 1.1
から 3.8β2 までの全バージョンが入っており、プログラムが使う命令からコンパイラのバージョンを
推定します。推定が外れる時は `--compiler` で指定してください。

独自の表を使う時は `Dictionary.xml` をコピーして編集し、`--dictionary` で渡します。形式は
[`Dictionary.xsd`](src/HspDecompiler.Core/Keywords/Dictionary.xsd) にあります。

## ソースからビルド

.NET 10 SDK が必要です。

```bash
dotnet build HspDecompiler.sln
dotnet run --project src/HspDecompiler.Cli -- input.ax   # CLI
dotnet run --project src/HspDecompiler.Gui               # GUI
```

開発の作業は [`just`](https://github.com/casey/just) で行います。`just verify` で CI と同じ検査を
すべて実行します。

## 歴史

| ソフト名 | 作者 | 説明 |
| --- | --- | --- |
| [HSP逆コンパイラ](http://www.vector.co.jp/soft/win95/prog/se390297.html) | [きつつき](http://www.vector.co.jp/vpack/browse/person/an043697.html) | オリジナルのソフト |
| [HSPdeco](https://osdn.jp/projects/hspdeco/) | [minorshift](https://osdn.jp/users/minorshift/) | オリジナルの改良版 |
| [HSPdecom](http://stpr18.blogspot.jp/2015/10/hspdecohspelona.html) | [したぷる](https://www.blogger.com/profile/00794326060600750840)、[YSRKEN](https://github.com/YSRKEN) | HSPdeco の改良版 |
| [HSPdeco のパッチ](http://vivibit.net/hspdeco/) | [xx2zz](http://vivibit.net/about/) | 復号に失敗する時の対策パッチ |

| 日付 | ソフト名 | バージョン | 説明 |
| --- | --- | --- | --- |
| 2006-01-28 | HSP逆コンパイラ | 1.0 | シェアウェアとして公開 |
| 2007-09-10 | HSP逆コンパイラ | 1.1 | HSP3 に対応 |
| 2010-09-12 | HSP逆コンパイラ | 1.2 | PDS・OSS になり、バグを修正 |
| 2012-01-13 | HSPdeco | 1.0 | ax ファイルのデコード機能を公開、バグを修正 |
| 2015-12-15 | HSPdecom | 1.0 | 変数名の復元、辞書データを追加 |
| 2016-08-16 | HSPdecom | 1.1 | パッチをすべて適用し、GitHub に公開し直し |
| 2026-03-13 | deHSP | 2.0 | .NET 8 + Avalonia、CLI ツール、クロスプラットフォーム対応 |
| 2026-11-24 | deHSP | 3.0 | HSP 1.1〜3.8β2 に対応、同じ AX に戻る出力、.NET 10 + Avalonia 12 |

## ライセンス

HSPdecom の部分は PDS、HSPdeco の部分は zlib/libpng ライセンスです。詳しくは [LICENSE](LICENSE) を
参照してください。
