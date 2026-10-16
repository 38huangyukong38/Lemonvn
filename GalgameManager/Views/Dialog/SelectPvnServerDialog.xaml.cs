using GalgameManager.Helpers;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace GalgameManager.Views.Dialog;

public sealed partial class SelectPvnServerDialog
{
    public string? ServerUrl;
    private readonly HttpClient _httpClient = Utils.GetDefaultHttpClient();
    
    public SelectPvnServerDialog()
    {
        InitializeComponent();
        RequestedTheme = App.MainWindow?.Content is Microsoft.UI.Xaml.FrameworkElement element ? element.RequestedTheme : RequestedTheme;

        XamlRoot = App.MainWindow!.Content.XamlRoot;
        Title = "SelectPvnServerDialog_Title".GetLocalized();
        PrimaryButtonText = "Yes".GetLocalized();
        IsPrimaryButtonEnabled = false;
        DefaultButton = ContentDialogButton.Primary;
        PrimaryButtonClick += (_, _) =>
        {
            ServerUrl = TextBox.Text;
        };
        SecondaryButtonText = "Cancel".GetLocalized();
        TextBox.PlaceholderText = "SelectPvnServerDialog_Placeholder".GetLocalized();
        Button.Content = "SelectPvnServerDialog_Check".GetLocalized();
    }

    private async void Button_Click(object sender, RoutedEventArgs e)
    {
        Button.Content = "SelectPvnServerDialog_Checking".GetLocalized();
        try
        {
            if (!(TextBox.Text.StartsWith("http") || TextBox.Text.StartsWith("https"))) //没有写协议，默认https
                TextBox.Text = "https://" + TextBox.Text;
            Uri baseUri = new(TextBox.Text);
            Button.Content = "Testing";
            HttpResponseMessage response = await _httpClient.GetAsync(new Uri(baseUri, "Server/info"));
            IsPrimaryButtonEnabled = response.IsSuccessStatusCode;
            Button.Content = response.IsSuccessStatusCode ? "OK" : "Failed";
        }
        catch
        {
            Button.Content = "Failed";
            Reset();
        }
    }

    private void TextBox_OnTextChanged(object? sender, TextChangedEventArgs? e)
    {
        Reset();
        Button.Content = "SelectPvnServerDialog_Check".GetLocalized();
    }

    private void Reset()
    {
        IsPrimaryButtonEnabled = false;
        ServerUrl = null;
    }
}