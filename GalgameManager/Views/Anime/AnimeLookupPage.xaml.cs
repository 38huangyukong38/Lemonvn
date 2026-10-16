using Windows.System;
using GalgameManager;
using LemonVN.ViewModels.Anime;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;

namespace LemonVN.Views.Anime;

public sealed partial class AnimeLookupPage : Page
{
    private bool _isKeywordComposing;
    public AnimeLookupViewModel ViewModel { get; } = App.GetService<AnimeLookupViewModel>();
    public AnimeLookupPage() => InitializeComponent();
    private void OnResultClicked(object sender, ItemClickEventArgs args) => ViewModel.PreviewCommand.Execute(args.ClickedItem);

    private void OnKeywordCompositionStarted(TextBox sender, TextCompositionStartedEventArgs args) => _isKeywordComposing = true;
    private void OnKeywordCompositionEnded(TextBox sender, TextCompositionEndedEventArgs args) => _isKeywordComposing = false;

    private void OnKeywordKeyDown(object sender, KeyRoutedEventArgs args)
    {
        // Let the IME confirm its candidate; only a separate Enter submits the query.
        if (args.Key != VirtualKey.Enter || _isKeywordComposing || args.KeyStatus.WasKeyDown ||
            !ViewModel.ShowSearch || !ViewModel.CanInteract || !ViewModel.SearchCommand.CanExecute(null)) return;
        args.Handled = true;
        ViewModel.SearchCommand.Execute(null);
    }
}
