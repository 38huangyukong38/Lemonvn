using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LemonVN.Application.Anime;
using LemonVN.Contracts.Services;
using LemonVN.Domain.Anime;
using LemonVN.Helpers;
using LemonVN.Services;

namespace LemonVN.ViewModels.Anime;

public sealed record EpisodeProgressItem(int EpisodeNumber, bool IsWatched, string Label,
    IAsyncRelayCommand<int> ToggleCommand);

public partial class AnimeProgressViewModel(AnimeProgressService progress, AnimeCollectionService collection,
    IAnimeUiService dialogs, AppLogService log, Func<string, object[], string>? translate = null) : ObservableObject
{
    private readonly Func<string, object[], string> _translate = translate ?? LemonLocalization.GetString;
    private AnimeEntry? _entry;
    private AnimeProgressState? _state;
    private bool _active;
    private int _firstEpisode = 1;
    public event Action<AnimeEntry>? Saved;
    public event Action<AnimeEntry>? Reloaded;
    public ObservableCollection<EpisodeProgressItem> EpisodeItems { get; } = [];
    [ObservableProperty] private int _watchedCount;
    [ObservableProperty] private string _summary = string.Empty;
    [ObservableProperty] private double _percentage;
    [ObservableProperty] private bool _hasKnownTotal;
    [ObservableProperty] private bool _hasOutOfRange;
    [ObservableProperty] private string _outOfRangeMessage = string.Empty;
    [ObservableProperty] private string _undoLabel = string.Empty;
    [ObservableProperty] private string _throughText = string.Empty;
    [ObservableProperty] private string _pageStartText = "1";
    [ObservableProperty] private string _pageRange = string.Empty;
    [ObservableProperty] private bool _isExpanded;
    [ObservableProperty] private bool _isErrorOpen;
    [ObservableProperty] private string _errorMessage = string.Empty;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanInteract), nameof(CanMarkNext), nameof(CanUndo), nameof(CanPreviousPage), nameof(CanNextPage))]
    private bool _isBusy;

    public bool CanInteract => _entry is not null && !IsBusy;
    public bool CanMarkNext => CanInteract && _state?.NextUnwatched is not null;
    public bool CanUndo => CanInteract && _state?.HighestWatched is not null;
    public bool CanPreviousPage => CanInteract && _firstEpisode > 1;
    public bool CanNextPage => CanInteract && _state is not null && (long)_firstEpisode + AnimeProgressService.PageSize <= _state.LastDisplayEpisode;

    public void Load(AnimeEntry entry)
    {
        _active = true;
        _entry = entry.Copy();
        _state = progress.GetProgress(_entry);
        WatchedCount = _state.WatchedCount;
        HasKnownTotal = _state.TotalEpisodes is not null;
        Summary = HasKnownTotal ? Text("AnimeProgress_Known", WatchedCount, _state.TotalEpisodes!)
            : Text("AnimeProgress_Unknown", WatchedCount);
        Percentage = _state.Percentage;
        HasOutOfRange = _state.OutOfRangeCount > 0;
        OutOfRangeMessage = HasOutOfRange ? Text("AnimeProgress_Outside", _state.OutOfRangeCount) : string.Empty;
        UndoLabel = _state.HighestWatched is { } highest ? Text("AnimeProgress_Undo", highest)
            : Text("AnimeProgress_UndoEmpty");
        _firstEpisode = Math.Min(_firstEpisode, _state.LastDisplayEpisode);
        RefreshPage();
        NotifyAvailability();
    }

    public void Deactivate() => _active = false;

    partial void OnIsExpandedChanged(bool value) => RefreshPage();

    private void RefreshPage()
    {
        EpisodeItems.Clear();
        if (_entry is null || _state is null) return;
        if (IsExpanded)
        {
            foreach (EpisodeState episode in progress.GetEpisodeStates(_entry, _firstEpisode))
            {
                string label = $"EP {episode.EpisodeNumber:D2} · " + Text(episode.IsWatched ? "AnimeProgress_Watched" : "AnimeProgress_Unwatched");
                if (episode.IsOutOfRange) label += " · " + Text("AnimeProgress_OutsideLabel");
                EpisodeItems.Add(new(episode.EpisodeNumber, episode.IsWatched, label, ToggleEpisodeCommand));
            }
        }
        int last = (int)Math.Min(_state.LastDisplayEpisode, (long)_firstEpisode + AnimeProgressService.PageSize - 1);
        PageRange = Text("AnimeProgress_PageRange", _firstEpisode, last, _state.LastDisplayEpisode);
        PageStartText = _firstEpisode.ToString(CultureInfo.InvariantCulture);
    }

    [RelayCommand]
    private Task MarkNextAsync() => RunAsync(async entry => await progress.MarkNextUnwatchedAsync(entry.Id, entry.Revision));

    [RelayCommand]
    private Task UndoHighestAsync() => RunAsync(async entry => await progress.UnmarkHighestWatchedAsync(entry.Id, entry.Revision));

    [RelayCommand]
    private Task ToggleEpisodeAsync(int number) => RunAsync(async entry => await progress.SetEpisodeWatchedAsync(entry.Id,
        entry.Revision, number, !entry.Record.Episodes.Any(episode => episode.EpisodeNumber == number && episode.IsWatched)));

    [RelayCommand]
    private Task MarkThroughAsync() => RunAsync(async entry =>
    {
        if (!int.TryParse(ThroughText, NumberStyles.None, CultureInfo.InvariantCulture, out int number) ||
            number is <= 0 or > AnimeProgressService.MaxBatchEpisodes)
            throw new AnimeValidationException(Text("AnimeProgress_InvalidBatch", AnimeProgressService.MaxBatchEpisodes));
        if (entry.Metadata.TotalEpisodes is { } total && number > total)
            throw new AnimeValidationException(Text("AnimeProgress_AboveTotal", total));
        AnimeProgressState state = progress.GetProgress(entry);
        // Make the fill-gaps/retain-later semantics explicit when there are skips or later watches.
        if ((state.HighestWatched > state.WatchedCount || state.HighestWatched > number) &&
            !await dialogs.ConfirmMarkThroughAsync(number)) return null;
        return await progress.MarkWatchedThroughAsync(entry.Id, entry.Revision, number);
    });

    private async Task RunAsync(Func<AnimeEntry, Task<AnimeEntry?>> operation)
    {
        if (!CanInteract || !_active) return;
        AnimeEntry before = _entry!.Copy();
        IsBusy = true;
        IsErrorOpen = false;
        try
        {
            AnimeEntry? saved = await operation(before);
            if (saved is null || !_active || _entry?.Id != saved.Id || _entry.Revision > saved.Revision) return;
            Load(saved); // Never show an optimistic state before persistence succeeds.
            Saved?.Invoke(saved);
        }
        catch (Exception error)
        {
            log.Error(error, "Anime progress was not confirmed saved; reloading persisted state.");
            if (!_active) return;
            try
            {
                AnimeEntry? latest = await collection.GetByIdAsync(before.Id);
                if (_active && latest is not null)
                {
                    Load(latest);
                    Reloaded?.Invoke(latest); // Keep the detail page's revision/personal fields current too.
                }
                else if (latest is null) { _entry = null; NotifyAvailability(); }
            }
            catch (Exception reloadError)
            {
                log.Error(reloadError, "Could not reload saved anime progress.");
            }
            if (!_active) return;
            ErrorMessage = error is AnimeConflictException ? Text("AnimeProgress_Conflict")
                : error is AnimeValidationException ? error.Message : Text("AnimeProgress_SaveFailed", error.Message);
            IsErrorOpen = true;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void PreviousPage()
    {
        if (!CanPreviousPage) return;
        _firstEpisode = Math.Max(1, _firstEpisode - AnimeProgressService.PageSize);
        RefreshPage();
        NotifyAvailability();
    }

    [RelayCommand]
    private void NextPage()
    {
        if (!CanNextPage) return;
        _firstEpisode += AnimeProgressService.PageSize;
        RefreshPage();
        NotifyAvailability();
    }

    [RelayCommand]
    private void JumpPage()
    {
        if (!CanInteract || _state is null) return;
        if (!int.TryParse(PageStartText, NumberStyles.None, CultureInfo.InvariantCulture, out int first) ||
            first <= 0 || first > _state.LastDisplayEpisode)
        {
            ErrorMessage = Text("AnimeProgress_InvalidPage", _state.LastDisplayEpisode);
            IsErrorOpen = true;
            return;
        }
        _firstEpisode = first;
        RefreshPage();
        NotifyAvailability();
        IsErrorOpen = false;
    }

    private string Text(string key, params object[] arguments) => _translate(key, arguments);
    private void NotifyAvailability()
    {
        OnPropertyChanged(nameof(CanInteract));
        OnPropertyChanged(nameof(CanMarkNext));
        OnPropertyChanged(nameof(CanUndo));
        OnPropertyChanged(nameof(CanPreviousPage));
        OnPropertyChanged(nameof(CanNextPage));
    }
}
