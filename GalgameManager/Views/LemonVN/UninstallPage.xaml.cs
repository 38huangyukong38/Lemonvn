using GalgameManager;
using LemonVN.Contracts.Services;
using LemonVN.Helpers;
using LemonVN.Infrastructure.Uninstall;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace LemonVN.Views;

public sealed partial class UninstallPage : Page
{
    private readonly string _dataRoot;
    private readonly string _applicationRoot;
    private bool _busy;
    public string ApplicationLocation => Text("Uninstall_ApplicationPath", _applicationRoot);
    public string DataLocation => Text("Uninstall_DataPath", _dataRoot);
    public UninstallPage(IAppDataPathService paths)
    {
        _applicationRoot = UninstallService.FindApplicationRoot(AppContext.BaseDirectory);
        _dataRoot = paths.RootPath; // Computing a path does not initialize settings, logs or LiteDB.
        InitializeComponent();
    }
    private void OnCancel(object sender, RoutedEventArgs args) => App.SetWindowMode(GalgameManager.Enums.WindowMode.Close);
    private async void OnContinue(object sender, RoutedEventArgs args)
    {
        if (_busy) return;
        _busy = true; Continue.IsEnabled = false; DeleteData.IsEnabled = false; Error.IsOpen = false;
        try
        {
            bool deleteData = DeleteData.IsChecked == true;
            UninstallPlan plan = await Task.Run(() => UninstallService.Prepare(_applicationRoot, _dataRoot, deleteData));
            var dialog = new ContentDialog
            {
                XamlRoot = XamlRoot, Title = Text("Uninstall_ConfirmTitle"),
                Content = new TextBlock { Text = Text(deleteData ? "Uninstall_ConfirmDelete" : "Uninstall_ConfirmKeep", _dataRoot), TextWrapping = TextWrapping.Wrap },
                PrimaryButtonText = Text("Uninstall_Confirm"), CloseButtonText = Text("Uninstall_CancelText"), DefaultButton = ContentDialogButton.Close
            };
            if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;
            CancelButton.IsEnabled = false;
            await Task.Run(() => UninstallService.LaunchConfirmed(plan));
            App.SetWindowMode(GalgameManager.Enums.WindowMode.Close);
        }
        catch (Exception error)
        {
            Error.Message = Text("Uninstall_Error", error.Message); Error.IsOpen = true;
        }
        finally { _busy = false; Continue.IsEnabled = true; DeleteData.IsEnabled = true; CancelButton.IsEnabled = true; }
    }
    private static string Text(string key, params object[] args) => LemonLocalization.GetString(key, args);
}
