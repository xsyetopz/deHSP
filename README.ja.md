# HSP Decompiler (deHSP)

HSP（Hot Soup Processor）2/3でコンパイルされたファイル（`.ax`、`.exe`、`.dpm`）をソースファイル（`.hsp`、`.as`）に戻すツールです。

## 機能

- HSP3（`.ax` → `.hsp`）およびHSP2（`.ax` → `.as`）のデコンパイル
- DPMアーカイブからのファイル抽出
- HSP実行ファイル（`.exe`）からのDPM抽出（UPX で圧縮されたものも含む）
- DPMアーカイブ内の暗号化ファイルの復号（XOR+ADDブルートフォース）
- クロスプラットフォーム対応（Windows、macOS、Linux）
- CLIツールおよびGUIアプリケーション

## 動作環境

- .NET 10 SDK

## ビルド

```bash
dotnet build HspDecompiler.sln
```

## 使い方

### CLI

```bash
# .axファイルをデコンパイル
dotnet run --project src/HspDecompiler.Cli -- input.ax

# 出力ディレクトリを指定してデコンパイル
dotnet run --project src/HspDecompiler.Cli -- input.ax -o ./output

# DPMアーカイブを展開
dotnet run --project src/HspDecompiler.Cli -- archive.dpm -o ./extracted

# HSP実行ファイルから展開
dotnet run --project src/HspDecompiler.Cli -- game.exe -o ./extracted
```

オプション:

```text
dehsp <入力ファイル> [オプション]
  -o, --output <output>          出力ディレクトリ
  -d, --dictionary <dictionary>  独自の辞書 XML（デフォルト: 組み込みの HSP 3.8beta2 の表）
  --no-decrypt                   暗号化ファイルを復号しない
  --skip-encrypted               暗号化されていないファイルだけを取り出す
  --utf8                         Shift_JIS ではなく BOM なしの UTF-8 で書く（hspcmp -i か -j で再コンパイル）
  -v, --verbose                  詳細なログを出す
  -?, -h, --help                 ヘルプと使い方を表示
  --version                      バージョンを表示
```

`dehsp` は成功すると 0、失敗すると 1 で終了します。失敗には、引数の誤り、辞書を読み込めない
場合、デコンパイルできない入力が含まれます。

### GUI

```bash
dotnet run --project src/HspDecompiler.Gui
```

またはファイルをアプリケーションウィンドウにドラッグ＆ドロップしてください。

## 出力

HSP 3 のプログラムは 1 つの `.hsp` ファイルになります。HSP 2 のプログラムは `.as` ファイルに
なり、取り込んでいたファイルはそれぞれ隣に別の `.as` ファイルとして書き出され、`#include` で
参照されます。

### 再コンパイル

出力は元と同じ AX のバイト列に戻ることを目指しています。先頭の行で AX に記録された
オプションを復元するため、hspcmp は同じ設定でビルドします。

| 行 | AX が次の条件のときに書く |
| --- | --- |
| `#cmpopt optcode 0` | 呼ばれない `#func`・`#cfunc` がある、または同じ文字列を 2 回格納している |
| `#cmpopt varname 1` | 変数名を含む |
| `#cmpopt optprm 0` | 1 つのリテラルだけの引数の印が無い |
| `#cmpopt utf8 1` | UTF-8 の文字列を含む |
| `#bootopt ...` | 起動フラグが立っている（hsp64、nogdip、float32、orgrnd、ioresume、utf8、notimer） |
| `#runtime "..."` | ランタイム名を持つ |

テスト用のフィクスチャは hspcmp 3.5、3.7、3.8β2 でバイト単位まで同じ AX に再コンパイルされます。

### 文字コード

ソースは既定で Shift_JIS で書き出します。hspcmp はソースを Shift_JIS として読むため、
Shift_JIS のプログラムを同じバイト列に戻せるのはこの出力です。

`--utf8` は BOM なしの UTF-8 で書き出します。UTF-8 の文字列でコンパイルされたプログラムに
使い、結果は `hspcmp -i` か `-j` でコンパイルしてください。どちらのスイッチも無いと UTF-8 の
ソースは壊れます。

