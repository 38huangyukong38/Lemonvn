using LemonVN.ViewModels.Anime;
using System.Globalization;
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

public partial class AnimeEditViewModel(AnimeCollectionService collection, ICoverStorageService covers,
    INavigationService navigation, IAnimeUiService dialogs, AppLogService log) : ObservableObject, INavigationAware
{
    private AnimeEntry _draft = new();
    private bool _isNew;
    private bool _active;
    [ObservableProperty] private string _heading = string.Empty;
    [ObservableProperty] private string _title = string.Empty;
    [ObservableProperty] private string _originalTitle = string.Empty;
    [ObservableProperty] private string _description = string.Empty;
    [ObservableProperty] private string _animationStudiosText = string.Empty;
    [ObservableProperty] private string _totalEpisodesText = string.Empty;
    [ObservableProperty] private string _airDateText = string.Empty;
    [ObservableProperty] private string _coverSourcePath = string.Empty;
    [ObservableProperty] private string _currentCoverSource = Infrastructure.Anime.CoverStorageService.Placeholder;
    [ObservableProperty] private bool _removeCover;
    [ObservableProperty] private int _statusIndex;
    [ObservableProperty] private string _ratingText = string.Empty;
    [ObservableProperty] private string _notes = string.Empty;
    [ObservableProperty] private string _tagsText = string.Empty;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private bool _canSearchBangumi;
    [ObservableProperty] private bool _isErrorOpen;
    [ObservableProperty] private string _errorMessage = string.Empty;

    public async void OnNavigatedTo(object parameter)
    {
        _active = true;
        IsBusy = true;
        try
        {
            _isNew = parameter is not Guid;
            CanSearchBangumi = _isNew;
            AnimeEntry entry = _isNew ? new() : await collection.GetByIdAsync((Guid)parameter)
                ?? throw new KeyNotFoundException("Anime no longer exists.");
            if (!_active) return;
            _draft = entry.Copy();
            Heading = LemonLocalization.GetString(_isNew ? "Anime_Add" : "Anime_Edit");
            Title = entry.Metadata.Title;
            OriginalTitle = entry.Metadata.OriginalTitle ?? string.Empty;
            Description = entry.Metadata.Description ?? string.Empty;
            AnimationStudiosText = string.Join(Environment.NewLine, entry.Metadata.AnimationStudios);
            TotalEpisodesText = entry.Metadata.TotalEpisodes?.ToString() ?? string.Empty;
            AirDateText = entry.Metadata.AirDate?.ToString("yyyy-MM-dd") ?? string.Empty;
            StatusIndex = (int)entry.Record.WatchStatus;
            RatingText = entry.Record.PersonalRating?.ToString() ?? string.Empty;
            Notes = entry.Record.Notes ?? string.Empty;
            TagsText = string.Join(", ", entry.Record.Tags);
            CurrentCoverSource = covers.GetDisplaySource(entry.Metadata.CoverRef);
        }
        catch (Exception error) { Report(error); }
        finally { IsBusy = false; }
    }
    public void OnNavigatedFrom() => _active = false;

    [RelayCommand]
    private void SearchBangumi()
    {
        if (_isNew) navigation.NavigateTo(typeof(AnimeLookupViewModel).FullName!, new AnimeLookupRequest(Keyword: Title));
    }

    [RelayCommand]
    private async Task PickCoverAsync()
    {
        try
        {
            string? file = await dialogs.PickCoverAsync();
            if (file is not null) { CoverSourcePath = file; RemoveCover = false; }
        }
        catch (Exception error) { Report(error); }
    }

    [RelayCommand]
    private void Cancel()
    {
        if (!navigation.GoBack()) navigation.NavigateTo(typeof(AnimeLibraryViewModel).FullName!, clearNavigation: true);
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        try
        {
            if (string.IsNullOrWhiteSpace(Title)) throw new AnimeValidationException(LemonLocalization.GetString("Anime_InvalidTitle"));
            _draft.Metadata.Title = Title;
            _draft.Metadata.OriginalTitle = Optional(OriginalTitle);
            _draft.Metadata.Description = Optional(Description);
            try { _draft.Metadata.AnimationStudios = AnimationStudioRules.Parse(AnimationStudiosText); }
            catch (AnimeValidationException) { throw new AnimeValidationException(LemonLocalization.GetString("Browse_InvalidStudios", AnimationStudioRules.MaxStudios, AnimationStudioRules.MaxLength)); }
            _draft.Metadata.TotalEpisodes = ParseOptionalInteger(TotalEpisodesText, 1, int.MaxValue, "Anime_InvalidEpisodes");
            if (!string.IsNullOrWhiteSpace(AirDateText) && !DateOnly.TryParseExact(AirDateText, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out _)) throw new AnimeValidationException(LemonLocalization.GetString("Anime_InvalidDate"));
            _draft.Metadata.AirDate = string.IsNullOrWhiteSpace(AirDateText) ? null : DateOnly.ParseExact(AirDateText, "yyyy-MM-dd", CultureInfo.InvariantCulture);
            _draft.Record.WatchStatus = (WatchStatus)StatusIndex;
            _draft.Record.PersonalRating = ParseOptionalInteger(RatingText, 0, 10, "Anime_InvalidRating");
            _draft.Record.Notes = Optional(Notes);
            try { _draft.Record.Tags = AnimeTagRules.Parse(TagsText); }
            catch (AnimeValidationException) { throw new AnimeValidationException(LemonLocalization.GetString("Anime_InvalidTags", AnimeTagRules.MaxLength, AnimeTagRules.MaxTags)); }
            AnimeEntry saved = await collection.SaveAsync(_draft, _isNew, Optional(CoverSourcePath), RemoveCover);
            _isNew = false;
            _draft = saved.Copy();
            navigation.NavigateTo(typeof(AnimeDetailViewModel).FullName!, saved.Id, clearNavigation: true);
        }
        catch (Exception error) { Report(error); }
        finally { IsBusy = false; }
    }

    private static string? Optional(string value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static int? ParseOptionalInteger(string text, int min, int max, string errorKey)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        if (!int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out int value) || value < min || value > max)
            throw new AnimeValidationException(LemonLocalization.GetString(errorKey));
        return value;
    }
    private void Report(Exception error)
    {
        log.Error(error, "Anime edit operation failed; original data preserved.");
        ErrorMessage = error is AnimeValidationException ? error.Message : LemonLocalization.GetString("Anime_SaveFailed", error.Message);
        IsErrorOpen = true;
    }
}
