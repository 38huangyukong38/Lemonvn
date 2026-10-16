using LemonVN.ViewModels;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GalgameManager.Contracts.Services;
using GalgameManager.Contracts.ViewModels;
using LemonVN.Application.Anime;
using LemonVN.Domain.Anime;
using LemonVN.Infrastructure.Backup;
using LemonVN.Contracts.Services;
using LemonVN.Helpers;
using LemonVN.Services;
using LemonVN.ViewModels.Anime;

namespace LemonVN.ViewModels.Anime;

public partial class AnimeLibraryViewModel(INavigationService navigation, AnimeCollectionService collection,
    ICoverStorageService covers, AppLogService log, AnimeQueryService query,
    IAnimeUiService dialogs, Func<string, object[], string>? translate = null) : ObservableObject, INavigationAware
{
    private readonly Func<string, object[], string> _translate = translate ?? LemonLocalization.GetString;
    private IReadOnlyList<AnimeEntry> _source = [];
    private int _loadVersion;
    [ObservableProperty] private IReadOnlyList<AnimeCardViewModel> _items = [];
    [ObservableProperty] private IReadOnlyList<string> _availableTags = [];
    [ObservableProperty] private string _search = string.Empty;
    [ObservableProperty] private int _statusIndex;
    [ObservableProperty] private int _sortIndex;
    [ObservableProperty] private bool _sortDescending = true;
    [ObservableProperty] private bool _hasFilters;
    [ObservableProperty] private string _filterSummary = string.Empty;
    [ObservableProperty] private string _sortSummary = string.Empty;
    [ObservableProperty] private string? _selectedTag;
    [ObservableProperty] private string? _animationStudio;
    [ObservableProperty] private bool _unassignedStudio;
    [ObservableProperty] private bool _onlyWithWatchTime;
    [ObservableProperty] private string _resultSummary = string.Empty;
    [ObservableProperty] private bool _isLibraryEmpty = true;
    [ObservableProperty] private bool _hasNoMatches;
    [ObservableProperty] private bool _hasTransferMessage;
    [ObservableProperty] private string _transferMessage = string.Empty;
    [ObservableProperty] private bool _isEmpty = true;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private bool _canUndoDelete;
    [ObservableProperty] private bool _isErrorOpen;
    [ObservableProperty] private string _errorMessage = string.Empty;
    private bool _active;
    private bool _batchQuery;

    public string LibraryTitle => _translate("Ux_LibraryTitle", []);

    public async void OnNavigatedTo(object parameter)
    {
        if (parameter is AnimeLibraryRequest request) ApplyRequest(request);
        if (parameter is BackupRestoreResult result)
        {
            ClearFilters();
            TransferMessage = _translate("Backup_Restored", [result.AnimeCount, result.TrashCount, result.RecoveryBackupPath]);
            HasTransferMessage = true;
        }
        await ActivateAsync();
    }

    public async Task ActivateAsync()
    {
        _active = true;
        await LoadAsync();
    }
    public void OnNavigatedFrom() { _active = false; _loadVersion++; }

    partial void OnSearchChanged(string value) => ApplyQuery();
    partial void OnStatusIndexChanged(int value) => ApplyQuery();
    partial void OnSortIndexChanged(int value) => ApplyQuery();
    partial void OnSortDescendingChanged(bool value) => ApplyQuery();
    partial void OnSelectedTagChanged(string? value) => ApplyQuery();
    partial void OnAnimationStudioChanged(string? value) => ApplyQuery();
    partial void OnUnassignedStudioChanged(bool value) => ApplyQuery();
    partial void OnOnlyWithWatchTimeChanged(bool value) => ApplyQuery();

    public void ApplyRequest(AnimeLibraryRequest request)
    {
        _batchQuery = true;
        try
        {
            ClearFilters();
            StatusIndex = request.Status is { } status ? (int)status + 1 : 0;
            AnimationStudio = request.AnimationStudio;
            UnassignedStudio = request.UnassignedStudio;
            OnlyWithWatchTime = request.Recent;
            SortIndex = (int)(request.Recent ? AnimeSort.LastWatched : AnimeSort.Updated);
            SortDescending = true;
        }
        finally { _batchQuery = false; }
        ApplyQuery();
    }

    private void ApplyQuery()
    {
        if (_batchQuery) return;
        WatchStatus? status = StatusIndex is >= 1 and <= 5 ? (WatchStatus)(StatusIndex - 1) : null;
        AnimeSort sort = (AnimeSort)Math.Clamp(SortIndex, 0, Enum.GetValues<AnimeSort>().Length - 1);
        var entries = query.Apply(_source, new(Search, status, SelectedTag, sort, SortDescending,
            AnimationStudio, UnassignedStudio, OnlyWithWatchTime));
        // Replace the data source once; do not send 1000 Add notifications or create bitmaps here.
        Items = entries.Select(entry => new AnimeCardViewModel(entry, covers, _translate)).ToList();
        IsEmpty = Items.Count == 0;
        IsLibraryEmpty = _source.Count == 0;
        HasNoMatches = !IsLibraryEmpty && IsEmpty;
        ResultSummary = _translate("AnimeLibrary_Count", [Items.Count, _source.Count]);
        HasFilters = !string.IsNullOrWhiteSpace(Search) || status is not null || SelectedTag is not null ||
            AnimationStudio is not null || UnassignedStudio || OnlyWithWatchTime;
        string all = _translate("Ux_All", []);
        FilterSummary = _translate("Ux_FilterSummary", [string.IsNullOrWhiteSpace(Search) ? all : Search,
            status is { } selected ? _translate("Anime_Status_" + selected, []) : all, SelectedTag ?? all]);
        if (AnimationStudio is not null || UnassignedStudio)
            FilterSummary += " · " + _translate("Browse_StudioFilter", [AnimationStudio ?? _translate("Browse_Unassigned", [])]);
        if (OnlyWithWatchTime) FilterSummary += " · " + _translate("Browse_RecentFilter", []);
        SortSummary = _translate("Ux_SortSummary", [_translate("Ux_Sort_" + sort, []),
            _translate(SortDescending ? "Ux_Descending" : "Ux_Ascending", [])]);
    }

    [RelayCommand]
    private void ClearFilters()
    {
        Search = string.Empty;
        StatusIndex = 0;
        SelectedTag = null;
        AnimationStudio = null;
        UnassignedStudio = false;
        OnlyWithWatchTime = false;
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        int version = ++_loadVersion;
        IsBusy = true;
        try
        {
            var entries = await collection.GetAllAsync();
            if (!_active || version != _loadVersion) return;
            _source = entries;
            // Replacing a ComboBox ItemsSource can send a transient null selection back
            // through its TwoWay binding. Retain valid filters across page reloads.
            string? selectedTag = SelectedTag;
            IReadOnlyList<string> tags = query.GetTags(entries);
            if (!AvailableTags.SequenceEqual(tags, StringComparer.OrdinalIgnoreCase)) AvailableTags = tags;
            SelectedTag = selectedTag is null ? null : tags.FirstOrDefault(tag =>
                string.Equals(tag, selectedTag, StringComparison.OrdinalIgnoreCase));
            ApplyQuery();
            CanUndoDelete = collection.LastDeletedId is not null;
            IsErrorOpen = false;
        }
        catch (Exception error)
        {
            log.Error(error, "Loading anime library failed.");
            ErrorMessage = _translate("Anime_LoadFailed", [error.Message]);
            IsErrorOpen = true;
        }
        finally { if (version == _loadVersion) IsBusy = false; }
    }

    [RelayCommand]
    private void AddAnime() => navigation.NavigateTo(typeof(AnimeEditViewModel).FullName!);
    [RelayCommand]
    private void SearchBangumi() => navigation.NavigateTo(typeof(AnimeLookupViewModel).FullName!, new AnimeLookupRequest());

    [RelayCommand]
    private void OpenAnime(AnimeCardViewModel? card)
    {
        if (card is not null) navigation.NavigateTo(typeof(AnimeDetailViewModel).FullName!, card.Id);
    }

    [RelayCommand]
    private async Task UndoDeleteAsync()
    {
        try { await collection.RestoreLastDeletedAsync(); await LoadAsync(); }
        catch (Exception error)
        {
            log.Error(error, "Restoring anime failed.");
            ErrorMessage = _translate("Anime_SaveFailed", [error.Message]);
            IsErrorOpen = true;
        }
    }

    [RelayCommand] private void ClearSearch() => Search = string.Empty;
    [RelayCommand] private void ClearTag() => SelectedTag = null;

    [RelayCommand]
    private void EditAnime(AnimeCardTarget? target)
    {
        if (target is not null && !IsBusy) navigation.NavigateTo(typeof(AnimeEditViewModel).FullName!, target.Id);
    }

    [RelayCommand]
    private void DownloadInfo(AnimeCardTarget? target)
    {
        if (target is not null && !IsBusy)
            navigation.NavigateTo(typeof(AnimeLookupViewModel).FullName!,
                new AnimeLookupRequest(target.Id, target.Title, target.BangumiId is not null));
    }

    private async Task<AnimeEntry> ReadTargetAsync(AnimeCardTarget target)
    {
        AnimeEntry entry = await collection.GetByIdAsync(target.Id) ?? throw new KeyNotFoundException("Anime no longer exists.");
        if (entry.Revision != target.Revision) throw new AnimeConflictException("The anime changed; reopen its menu and try again.");
        return entry;
    }

    [RelayCommand]
    private async Task ChangeStatusAsync(AnimeStatusChange? change)
    {
        if (change is null || IsBusy) return;
        IsBusy = true;
        try
        {
            if (!Enum.IsDefined(change.Status)) throw new ArgumentOutOfRangeException(nameof(change));
            AnimeEntry entry = await ReadTargetAsync(change.Target);
            if (entry.Record.WatchStatus != change.Status)
            {
                entry.Record.WatchStatus = change.Status;
                await collection.UpdateRecordAsync(entry.Id, change.Target.Revision, entry.Record);
            }
            await LoadAsync();
        }
        catch (Exception error) { await ReportActionAsync(error); }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task RemoveAnimeAsync(AnimeCardTarget? target)
    {
        if (target is null || IsBusy) return;
        IsBusy = true;
        try
        {
            AnimeEntry entry = await ReadTargetAsync(target);
            if (!await dialogs.ConfirmDeleteAsync(entry.Metadata.Title)) return;
            // Delete checks the captured revision again after the confirmation await.
            await collection.DeleteAsync(entry);
            await LoadAsync();
        }
        catch (Exception error) { await ReportActionAsync(error); }
        finally { IsBusy = false; }
    }

    private async Task ReportActionAsync(Exception error)
    {
        log.Error(error, "Anime library action failed; persisted data preserved.");
        await LoadAsync();
        ErrorMessage = _translate(error is AnimeConflictException ? "Bangumi_Conflict" : "Anime_OperationFailed", [error.Message]);
        IsErrorOpen = true;
    }

    [RelayCommand]
    private void OpenSettings() => navigation.NavigateTo(typeof(LemonSettingsViewModel).FullName!);
    [RelayCommand]
    private void OpenRecycle() => navigation.NavigateTo(typeof(AnimeRecycleViewModel).FullName!);
}
