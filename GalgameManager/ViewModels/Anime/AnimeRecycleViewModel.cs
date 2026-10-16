using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GalgameManager.Contracts.ViewModels;
using LemonVN.Application.Anime;
using LemonVN.Contracts.Services;
using LemonVN.Domain.Anime;
using LemonVN.Helpers;
using LemonVN.Infrastructure.Backup;
using LemonVN.Services;

namespace LemonVN.ViewModels.Anime;

public sealed class AnimeTrashItem(AnimeEntry entry, IAsyncRelayCommand<AnimeTrashItem?> restore,
    IAsyncRelayCommand<AnimeTrashItem?> purge)
{
    public AnimeEntry Entry { get; } = entry;
    public string Title => Entry.Metadata.Title;
    public string Tags => string.Join(" · ", Entry.Record.Tags);
    public IAsyncRelayCommand<AnimeTrashItem?> RestoreCommand { get; } = restore;
    public IAsyncRelayCommand<AnimeTrashItem?> PurgeCommand { get; } = purge;
}

public partial class AnimeRecycleViewModel(IAnimeRepository repository, AnimeCollectionService collection,
    AnimeBackupService backup, IBackupUiService dialogs, AppLogService log,
    Func<string, object[], string>? translate = null) : ObservableObject, INavigationAware
{
    private readonly Func<string, object[], string> _translate = translate ?? LemonLocalization.GetString;
    private bool _active;
    [ObservableProperty] private IReadOnlyList<AnimeTrashItem> _items = [];
    [ObservableProperty] private IReadOnlyList<string> _coverPreview = [];
    [ObservableProperty] private bool _isEmpty = true;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(CanOperate))] private bool _isBusy;
    [ObservableProperty] private bool _isMessageOpen;
    [ObservableProperty] private string _message = string.Empty;
    public bool CanOperate => !IsBusy;
    public async void OnNavigatedTo(object parameter) { _active = true; await ReloadAsync(); }
    public void OnNavigatedFrom() => _active = false;

    [RelayCommand]
    private async Task ReloadAsync()
    {
        try
        {
            var entries = await repository.GetTrashAsync();
            if (!_active) return;
            Items = entries.Select(entry => new AnimeTrashItem(entry, RestoreCommand, PurgeCommand)).ToList();
            IsEmpty = Items.Count == 0;
        }
        catch (Exception error) { Report(error); }
    }

    [RelayCommand]
    private async Task RestoreAsync(AnimeTrashItem? item)
    {
        if (item is null || IsBusy) return;
        IsBusy = true;
        try
        {
            await collection.RestoreArchivedAsync(item.Entry);
            await ReloadAsync();
            Message = Text("Recycle_Restored", item.Title);
            IsMessageOpen = true;
        }
        catch (Exception error) { Report(error); }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task PurgeAsync(AnimeTrashItem? item)
    {
        if (item is null || IsBusy) return;
        IsBusy = true;
        try
        {
            if (!await dialogs.ConfirmPurgeAsync(item.Title, false) || !await dialogs.ConfirmPurgeAsync(item.Title, true)) return;
            string recovery = await Task.Run(() => backup.PermanentlyDeleteAsync(item.Entry));
            await ReloadAsync();
            Message = Text("Recycle_Purged", recovery);
            IsMessageOpen = true;
        }
        catch (Exception error) { Report(error); }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task PreviewCoversAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        try
        {
            CoverPreview = await backup.GetUnreferencedCoverPreviewAsync();
            Message = Text("Recycle_CoverPreview", CoverPreview.Count);
            IsMessageOpen = true;
        }
        catch (Exception error) { Report(error); }
        finally { IsBusy = false; }
    }

    private string Text(string key, params object[] args) => _translate(key, args);
    private void Report(Exception error)
    {
        log.Error(error, "Recycle operation failed; original resources preserved.");
        Message = Text("Backup_Failed", error.Message);
        IsMessageOpen = true;
    }
}
