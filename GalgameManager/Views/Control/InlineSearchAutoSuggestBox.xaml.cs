using System.Collections.ObjectModel;
using System.Windows.Input;
using DependencyPropertyGenerator;
using GalgameManager.Helpers;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;

namespace GalgameManager.Views.Control;

[DependencyProperty<string>("SearchKey")]
[DependencyProperty<ICommand>("SearchCommand")]
[DependencyProperty<ICommand>("SearchSubmitCommand")]
[DependencyProperty<ISearchSuggestionsProvider>("SearchSuggestionsProvider")]
[DependencyProperty<string>("PlaceholderText")]
[DependencyProperty<double>("ExpandedWidth", DefaultValue = 250d)]
public sealed partial class InlineSearchAutoSuggestBox : UserControl
{
    private const int SearchDelay = 500;
    private readonly ObservableCollection<string> _searchSuggestions = new();
    private bool _isExpanded;
    private Button? _clearButton;
    private CancellationTokenSource? _searchCts;
    private bool _isInitialized;

    // Animation caching
    private Storyboard? _widthStoryboard;
    private DoubleAnimation? _widthAnimation;

    public InlineSearchAutoSuggestBox()
    {
        InitializeComponent();
        // Use a single event handler for setup to reduce closure allocations if possible, 
        // but lambda is fine here for capturing 'this'.
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    public ObservableCollection<string> SearchSuggestions => _searchSuggestions;

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_isInitialized) return;
        _isInitialized = true;
        SearchLabelText.Text = "Search".GetLocalized();
        ToggleState(false);
        SearchBox.Width = 0;
        AttachClearButton();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        DetachClearButton();
        _searchCts?.Cancel();
        _searchCts?.Dispose();
        _searchCts = null;
    }

    private void SearchButton_Click(object sender, RoutedEventArgs e) => Expand();

    private void Expand()
    {
        if (_isExpanded)
        {
            SearchBox.Focus(FocusState.Programmatic);
            return;
        }

        _isExpanded = true;
        ToggleState(true);
        AnimateWidth(ExpandedWidth);
        SearchBox.Focus(FocusState.Programmatic);
        AttachClearButton();
    }

    private void OnCtrlF_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        Expand();
        args.Handled = true;
    }

    private void Collapse()
    {
        if (!_isExpanded) return;
        _isExpanded = false;

        SearchBox.IsSuggestionListOpen = false;
        ToggleState(false);
        AnimateWidth(0);
    }

    private void ToggleState(bool isExpanded)
    {
        // Batch property updates if needed, but here simple assignment is efficient enough.
        SearchButton.IsHitTestVisible = !isExpanded;
        SearchBox.IsHitTestVisible = isExpanded;

        // Use Visibility for the button to remove it from hit testing fully when hidden,
        // but Opacity animation handles the visual transition.
        // Keeping it simple with Opacity/IsHitTestVisible as per original design.
        SearchButton.Opacity = isExpanded ? 0 : 1;
        SearchBox.Opacity = isExpanded ? 1 : 0;
    }

    private void SearchBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(SearchKey))
            Collapse();
    }

    private void SearchBox_OnLoaded(object sender, RoutedEventArgs e) => AttachClearButton();

    private void AttachClearButton()
    {
        if (_clearButton != null)
        {
            EnsureClearButtonVisible();
            return;
        }

        // Optimized iterative search
        _clearButton = FindDescendant<Button>(SearchBox, "DeleteButton")
                       ?? FindDescendant<Button>(SearchBox, "ClearButton");

        if (_clearButton != null)
        {
            _clearButton.Click += ClearButton_Click;
            EnsureClearButtonVisible();
        }
    }

    private void DetachClearButton()
    {
        if (_clearButton != null)
        {
            _clearButton.Click -= ClearButton_Click;
            _clearButton = null;
        }
    }

    private void ClearButton_Click(object sender, RoutedEventArgs e)
    {
        if (!_isExpanded) return;

        if (!string.IsNullOrEmpty(SearchKey))
        {
            SearchKey = string.Empty;
            SearchBox.Focus(FocusState.Programmatic);
            EnsureClearButtonVisible();
            return;
        }

        Collapse();
    }

    private void EnsureClearButtonVisible()
    {
        if (_clearButton == null || _clearButton.Visibility == Visibility.Visible) return;
        _clearButton.Visibility = Visibility.Visible;
        _clearButton.Opacity = 1;
        _clearButton.IsHitTestVisible = true;
    }

    private void AnimateWidth(double toWidth)
    {
        if (_widthStoryboard == null)
        {
            _widthAnimation = new DoubleAnimation
            {
                Duration = new Duration(TimeSpan.FromSeconds(0.2)),
                EnableDependentAnimation = true,
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            Storyboard.SetTarget(_widthAnimation, SearchBox);
            Storyboard.SetTargetProperty(_widthAnimation, "Width");
            _widthStoryboard = new Storyboard();
            _widthStoryboard.Children.Add(_widthAnimation);
        }

        if (_widthAnimation != null)
        {
            _widthAnimation.From = SearchBox.Width;
            _widthAnimation.To = toWidth;
        }
        _widthStoryboard!.Begin();
    }

    // Optimized iterative implementation to avoid recursion stack overhead
    private static T? FindDescendant<T>(DependencyObject root, string name) where T : FrameworkElement
    {
        if (root == null) return null;

        var queue = new Queue<DependencyObject>();
        queue.Enqueue(root);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            var count = VisualTreeHelper.GetChildrenCount(current);

            for (var i = 0; i < count; i++)
            {
                var child = VisualTreeHelper.GetChild(current, i);
                if (child is T element && element.Name == name)
                    return element;
                queue.Enqueue(child);
            }
        }
        return null;
    }

    private async void AutoSuggestBox_OnTextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        // Avoid closure allocation for the DispatcherQueue call if possible, 
        // but minimal impact here. We check if update is needed inside EnsureClearButtonVisible.
        DispatcherQueue.TryEnqueue(EnsureClearButtonVisible);

        if (string.IsNullOrEmpty(SearchKey))
        {
            _searchCts?.Cancel();
            SearchCommand?.Execute(string.Empty);
            SearchSuggestions.Clear();
            return;
        }

        _searchCts?.Cancel();
        _searchCts = new CancellationTokenSource();
        var token = _searchCts.Token;

        // Fire-and-forget debounce
        _ = DebounceSearchCommand(token);

        if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput && SearchSuggestionsProvider != null)
        {
            // Use ConfigureAwait(true) to ensure we stay on UI thread for collection modification 
            // (default is true, but explicit is clear). 
            // Actually, we must be on UI thread to modify ObservableCollection bound to UI.
            var result = await SearchSuggestionsProvider.GetSearchSuggestionsAsync(SearchKey);

            if (!token.IsCancellationRequested)
            {
                SearchSuggestions.Clear();
                if (result != null)
                {
                    foreach (var suggestion in result)
                        SearchSuggestions.Add(suggestion);
                }
            }
        }
    }

    private async Task DebounceSearchCommand(CancellationToken token)
    {
        try
        {
            await Task.Delay(SearchDelay, token);
            if (!token.IsCancellationRequested)
            {
                SearchCommand?.Execute(SearchKey);
            }
        }
        catch (TaskCanceledException) { /* Ignore */ }
    }

    private void AutoSuggestBox_OnQuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
    {
        _searchCts?.Cancel();
        SearchKey = args.ChosenSuggestion?.ToString() ?? args.QueryText;

        if (string.IsNullOrEmpty(SearchKey)) return;

        SearchCommand?.Execute(SearchKey);
        SearchSubmitCommand?.Execute(SearchKey);
    }
}