- hspcmp 3.5 と 3.7 はバイト列を Shift_JIS として読みます。`0x86` のような UTF-8 のバイトが
  Shift_JIS の文字の 1 バイト目に見え、閉じ引用符を巻き込むため、文字列が行末まで続きます。
- hspcmp 3.8β2 は UTF-8 を検出して Windows のコードページに変換するため、そのコードページに
  無い文字は `?` になります。

## プロジェクト構成

| プロジェクト | 説明 |
| --- | --- |
| `HspDecompiler.Core` | コアライブラリ（全アルゴリズム） |
| `HspDecompiler.Cli` | コマンドラインインターフェース |
| `HspDecompiler.Gui` | Avalonia クロスプラットフォームGUI |

## 辞書

HSP 3 のキーワード表はデコンパイラーに組み込まれているため、隣にファイルを置く必要はありません。
HSP 3.8beta2 の組み込み命令・関数・引数の型を収録しています。元データは
[`Dictionary.xml`](src/HspDecompiler.Core/Ax3/Dictionary.xml) です。

独自の表を使うには、`Dictionary.xml` をコピーして編集し、`--dictionary` で渡します。形式は
[`Dictionary.xsd`](src/HspDecompiler.Core/Ax3/Dictionary.xsd) で定義しており、XML スキーマに
対応したエディターなら入力中に検査できます。数値は桁数固定の大文字 16 進数(`0x0F`、`0x001E`)
です。デコンパイラーは読む前にファイルを検証し、最初の誤りの行番号を示して止まります。

## 開発

作業は [`just`](https://github.com/casey/just) で実行します。`just` でレシピの一覧が出ます。

```bash
just setup     # 固定したバージョンのツールを入れる
just test      # テストを実行する
just lint      # アナライザーを有効にし、警告をエラーとしてビルドする
just inspect   # ReSharper InspectCode の警告が 1 つでもあれば失敗する
just verify    # CI と同じ検査をすべて実行する
```

## 歴史

### 作者

| ソフト名 | 作者 | 説明 |
| --- | --- | --- |
| [HSP逆コンパイラ](http://www.vector.co.jp/soft/win95/prog/se390297.html) | [きつつき](http://www.vector.co.jp/vpack/browse/person/an043697.html) | オリジナルのソフト |
| [HSPdeco](https://osdn.jp/projects/hspdeco/) | [minorshift](https://osdn.jp/users/minorshift/) | オリジナルの改良版 |
| [HSPdecom](http://stpr18.blogspot.jp/2015/10/hspdecohspelona.html) | [したぷる](https://www.blogger.com/profile/00794326060600750840)、[YSRKEN](https://github.com/YSRKEN) | HSPdecoの改良版 |
| [HSPdecoのパッチ](http://vivibit.net/hspdeco/) | [xx2zz](http://vivibit.net/about/) | 復号失敗時の対策パッチ |

### バージョン履歴

| 日付 | ソフト名 | バージョン | 説明 |
| --- | --- | --- | --- |
| 2006/01/28 | HSP逆コンパイラ | 1.0 | 当初シェアウェアとして公開 |
| 2007/09/10 | HSP逆コンパイラ | 1.1 | HSP3に対応 |
| 2010/09/12 | HSP逆コンパイラ | 1.2 | PDS・OSSになった他、バグ修正 |
| 2012/01/13 | HSPdeco | 1.0 | axファイルのデコード機能を開放、バグ修正 |
| 2015/12/15 | HSPdecom | 1.0 | 変数名復元をサポート、辞書データ追加 |
| 2016/08/16 | HSPdecom | 1.1 | パッチを全て付加、GitHubに上げ直し |
| 2026-03-13 | deHSP | 2.0 | .NET 8.0 + Avalonia、CLIツール、クロスプラットフォーム対応 |
| 未リリース | deHSP | 3.0 | .NET 10 + Avalonia 12、同じ AX に戻る出力、`--utf8`。[CHANGELOG](CHANGELOG.md) を参照 |

## ライセンス

HSPdecom部分はPDSライセンス、HSPdeco部分はzlib/libpngライセンスです。
詳しくは[LICENSE](LICENSE)ファイルを参照してください。
