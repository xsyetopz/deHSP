using System.Linq;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using HspDecompiler.Gui.Resources;
using HspDecompiler.Gui.ViewModels;

namespace HspDecompiler.Gui.Views;

public partial class MainWindow : Window
{
    private static readonly string[] s_hspFilePatterns = ["*.ax", "*.exe", "*.dpm"];
    private static readonly string[] s_allFilePatterns = ["*"];

    public MainWindow()
    {
        InitializeComponent();
        AddHandler(DragDrop.DropEvent, OnDrop);
        AddHandler(DragDrop.DragOverEvent, OnDragOver);
    }

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

    private async void OnMenuOpen(object? sender, RoutedEventArgs e)
    {
        System.Collections.Generic.IReadOnlyList<IStorageFile> files = await StorageProvider
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
        if (files.Count > 0)
        {
            string? path = files[0].TryGetLocalPath();
            if (path != null && DataContext is MainWindowViewModel vm)
            {
                await vm.ProcessFileAsync(path).ConfigureAwait(true);
            }
        }
    }

    private void OnMenuAbout(object? sender, RoutedEventArgs e)
    {
        var about = new AboutDialog();
        about.ShowDialog(this);
    }

    private void OnDragOver(object? sender, DragEventArgs e) =>
        e.DragEffects = e.DataTransfer.Contains(DataFormat.File)
            ? DragDropEffects.Copy
            : DragDropEffects.None;

    private async void OnDrop(object? sender, DragEventArgs e)
    {
        IStorageItem? file = e.DataTransfer.TryGetFile();
        if (file == null)
        {
            return;
        }

        string? path = file.TryGetLocalPath();
        if (path != null && DataContext is MainWindowViewModel vm)
        {
            await vm.ProcessFileAsync(path).ConfigureAwait(true);
        }
    }
}
