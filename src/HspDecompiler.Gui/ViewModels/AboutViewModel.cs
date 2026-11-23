using System.Reflection;
using HspDecompiler.Gui.Resources;

namespace HspDecompiler.Gui.ViewModels;

/// <summary>
/// <para xml:lang="en">Data shown in the About dialog.</para>
/// <para xml:lang="ja">バージョン情報ダイアログに表示するデータ。</para>
/// </summary>
public class AboutViewModel : ViewModelBase
{
    private static readonly Assembly s_assembly = typeof(AboutViewModel).Assembly;

    /// <summary>
    /// <para xml:lang="en">Release version of the application, such as 3.0.0, without build metadata.</para>
    /// <para xml:lang="ja">アプリケーションのリリースバージョン (例: 3.0.0)。ビルドメタデータは含まない。</para>
    /// </summary>
    public static string Version =>
        s_assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion.Split('+')[0]
        ?? s_assembly.GetName().Version?.ToString(3)
        ?? "";

    /// <summary>
    /// <para xml:lang="en">Localized application title.</para>
    /// <para xml:lang="ja">ローカライズされたアプリケーション名。</para>
    /// </summary>
    public static string Title => Strings.WindowTitle;

    /// <summary>
    /// <para xml:lang="en">Copyright line, crediting Kitsutsuki as the original author.</para>
    /// <para xml:lang="ja">著作権表示。原作者として Kitsutsuki 氏を記載する。</para>
    /// </summary>
    public static string Copyright =>
        s_assembly.GetCustomAttribute<AssemblyCopyrightAttribute>()?.Copyright ?? "";
}
