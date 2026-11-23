using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using HspDecompiler.Core.DpmToAx;
using HspDecompiler.Core.Pipeline;
using HspDecompiler.Gui.Resources;
using HspDecompiler.Gui.Services;

namespace HspDecompiler.Gui.ViewModels;

/// <summary>
/// <para xml:lang="en">State of the main window: the log, the busy flag and the entries of the opened archive.</para>
/// <para xml:lang="ja">メインウィンドウの状態。ログ、処理中フラグ、開いたアーカイブのエントリを持つ。</para>
/// </summary>
public partial class MainWindowViewModel : ViewModelBase
{
    private static readonly CompositeFormat s_errorFormat = CompositeFormat.Parse(
        Strings.ErrorFormat
    );

    /// <summary>
    /// <para xml:lang="en">Log of the current run.</para>
    /// <para xml:lang="ja">現在の処理のログ。</para>
    /// </summary>
    [ObservableProperty]
    private string _logText = "";

    /// <summary>
    /// <para xml:lang="en">Whether a file is being decompiled.</para>
    /// <para xml:lang="ja">ファイルをデコンパイル中かどうか。</para>
    /// </summary>
    [ObservableProperty]
    private bool _isProcessing;

    /// <summary>
    /// <para xml:lang="en">Entries of the archive the last run opened; empty for a plain .ax file.</para>
    /// <para xml:lang="ja">直前の処理で開いたアーカイブのエントリ。.ax ファイルの場合は空。</para>
    /// </summary>
    public ObservableCollection<DpmFileEntry> DpmFiles { get; } = [];

    /// <summary>
    /// <para xml:lang="en">How the next run spells the source; the Style menu edits it.</para>
    /// <para xml:lang="ja">次の処理でソースをどう綴るか。スタイルメニューで変更する。</para>
    /// </summary>
    public SourceStyle Style { get; set; } = new();

    /// <summary>
    /// <para xml:lang="en">
    /// Decompiles a file beside itself, with decryption allowed, and shows the log and archive
    /// entries. Errors go to the log instead of being thrown. A missing file is ignored.
    /// </para>
    /// <para xml:lang="ja">
    /// 復号を許可してファイルをその隣へデコンパイルし、ログとアーカイブのエントリを表示する。
    /// エラーは例外にせずログへ書く。存在しないファイルは無視する。
    /// </para>
    /// </summary>
    /// <param name="filePath">
    /// <para xml:lang="en">Path of the .ax, .exe or .dpm file.</para>
    /// <para xml:lang="ja">.ax・.exe・.dpm ファイルのパス。</para>
    /// </param>
    /// <returns>
    /// <para xml:lang="en">A task that completes when the run ends.</para>
    /// <para xml:lang="ja">処理が終わると完了するタスク。</para>
    /// </returns>
    public async Task ProcessFileAsync(string filePath)
    {
        if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
        {
            return;
        }

        IsProcessing = true;
        LogText = "";
        DpmFiles.Clear();

        try
        {
            var logger = new GuiLogger(this);
            var progress = new GuiProgressReporter();
            var pipeline = new DecompilerPipeline(logger, progress);

            var options = new DecompilerOptions
            {
                InputPath = filePath,
                AllowDecryption = true,
                SkipEncrypted = false,
                Style = Style,
            };

            pipeline.Initialize();
            // The continuation edits bound collections, which Avalonia allows only on the UI thread.
            //
            // 継続処理はバインドされたコレクションを書き換える。Avalonia では UI スレッドでのみ許される。
            DecompilerResult result = await Task.Run(() =>
                    pipeline.RunAsync(options, CancellationToken.None)
                )
                .ConfigureAwait(true);

            foreach (DpmFileEntry file in result.DpmFiles)
            {
                DpmFiles.Add(file);
            }

            if (!result.Success)
            {
                AppendLog(
                    string.Format(CultureInfo.InvariantCulture, s_errorFormat, result.ErrorMessage)
                );
            }
        }
        catch (Exception ex)
        {
            ReportError(ex);
        }
        finally
        {
            IsProcessing = false;
        }
    }

    internal void ReportError(Exception ex) =>
        AppendLog(string.Format(CultureInfo.InvariantCulture, s_errorFormat, ex.Message));

    internal void AppendLog(string line) => LogText += line + Environment.NewLine;
}
