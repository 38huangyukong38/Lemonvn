using LemonVN.ViewModels.Anime;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GalgameManager.Contracts.Services;
using GalgameManager.Contracts.ViewModels;
using LemonVN.Application.Anime;
using LemonVN.Contracts.Services;
using LemonVN.Domain.Anime;
using LemonVN.Helpers;
using LemonVN.Services;

namespace LemonVN.ViewModels.Anime;

public partial class AnimeDetailViewModel(AnimeCollectionService collection, ICoverStorageService covers,
    INavigationService navigation, IAnimeUiService dialogs, AppLogService log, AnimeProgressViewModel progress) : ObservableObject, INavigationAware
{
    public AnimeProgressViewModel Progress { get; } = progress;
    private AnimeEntry? _entry;
    private bool _active;
    [ObservableProperty] private string _title = string.Empty;
    [ObservableProperty] private string _originalTitle = string.Empty;
    [ObservableProperty] private string _description = string.Empty;
    [ObservableProperty] private string _animationStudios = string.Empty;
    [ObservableProperty] private string _coverSource = Infrastructure.Anime.CoverStorageService.Placeholder;
    [ObservableProperty] private string _status = string.Empty;
    [ObservableProperty] private string _episodes = string.Empty;
    [ObservableProperty] private string _airDate = string.Empty;
    [ObservableProperty] private string _rating = string.Empty;
    [ObservableProperty] private string _notes = string.Empty;
    [ObservableProperty] private string _bangumiSource = string.Empty;
    [ObservableProperty] private bool _hasBangumiSource;
    [ObservableProperty] private IReadOnlyList<string> _tags = [];
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private bool _isErrorOpen;
    [ObservableProperty] private string _errorMessage = string.Empty;

    public async void OnNavigatedTo(object parameter)
    {
        _active = true;
        Progress.Saved += ApplyEntry;
        Progress.Reloaded += ApplyEntry;
        IsBusy = true;
        try
        {
            AnimeEntry entry = await collection.GetByIdAsync((Guid)parameter) ?? throw new KeyNotFoundException("Anime no longer exists.");
            if (!_active) return;
            ApplyEntry(entry);
        }
        catch (Exception error) { Report(error); }
        finally { IsBusy = false; }
    }

    private void ApplyEntry(AnimeEntry entry)
    {
        _entry = entry;
        Title = entry.Metadata.Title;
        OriginalTitle = entry.Metadata.OriginalTitle ?? string.Empty;
        Description = entry.Metadata.Description ?? string.Empty;
        AnimationStudios = entry.Metadata.AnimationStudios.Count == 0 ? LemonLocalization.GetString("Browse_Unassigned")
            : string.Join(" · ", entry.Metadata.AnimationStudios);
        CoverSource = covers.GetDisplaySource(entry.Metadata.CoverRef);
        Status = LemonLocalization.GetString("Anime_Status_" + entry.Record.WatchStatus);
        Episodes = entry.Metadata.TotalEpisodes?.ToString() ?? LemonLocalization.GetString("Anime_Unknown");
        AirDate = entry.Metadata.AirDate?.ToString("yyyy-MM-dd") ?? LemonLocalization.GetString("Anime_Unknown");
        Rating = entry.Record.PersonalRating?.ToString() ?? LemonLocalization.GetString("Anime_Unrated");
        Notes = entry.Record.Notes ?? string.Empty;
        HasBangumiSource = entry.Metadata.BangumiId is not null;
        BangumiSource = HasBangumiSource ? "Bangumi #" + entry.Metadata.BangumiId : LemonLocalization.GetString("Bangumi_ManualSource");
        Tags = entry.Record.Tags.ToList();
        Progress.Load(entry);
    }
    public void OnNavigatedFrom()
    {
        _active = false;
        Progress.Saved -= ApplyEntry;
        Progress.Reloaded -= ApplyEntry;
        Progress.Deactivate();
    }

    [RelayCommand]
    private void Edit()
    {
        if (_entry is not null && !Progress.IsBusy) navigation.NavigateTo(typeof(AnimeEditViewModel).FullName!, _entry.Id);
    }

    [RelayCommand]
    private void Library() => navigation.NavigateTo(typeof(AnimeLibraryViewModel).FullName!, clearNavigation: true);

    [RelayCommand]
    private void LinkBangumi()
    {
        if (_entry is not null && !Progress.IsBusy)
            navigation.NavigateTo(typeof(AnimeLookupViewModel).FullName!, new AnimeLookupRequest(_entry.Id, Title));
    }

    [RelayCommand]
    private void RefreshBangumi()
    {
        if (_entry?.Metadata.BangumiId is not null && !Progress.IsBusy)
            navigation.NavigateTo(typeof(AnimeLookupViewModel).FullName!, new AnimeLookupRequest(_entry.Id, Title, true));
    }

    [RelayCommand]
    private async Task DeleteAsync()
    {
        if (_entry is null || IsBusy || Progress.IsBusy) return;
        IsBusy = true;
        try
        {
            if (!await dialogs.ConfirmDeleteAsync(_entry.Metadata.Title)) return;
            await collection.DeleteAsync(_entry);
            Library();
        }
        catch (Exception error) { Report(error); }
        finally { IsBusy = false; }
    }

    private void Report(Exception error)
    {
        log.Error(error, "Anime detail operation failed.");
        ErrorMessage = LemonLocalization.GetString("Anime_OperationFailed", error.Message);
        IsErrorOpen = true;
    }
}
