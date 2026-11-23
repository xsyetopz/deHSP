using System;
using Avalonia.Controls;
using HspDecompiler.Core.Pipeline;
using HspDecompiler.Gui.Resources;
using HspDecompiler.Gui.ViewModels;

namespace HspDecompiler.Gui.Views;

// Fills the Style menu with one entry per SourceStyle setting, the same settings the CLI takes
// as --if-parens, --one-line-if and so on. A choice of values is a submenu of radio items named
// after the CLI values; a yes-or-no setting is a check item.
//
// スタイルメニューに SourceStyle の設定ごとの項目を入れる。CLI が --if-parens・--one-line-if
// などで受け取る設定と同じである。値を選ぶ設定は CLI の値の名前を持つラジオ項目のサブメニュー、
// はい・いいえの設定はチェック項目にする。
internal static class StyleMenu
{
    internal static void Fill(MenuItem menu, MainWindowViewModel vm)
    {
        menu.Header = Strings.MenuStyle;
        menu.Items.Add(
            Choice(
                vm,
                Strings.StyleIfParens,
                s => s.IfParentheses,
                (s, v) => s with { IfParentheses = v }
            )
        );
        menu.Items.Add(
            Choice(vm, Strings.StyleOneLineIf, s => s.OneLineIf, (s, v) => s with { OneLineIf = v })
        );
        menu.Items.Add(
            Choice(
                vm,
                Strings.StyleLogical,
                s => s.LogicalOperators,
                (s, v) => s with { LogicalOperators = v }
            )
        );
        menu.Items.Add(
            Choice(vm, Strings.StyleHex, s => s.HexLiterals, (s, v) => s with { HexLiterals = v })
        );
        menu.Items.Add(new Separator());
        menu.Items.Add(
            Toggle(
                vm,
                Strings.StyleParenNegatives,
                s => s.ParenthesizeNegatives,
                (s, v) => s with { ParenthesizeNegatives = v }
            )
        );
        menu.Items.Add(
            Toggle(
                vm,
                Strings.StyleCompact,
                s => s.CompactOperators,
                (s, v) => s with { CompactOperators = v }
            )
        );
        menu.Items.Add(
            Toggle(
                vm,
                Strings.StyleMacros,
                s => s.StandardMacros,
                (s, v) => s with { StandardMacros = v }
            )
        );
    }

    private static MenuItem Choice<T>(
        MainWindowViewModel vm,
        string header,
        Func<SourceStyle, T> get,
        Func<SourceStyle, T, SourceStyle> set
    )
        where T : struct, Enum
    {
        var submenu = new MenuItem { Header = header };
        foreach (T value in Enum.GetValues<T>())
        {
            var item = new MenuItem
            {
                Header = value.ToString().ToLowerInvariant(),
                ToggleType = MenuItemToggleType.Radio,
                GroupName = header,
                IsChecked = get(vm.Style).Equals(value),
            };
            item.Click += (_, _) =>
            {
                vm.Style = set(vm.Style, value);
                item.IsChecked = true;
            };
            submenu.Items.Add(item);
        }

        return submenu;
    }

    private static MenuItem Toggle(
        MainWindowViewModel vm,
        string header,
        Func<SourceStyle, bool> get,
        Func<SourceStyle, bool, SourceStyle> set
    )
    {
        var item = new MenuItem
        {
            Header = header,
            ToggleType = MenuItemToggleType.CheckBox,
            IsChecked = get(vm.Style),
        };
        item.Click += (_, _) =>
        {
            bool value = !get(vm.Style);
            vm.Style = set(vm.Style, value);
            item.IsChecked = value;
        };
        return item;
    }
}
