using LemonVN.Helpers;
using LemonVN.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace LemonVN.Views;

/// <summary>View-only gate. No database, network provider or account service is resolved here.</summary>
public sealed partial class LicenseConsentPage : Page
{
    private readonly LicenseTerms _terms;
    private readonly TaskCompletionSource<bool> _decision = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public Task<bool> Decision => _decision.Task;
    public string VersionLabel => LicenseTerms.SdkIdentity + " · " + LicenseTerms.AppSdkIdentity + " · " + _terms.ContentHash[..12];

    public LicenseConsentPage(LicenseTerms terms)
    {
        _terms = terms;
        InitializeComponent();
        Loaded += (_, _) => Decline.Focus(FocusState.Programmatic);
    }

    public void Cancel() => _decision.TrySetResult(false);

    private void Agree_Click(object sender, RoutedEventArgs e)
    {
        if (_decision.TrySetResult(true)) Agree.IsEnabled = Decline.IsEnabled = false;
    }

    private void Decline_Click(object sender, RoutedEventArgs e) => Cancel();

    private async void ReadFull_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var sdk = new RichEditBox { IsReadOnly = true, TextWrapping = TextWrapping.Wrap, AcceptsReturn = true };
            using (Stream original = _terms.OpenSdkOriginal())
            using (var stream = original.AsRandomAccessStream())
                sdk.Document.LoadFromStream(Microsoft.UI.Text.TextSetOptions.FormatRtf, stream);
            var tabs = new TabView
            {
                IsAddTabButtonVisible = false, CanDragTabs = false, CanReorderTabs = false,
                Width = Math.Max(420, Math.Min(720, XamlRoot.Size.Width - 100)),
                Height = Math.Max(200, Math.Min(420, XamlRoot.Size.Height - 200))
            };
            tabs.TabItems.Add(new TabViewItem { Header = LemonLocalization.GetString("LicenseConsent_SdkTab"), IsClosable = false, Content = sdk });
            tabs.TabItems.Add(new TabViewItem { Header = "Windows App SDK", IsClosable = false, Content = ReadOnlyText(_terms.AppSdkText) });
            tabs.TabItems.Add(new TabViewItem { Header = "Windows ML", IsClosable = false, Content = ReadOnlyText(_terms.WindowsMlText) });
            tabs.TabItems.Add(new TabViewItem { Header = "C#/WinRT · MIT", IsClosable = false, Content = ReadOnlyText(_terms.WinRtMitText) });
            tabs.TabItems.Add(new TabViewItem { Header = LemonLocalization.GetString("LicenseConsent_DraftTab"), IsClosable = false, Content = ReadOnlyText(_terms.EndUserText) });
            tabs.TabItems.Add(new TabViewItem { Header = "Apache-2.0", IsClosable = false, Content = ReadOnlyText(_terms.ApacheText) });
            var dialog = new ContentDialog
            {
                XamlRoot = XamlRoot, RequestedTheme = RequestedTheme,
                Title = LemonLocalization.GetString("LicenseConsent_FullTitle"), Content = tabs,
                CloseButtonText = LemonLocalization.GetString("LicenseConsent_Close"), DefaultButton = ContentDialogButton.Close
            };
            dialog.Resources["ContentDialogMaxWidth"] = 820d;
            await dialog.ShowAsync();
        }
        catch (Exception)
        {
            ReadError.Message = LemonLocalization.GetString("LicenseConsent_ReadError");
            ReadError.IsOpen = true;
        }
    }

    private static TextBox ReadOnlyText(string text) => new()
    {
        IsReadOnly = true, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, Text = text,
        VerticalAlignment = VerticalAlignment.Stretch, HorizontalAlignment = HorizontalAlignment.Stretch
    };
}
