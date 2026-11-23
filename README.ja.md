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
  -o, --output <output>              出力ディレクトリ
  -d, --dictionary <dictionary>      独自の辞書 XML（デフォルト: 組み込みの HSP 3.8beta2 の表）
  --no-decrypt                       暗号化ファイルを復号しない
  --skip-encrypted                   暗号化されていないファイルだけを取り出す
  --utf8                             Shift_JIS ではなく BOM なしの UTF-8 で書く（hspcmp -i か -j で再コンパイル）
  --common <common>                  HSP の common ディレクトリ。デバッグビルドで一致する共通ヘッダーの #include を戻す
  --if-parens <None|Spaced|Tight>    if の条件の括弧: 無し、詰める '(x)'、空ける '( x )'（デフォルト: None）
  --one-line-if <Braces|Colon>       1 行の if ブロック（デバッグビルド）: コロン 'if x : y'、波括弧（デフォルト: Colon）
  --paren-negatives                  演算子の隣の負のリテラルを 'a - (-1)' と書く
  --logical <Doubled|Symbols|Words>  条件をつなぐ & と |: 記号、重ねる '&&'、単語 'and'（デフォルト: Symbols）
  --compact                          二項演算子を空白無しで書く: 'a+1'
  --no-macros                        hspdef.as のマクロを展開して書く: 'dir_exe' ではなく 'dirinfo(1)'
  --hex <All|None|Poke>              整数を 16 進数で書く: 無し、poke（128 以上の poke の値）、すべて（デフォルト: None）
  -v, --verbose                      詳細なログを出す
  -?, -h, --help                     ヘルプと使い方を表示
  --version                          バージョンを表示
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
| `#cmpopt optcode 0` | 呼ばれない `#func`・`#cfunc` か、何にも使われない `#module` がある（形式 3.1 以降）、または同じ文字列を 2 回格納している（文字列を共有する最初の 3.6 以降） |
| `#cmpopt varname 1` | 変数名を含み、デバッグビルドではない |
| `#cmpopt optprm 0` | 1 つのリテラルだけの引数の印が無い（形式 3.5 以降。hspcmp 3.1〜3.21 はこの行を拒否する） |
| `#cmpopt utf8 1` | UTF-8 の文字列を含む（utf8 起動フラグの無い hsp64 のプログラムを除く） |
| `#bootopt ...` | 起動フラグが立っている（hsp64、nogdip、float32、orgrnd、ioresume、utf8、notimer） |
| `#runtime "..."` | ランタイム名を持つ |

テスト用のフィクスチャは hspcmp 3.5、3.7、3.8β2 でバイト単位まで同じ AX に再コンパイルされます。

### デバッグビルド

デバッグモード（`hspcmp -d`）でコンパイルされたプログラムは、ファイル名、各文のソース行、
変数・ラベル・引数の名前を持っています。deHSP はそれらからソースを組み直します。

- コードを持つファイルは記録された名前でメインのソースの隣に書き出し、その `#include` は
  元の行に戻します。
- 各文は元のソース行に戻します。同じ行にあった文は ` : ` でつなぎ、`{"..."}` の文字列は
  改行を取り戻します。
- 変数・ラベル・引数は元の名前になります。
- `#module` と `#global` のブロックを、`#modinit`・`#modfunc`・`#modcfunc`・`#modterm`・
  `#deffunc local`・名前の無い `#module`・`thismod` とともに戻します。ブロックの中の名前は
  そのスコープに合わせて書きます。自身の変数は `val`、グローバル変数は `gcount@`、
  グローバルのラベルは `*init@` です。
- `--common` には HSP の `common` ディレクトリを指定します。`hspcmp.as` のように定義しか
  残さない共通ヘッダーは `#include` が戻り、出力からはそのヘッダーが宣言するインポートを
  除きます。`--common` が無ければその `#include` はコメントのままです。

出力を `hspcmp -d` で再コンパイルすると同じデバッグ情報になります。デバッグ用のフィクスチャと、
hspcmp 3.8β2 でビルドされた 41 KB のプログラムはバイト単位まで同じに戻ります。残る制限は
2 つです。

- AX はコメント、`#define` マクロ、名前の大文字・小文字、共通ヘッダーが宣言しない `#func`
  の別名を持たないため、これらは戻せません。
- 64 ビット版の hspcmp 3.8β2 は指定が無くても hsp64 と `#runtime "hsp3_64"` を設定しますが、
  32 ビット版にはこの 2 行が必要です。2 行はコードの無い最初の隙間に入れます。隙間の無い
  ソースでは最後の行の後に置くため、再コンパイルするとその分の行が記録されます。

