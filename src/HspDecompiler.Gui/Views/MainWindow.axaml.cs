using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using HspDecompiler.Gui.Resources;
using HspDecompiler.Gui.ViewModels;

namespace HspDecompiler.Gui.Views;

/// <summary>
/// <para xml:lang="en">Main window: open or drop a file to decompile it.</para>
/// <para xml:lang="ja">メインウィンドウ。ファイルを開くかドロップするとデコンパイルする。</para>
/// </summary>
public partial class MainWindow : Window
{
    private static readonly string[] s_hspFilePatterns = ["*.ax", "*.exe", "*.dpm"];
    private static readonly string[] s_allFilePatterns = ["*"];

    /// <summary>
    /// <para xml:lang="en">Creates the window and accepts dropped files.</para>
    /// <para xml:lang="ja">ウィンドウを作り、ファイルのドロップを受け付ける。</para>
    /// </summary>
    public MainWindow()
    {
        InitializeComponent();
        AddHandler(DragDrop.DropEvent, OnDrop);
        AddHandler(DragDrop.DragOverEvent, OnDragOver);
    }

    /// <inheritdoc/>
    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);

        Title = Strings.WindowTitle;
        SetUpMenu();
        LocalizeFileGrid();
    }

    private void SetUpMenu()
    {
        MenuItem? menuOpen = this.FindControl<MenuItem>("MenuOpen");
        MenuItem? menuExit = this.FindControl<MenuItem>("MenuExit");
        MenuItem? menuAbout = this.FindControl<MenuItem>("MenuAbout");
        menuOpen?.Click += OnMenuOpen;
        menuExit?.Click += (_, _) => Close();
        menuAbout?.Click += OnMenuAbout;

        this.FindControl<MenuItem>("MenuFile")?.Header = Strings.MenuFile;
        this.FindControl<MenuItem>("MenuHelp")?.Header = Strings.MenuHelp;
        menuOpen?.Header = Strings.MenuOpen;
        menuExit?.Header = Strings.MenuExit;
        menuAbout?.Header = Strings.MenuAbout;
    }

    private void LocalizeFileGrid()
    {
        DataGrid? dataGrid = this.FindControl<DataGrid>("FileGrid");
        if (dataGrid is not { Columns.Count: >= 4 })
        {
            return;
        }

        dataGrid.Columns[0].Header = Strings.ColumnFilename;
        dataGrid.Columns[1].Header = Strings.ColumnEncrypted;
        dataGrid.Columns[2].Header = Strings.ColumnOffset;
        dataGrid.Columns[3].Header = Strings.ColumnFileSize;
    }

    // Avalonia raises these handlers as async void, so an exception escaping one would end the
    // process. Each body runs through HandleFileAsync, which logs the failure instead.
    // ---
    // Avalonia はこれらのハンドラーを async void として呼ぶため、例外が漏れるとプロセスが終了する。
    // 各処理は HandleFileAsync を通し、失敗はログへ書く。
    private async void OnMenuOpen(object? sender, RoutedEventArgs e) =>
        await HandleFileAsync(PickFileAsync).ConfigureAwait(true);

    private async void OnDrop(object? sender, DragEventArgs e) =>
        await HandleFileAsync(() => Task.FromResult(e.DataTransfer.TryGetFile()))
            .ConfigureAwait(true);

    private async void OnMenuAbout(object? sender, RoutedEventArgs e)
    {
        try
        {
            await new AboutDialog().ShowDialog(this).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            (DataContext as MainWindowViewModel)?.ReportError(ex);
        }
    }

    private async Task HandleFileAsync(Func<Task<IStorageItem?>> selectFile)
    {
        if (DataContext is not MainWindowViewModel vm)
        {
            return;
        }

        try
        {
            IStorageItem? file = await selectFile().ConfigureAwait(true);
            string? path = file?.TryGetLocalPath();
            if (path != null)
            {
                await vm.ProcessFileAsync(path).ConfigureAwait(true);
            }
        }
        catch (Exception ex)
        {
            vm.ReportError(ex);
        }
    }

    private async Task<IStorageItem?> PickFileAsync()
    {
        IReadOnlyList<IStorageFile> files = await StorageProvider
            .OpenFilePickerAsync(
                new FilePickerOpenOptions
                {
                    Title = Strings.OpenFileTitle,
                    AllowMultiple = false,
                    FileTypeFilter =
                    [
                        new FilePickerFileType(Strings.HspFilesFilter)
                        {
                            Patterns = s_hspFilePatterns,
                        },
                        new FilePickerFileType(Strings.AllFilesFilter)
                        {
                            Patterns = s_allFilePatterns,
                        },
                    ],
                }
            )
            .ConfigureAwait(true);
        return files.Count > 0 ? files[0] : null;
    }

    private void OnDragOver(object? sender, DragEventArgs e) =>
        e.DragEffects = e.DataTransfer.Contains(DataFormat.File)
            ? DragDropEffects.Copy
            : DragDropEffects.None;
}
