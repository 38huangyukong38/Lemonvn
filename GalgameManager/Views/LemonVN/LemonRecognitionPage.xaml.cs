using GalgameManager;
using LemonVN.Helpers;
using LemonVN.ViewModels;
using Microsoft.UI.Xaml.Controls;

namespace LemonVN.Views;

public sealed partial class LemonRecognitionPage : Page
{
    public LemonRecognitionViewModel ViewModel { get; }
    public string RecognitionTitle { get; } = LemonLocalization.GetString("Ux_RecognitionTitle");
    public LemonRecognitionPage()
    {
        ViewModel = App.GetService<LemonRecognitionViewModel>();
        InitializeComponent();
    }
    private void OnViewportSizeChanged(object sender, Microsoft.UI.Xaml.SizeChangedEventArgs args) =>
        RecognitionContent.Width = Math.Min(args.NewSize.Width, RecognitionContent.MaxWidth);
    private void OnCandidateClicked(object sender, ItemClickEventArgs args) => ViewModel.SelectCandidateCommand.Execute(args.ClickedItem);
    private void OnChooseClicked(object sender, Microsoft.UI.Xaml.RoutedEventArgs args) =>
        ViewModel.SelectCandidateCommand.Execute(((Button)sender).DataContext);
    private void OnPreviewClicked(object sender, Microsoft.UI.Xaml.RoutedEventArgs args) =>
        ViewModel.PreviewCommand.Execute(((Button)sender).DataContext);
    private void OnLocalClicked(object sender, ItemClickEventArgs args) => ViewModel.OpenLocalCommand.Execute(args.ClickedItem);
    private void OnInputDecodeFailed(object? sender, EventArgs args) => ViewModel.RejectInputPreview();
    private void OnMatchDecodeFailed(object? sender, EventArgs args) => ViewModel.RejectMatchPreview();
}