### hspcmp 3.1〜3.32 の通常ビルド

hspcmp 3.1〜3.32 は `-d` が無くても行を記録します。各 `#module` と `#global` の行を 1 つずつ、
3.3 からは各 `{"..."}` 文字列が終わる行も記録します。記録にファイル名は無く、行番号は取り込み
ファイルごとに数え直します。deHSP は各記録をモジュールブロックか文字列に結び付け、それを軸に
ソースを組み直します。

- `#module` と `#global` のブロックを記録された行に戻します。通常ビルドはモジュール名を
  持たないため、ブロックの名前は `#modinit`・`#modfunc` のメンバーか `local` の関数があれば
  そこから取り、無ければ名前の無い `#module` として書きます。
- `{"..."}` の文字列は記録された行で終わります。
- それ以外の行は収まる限り 1 行ずつ保ちます。収まらなければ空行を落とし、さらに文を前の行に
  ` : ` でつなぎます。
- 行番号が数え直される所から先は `<名前>_1.hsp`・`<名前>_2.hsp` … に書き、それぞれを前の
  ファイルの最後で `#include` します。

同じ hspcmp で `-d` 無しで再コンパイルすると、記録も含めて同じ AX に戻ります。通常ビルドの
フィクスチャと、hspcmp 3.1・3.21・3.3・3.32 で確かめた 80 個の試験プログラムはそうなります。
hspcmp 3.4 以降はこの記録を書きません。記録に収まる配置が無ければ、記録を戻さずに行を順に
書きます。

### ソースの書き方

いくつかの構文は、どう書いても同じバイト列にコンパイルされます。書き方のオプションはその綴りを
選ぶもので、どれを選んでも同じ AX に戻ります。デフォルトは一般的な HSP の書き方に従います。

| オプション | デフォルト | 他の書き方 |
| --- | --- | --- |
| `--if-parens` | `if x == 1 {` | `tight`: `if (x == 1) {`、`spaced`: `if ( x == 1 ) {` |
| `--one-line-if` | `if x : mes "a"` | `braces`: `if x { mes "a" }` |
| `--paren-negatives` | `a - -1` | `a - (-1)` |
| `--logical` | `a == 1 & b == 2` | `doubled`: `(a == 1) && (b == 2)`、`words`: `(a == 1) and (b == 2)` |
| `--compact` | `a + 1` | `a+1` |
| `--no-macros` | `dir_exe`、`ginfo_mx` | `dirinfo(1)`、`ginfo(0)` |
| `--hex` | `poke b, 0, 239` | `poke`: 128 以上の `poke`・`wpoke`・`lpoke` の値を `poke b, 0, 0xEF` と書く、`all`: すべての整数 |

- `--one-line-if colon` はデバッグビルドに使われます。どのブロックが 1 行にあったかは行テーブルで
  分かるためです。複数行の `{"..."}` 文字列がその行を通る場合、本体に文以外がある場合、`else`
  が別の行で始まる場合、ブロックは波括弧のままです。
- `--no-macros` が無ければ、`hspdef.as` のマクロは AX をビルドしたコンパイラが持つ場合だけ
  書きます。hspcmp 3.0a〜3.4 には `ginfo_newid`・`dir_tv`・`ginfo_accelx` などのセンサーの
  マクロが無いリリースがあり、3.1〜3.4 は同じ AX バージョンを使うため、これらのマクロは
  AX バージョン 3.5 以降でだけ書きます。

それぞれの書き方は hspcmp 3.0a、3.1、3.2、3.21、3.3、3.31、3.32、3.4、3.5、3.51、3.6、3.7、
3.8β2 の通常ビルドとデバッグビルドで確かめ、どの組の書き方もすべてで同じバイト列になりました。
例外は、3.5 より前のコンパイラで上のマクロが定義されていないことだけです。

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

UTF-8 の文字列を持つプログラムの Shift_JIS 出力も再コンパイルできますが、hspcmp はその
文字列を Windows のコードページを通して UTF-8 に変換します。この方法で同じバイト列に戻せる
のは日本語のシステム（コードページ 932）だけで、それ以外のシステムでは `--utf8` を使って
ください。hspcmp 3.8β2 は hsp64 のプログラムには常に UTF-8 の文字列を格納するため、
`#cmpopt utf8 1` が無くてもその出力にはこれが当てはまります。

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
