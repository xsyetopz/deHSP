using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using HspDecompiler.Gui.ViewModels;
using HspDecompiler.Gui.Views;

namespace HspDecompiler.Gui;

/// <summary>
/// <para xml:lang="en">The deHSP desktop application; it opens the main window on desktop platforms.</para>
/// <para xml:lang="ja">deHSP のデスクトップアプリケーション。デスクトップ環境ではメインウィンドウを開く。</para>
/// </summary>
public class App : Application
{
    /// <inheritdoc/>
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    /// <inheritdoc/>
    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow { DataContext = new MainWindowViewModel() };
        }
        base.OnFrameworkInitializationCompleted();
    }
}
