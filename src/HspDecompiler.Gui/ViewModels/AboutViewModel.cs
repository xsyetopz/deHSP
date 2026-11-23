using System.Reflection;
using HspDecompiler.Gui.Resources;

namespace HspDecompiler.Gui.ViewModels;

/// <summary>
/// <para xml:lang="en">Data shown in the About dialog.</para>
/// <para xml:lang="ja">バージョン情報ダイアログに表示するデータ。</para>
/// </summary>
public class AboutViewModel : ViewModelBase
{
    /// <summary>
    /// <para xml:lang="en">Assembly version of the application.</para>
    /// <para xml:lang="ja">アプリケーションのアセンブリバージョン。</para>
    /// </summary>
    public static string Version =>
        Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "1.0.0";

    /// <summary>
    /// <para xml:lang="en">Localized application title.</para>
    /// <para xml:lang="ja">ローカライズされたアプリケーション名。</para>
    /// </summary>
    public static string Title => Strings.WindowTitle;

    /// <summary>
    /// <para xml:lang="en">Copyright line.</para>
    /// <para xml:lang="ja">著作権表示。</para>
    /// </summary>
    public static string Copyright => "Kitsutsuki (Original) / HSP Decompiler Contributors";
}
