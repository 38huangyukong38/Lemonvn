using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GalgameManager.Contracts.Services;
using GalgameManager.Contracts.ViewModels;
using GalgameManager.Helpers;
using GalgameManager.Models;
using GalgameManager.Models.Sources;
using Microsoft.UI.Xaml.Controls;

namespace GalgameManager.ViewModels;

public partial class ScanResultViewModel(
    ISourceScanResultService sourceScanResultService,
    IGalgameSourceCollectionService galgameSourceCollectionService,
    IGalgameCollectionService gameService,
    INavigationService navigationService,
    IInfoService infoService)
    : ObservableRecipient, INavigationAware
{
    private GalgameScanResult? _scanResult;
    private string _filterText = string.Empty;

    [ObservableProperty] private string _sourceName = string.Empty;
    [ObservableProperty] private string _sourcePath = string.Empty;
    [ObservableProperty] private DateTime _scanTime;
    [ObservableProperty] private ObservableCollection<PathScanResultItem> _displayResults = new();

    public void OnNavigatedTo(object parameter)
    {
        try
        {
            if (parameter is Guid scanResultId)
            {
                _scanResult = sourceScanResultService.GetScanResult(scanResultId);
                if (_scanResult != null)
                {
                    SourceName = _scanResult.SourceName;
                    ScanTime = _scanResult.ScanTime;
                    GalgameSourceBase? source =
                        galgameSourceCollectionService.GetGalgameSourceFromId(_scanResult.SourceId);
                    SourcePath = source?.Path ?? "N/A (Source may have been removed or changed)";
                    ApplyFilter();
                }
                else
                {
                    // Handle case where scan result is not found
                    SourceName = "Error: Scan result not found.";
                }
            }
        }
        catch (Exception e)
        {
            infoService.DeveloperEvent(e: e);
        }
    }

    public void OnNavigatedFrom()
    {
    }

    public string FilterText
    {
        get => _filterText;
        set
        {
            SetProperty(ref _filterText, value);
            ApplyFilter();
        }
    }

    private void ApplyFilter()
    {
        if (_scanResult == null) return;

        IEnumerable<PathScanResultItem> filtered;
        if (string.IsNullOrWhiteSpace(FilterText))
        {
            filtered = _scanResult.Results;
        }
        else
        {
            var filter = FilterText.ToLowerInvariant();
            filtered = _scanResult.Results.Where(r => r.Path.ToLowerInvariant().Contains(filter) ||
                                                      (r.Message.ToLowerInvariant().Contains(filter)));
        }

        DisplayResults = new ObservableCollection<PathScanResultItem>(filtered);
    }

    [RelayCommand]
    private void CheckGame(PathScanResultItem item)
    {
        Galgame? game = gameService.GetGalgameFromUuid(item.RelatedGameId);
        if (game is null)
        {
            infoService.Info(InfoBarSeverity.Error, "Game not found"); //不应该发生
            return;
        }
        NavigationHelper.NavigateToGalgamePage(navigationService, new GalgamePageParameter{Galgame = game});
    }

    [RelayCommand]
    private async Task ConfirmLink(PathScanResultItem item)
    {
        if (_scanResult is null || !item.RequiresConfirmation) return;
        Galgame? game = gameService.GetGalgameFromUuid(item.RelatedGameId);
        GalgameSourceBase? source = galgameSourceCollectionService.GetGalgameSourceFromId(_scanResult.SourceId);
        if (game is null || source is null)
        {
            infoService.Info(InfoBarSeverity.Error, "ScanResult_LinkFailed".GetLocalized());
            return;
        }

        ContentDialog dialog = new()
        {
            XamlRoot = App.MainWindow!.Content.XamlRoot,
            Title = "MultiInstall_NameMatch_Title".GetLocalized(),
            Content = "MultiInstall_NameMatch_Content".GetLocalized() +
                      $"\n{game.Name.Value}\n{item.Path}",
            PrimaryButtonText = "MultiInstall_LinkInstallation".GetLocalized(),
            CloseButtonText = "Cancel".GetLocalized(),
            DefaultButton = ContentDialogButton.Close,
        };
        if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;

        GalgameAndPath? entry = galgameSourceCollectionService.MoveInNoOperate(source, game, item.Path,
            source is ILocalGalgameSource
                ? new LocalInstallationConfig()
                : null);
        if (entry is null)
        {
            infoService.Info(InfoBarSeverity.Error, "ScanResult_LinkFailed".GetLocalized());
            return;
        }

        item.ResultType = ScanResultType.Success;
        item.Message = "ScanResult_LinkSuccess".GetLocalized() + $" {game.Name.Value}";
        sourceScanResultService.SaveScanResult(_scanResult);
        ApplyFilter();
    }
}