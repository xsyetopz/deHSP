using Avalonia.Controls;
using Avalonia.Interactivity;
using HspDecompiler.Gui.Resources;
using HspDecompiler.Gui.ViewModels;

namespace HspDecompiler.Gui.Views;

/// <summary>
/// <para xml:lang="en">Dialog that shows the application version and copyright.</para>
/// <para xml:lang="ja">アプリケーションのバージョンと著作権を表示するダイアログ。</para>
/// </summary>
public partial class AboutDialog : Window
{
    /// <summary>
    /// <para xml:lang="en">Creates the dialog with its localized title.</para>
    /// <para xml:lang="ja">ローカライズされたタイトルでダイアログを作る。</para>
    /// </summary>
    public AboutDialog()
    {
        InitializeComponent();
        DataContext = new AboutViewModel();
        Title = Strings.AboutTitle;
    }

    private void OnOkClick(object sender, RoutedEventArgs e) => Close();
}
