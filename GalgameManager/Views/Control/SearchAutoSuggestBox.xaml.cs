using System.Collections.ObjectModel;
using System.Windows.Input;
using DependencyPropertyGenerator;
using GalgameManager.Helpers;
using Microsoft.UI.Xaml.Controls;

namespace GalgameManager.Views.Control
{
    [DependencyProperty<string>("SearchKey")]
    [DependencyProperty<ICommand>("SearchCommand")]
    [DependencyProperty<ICommand>("SearchSubmitCommand")]
    [DependencyProperty<ISearchSuggestionsProvider>("SearchSuggestionsProvider")]
    [DependencyProperty<string>("PlaceholderText")]
    public sealed partial class SearchAutoSuggestBox: UserControl
    {
        public SearchAutoSuggestBox()
        {
            InitializeComponent();
            SuggestBox.MinWidth = MinWidth;
            MinWidth = 250; //兼容老代码写死在XAML里的MinWidth
        }

        private const int SearchDelay = 500;

        public readonly ObservableCollection<string> SearchSuggestions = new();
        private DateTime _lastSearchTime = DateTime.Now;
        private async void AutoSuggestBox_OnTextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
        {
            if (string.IsNullOrEmpty(SearchKey))
            {
                SearchCommand?.Execute(SearchKey);
                SearchSuggestions.Clear();
                return;
            }

            _ = Task.Run((async Task() =>
            {
                _lastSearchTime = DateTime.Now;
                DateTime tmp = _lastSearchTime;
                await Task.Delay(SearchDelay);
                if (tmp == _lastSearchTime) //如果在延迟时间内没有再次输入，则开始搜索
                {
                    await UiThreadInvokeHelper.InvokeAsync(() =>
                    {
                        SearchCommand?.Execute(SearchKey);
                    });
                }
            })!);
            //更新建议
            if(args.Reason != AutoSuggestionBoxTextChangeReason.UserInput) return;
            SearchSuggestions.Clear();

            if (SearchKey == string.Empty)return;

            if (SearchSuggestionsProvider != null &&
                await SearchSuggestionsProvider.GetSearchSuggestionsAsync(SearchKey) is {} result)
            {
                foreach (var suggestion in result)
                    SearchSuggestions.Add(suggestion);
            }
        }

        private void AutoSuggestBox_OnQuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
        {
            // 更新 SearchKey 为用户提交的查询或选择的建议
            if (args.ChosenSuggestion != null)
            {
                // 如果是从建议中选择的项目
                SearchKey = args.ChosenSuggestion.ToString();
            }
            else
            {
                // 如果是直接提交的查询文本
                SearchKey = args.QueryText;
            }

            if (string.IsNullOrEmpty(SearchKey)) return;

            // 执行搜索命令
            SearchCommand?.Execute(SearchKey);
            SearchSubmitCommand?.Execute(SearchKey);
        }
    }
}

public interface ISearchSuggestionsProvider
{
    public Task<IEnumerable<string>?> GetSearchSuggestionsAsync(string key);
}
