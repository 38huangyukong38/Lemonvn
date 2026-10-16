using System.Collections.ObjectModel;
using CommunityToolkit.WinUI;
using GalgameManager.Enums;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;

namespace GalgameManager.Views.Control;

public sealed partial class SearchableTextBox : UserControl
{
    public static readonly DependencyProperty TextProperty =
        DependencyProperty.Register(nameof(Text), typeof(string), typeof(SearchableTextBox),
            new PropertyMetadata(""));

    public static readonly DependencyProperty SuggestionsProperty =
        DependencyProperty.Register(nameof(Suggestions), typeof(ObservableCollection<string>), typeof(SearchableTextBox),
            new PropertyMetadata(new ObservableCollection<string>()));

    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public ObservableCollection<string> Suggestions
    {
        get => (ObservableCollection<string>)GetValue(SuggestionsProperty);
        set => SetValue(SuggestionsProperty, value);
    }

    private string[] DefaultSuggestions => HotkeyResourceKeys.KnownKeys
        .Select(keySuffix => $"{HotkeyResourceKeys.Prefix}{keySuffix}")
        .Select(fullKey =>
        {
            try
            {
                var localizedValue = fullKey.GetLocalized();
                return !string.IsNullOrEmpty(localizedValue) && localizedValue != fullKey ? localizedValue : null;
            }
            catch
            {
                return null;
            }
        })
        .Where(value => value != null)
        .ToArray()!;

    
    public SearchableTextBox()
    {
        this.InitializeComponent();

        // 初始化建议列表（但不显示，等待用户点击或获得焦点）
        Suggestions = new ObservableCollection<string>();
    }

    private void ShowAllSuggestions()
    {
        // 清空并重新添加所有建议
        Suggestions.Clear();
        foreach (var suggestion in DefaultSuggestions)
        {
            Suggestions.Add(suggestion);
        }
    }

    private void AutoSuggestBox_OnTextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        if (args.Reason != AutoSuggestionBoxTextChangeReason.UserInput) return;

        // 如果用户开始输入，则进行过滤
        if (!string.IsNullOrEmpty(Text))
        {
            var filtered = DefaultSuggestions
                .Where(suggestion =>
                    suggestion.Contains(Text, System.StringComparison.OrdinalIgnoreCase) ||
                    Text.Contains(suggestion, System.StringComparison.OrdinalIgnoreCase))
                .OrderBy(suggestion => suggestion.StartsWith(Text, System.StringComparison.OrdinalIgnoreCase) ? 0 : 1)
                .ThenBy(suggestion => suggestion)
                .Take(10);

            Suggestions.Clear();
            foreach (var suggestion in filtered)
            {
                Suggestions.Add(suggestion);
            }
        }
        else
        {
            // 如果输入为空，显示所有建议
            ShowAllSuggestions();
        }
    }

    private void AutoSuggestBox_OnQuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
    {
        // 确认输入（无论用户选择建议项还是输入的内容）
        ConfirmInputAndClearSuggestions();
    }

    private void MainAutoSuggestBox_GotFocus(object sender, RoutedEventArgs e)
    {
        // 当获得焦点时，显示所有建议
        ShowAllSuggestions();
    }

    private void MainAutoSuggestBox_LostFocus(object sender, RoutedEventArgs e)
    {
        // 当失去焦点时（用户点击外部），确认输入并清除建议列表
        ConfirmInputAndClearSuggestions();
    }

    private void MainAutoSuggestBox_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        // 处理 Enter 键
        if (e.Key == Windows.System.VirtualKey.Enter)
        {
            e.Handled = true;
            ConfirmInputAndClearSuggestions();
        }
    }

    private void ConfirmInputAndClearSuggestions()
    {
        // 清除建议列表
        Suggestions.Clear();
    }
}