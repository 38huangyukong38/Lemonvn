using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GalgameManager.Contracts.Services;
using GalgameManager.Contracts.ViewModels;
using LemonVN.Application.Anime;
using LemonVN.Contracts.Services;
using LemonVN.Helpers;
using LemonVN.Services;
using LemonVN.ViewModels.Anime;

namespace LemonVN.ViewModels;

public sealed class RecognitionCandidateItem
{
    public RecognitionCandidate Source { get; }
    public string Title { get; }
    public string Work { get; }
    public string Details { get; }
    public string Aliases { get; }
    public bool HasCharacter => Source.Character is not null;
    public bool HasPreview => Source.Preview is not null;
    public RecognitionCandidateItem(RecognitionCandidate source, Func<string, object[], string> text)
    {
        Source = source;
        Work = source.Work.Length > 0 ? source.Work : text("Recognition_NoTitle", []);
        Title = source.Character ?? Work;
        Aliases = string.Join(" · ", source.Aliases.Where(name => name != source.Work));
        static string Time(double? seconds)
        {
            if (seconds is null) return "—";
            TimeSpan span = TimeSpan.FromSeconds(seconds.Value);
            return ((int)span.TotalHours).ToString("00", CultureInfo.InvariantCulture) + span.ToString(@"\:mm\:ss\.fff", CultureInfo.InvariantCulture);
        }
        Details = source.AniListId is { } id
            ? text("Recognition_SceneDetails", [id, source.Episode ?? "—", Time(source.FromSeconds), Time(source.ToSeconds),
                source.Similarity is { } similarity ? (object)similarity : "—"])
            : text("Recognition_CharacterDetails", [source.Group,
                text(source.LowConfidence is true ? "Recognition_Low" : source.LowConfidence is false ? "Recognition_CandidateOnly" : "Recognition_ConfidenceUnknown", [])]);
    }
}
public sealed record RecognitionLocalMatch(Guid Id, string Title);

public sealed partial class LemonRecognitionViewModel(INavigationService navigation, IRecognitionImageLoader loader,
    IImageRecognitionService recognition, IRecognitionUiService ui, AnimeCollectionService collection, AppLogService log,
    Func<string, object[], string>? translate = null) : LemonLandingViewModel(navigation), INavigationAware
{
    private readonly Func<string, object[], string> _text = translate ?? LemonLocalization.GetString;
    private RecognitionImage? _image;
    private CancellationTokenSource? _work;
    private bool _active;
    private int _generation;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(CanInteract)), NotifyPropertyChangedFor(nameof(CanRecognize)), NotifyPropertyChangedFor(nameof(CanSearch))]
    private bool _isBusy;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(Target)), NotifyPropertyChangedFor(nameof(CanRecognize))] private int _modeIndex;
    [ObservableProperty] private byte[]? _inputPreview;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(HasMatchPreview))] private byte[]? _matchPreview;
    [ObservableProperty] private string _imageSummary = string.Empty;
    [ObservableProperty] private IReadOnlyList<RecognitionCandidateItem> _items = [];
    [ObservableProperty] private IReadOnlyList<RecognitionLocalMatch> _localMatches = [];
    [ObservableProperty, NotifyPropertyChangedFor(nameof(CanSearch))] private string _searchKeyword = string.Empty;
    [ObservableProperty] private string _message = string.Empty;
    [ObservableProperty] private bool _isMessageOpen;
    [ObservableProperty] private bool _hasNoMatches;
    [ObservableProperty] private string _resultSummary = string.Empty;
    [ObservableProperty] private string _quotaText = string.Empty;
    [ObservableProperty] private bool _hasLookedLocal;
    public bool CanInteract => !IsBusy;
    public bool CanRecognize => _active && _image is not null && !IsBusy && ModeIndex is 0 or 1;
    public bool CanSearch => _active && !IsBusy && !string.IsNullOrWhiteSpace(SearchKeyword);
    public bool HasMatchPreview => MatchPreview is not null;
    public string Target => _text("Recognition_Target", [ModeIndex == 0 ? "Trace.moe · api.trace.moe" : "AnimeTrace · api.animetrace.com"]);

    public void OnNavigatedTo(object parameter) { _active = true; NotifyActions(); }
    public void OnNavigatedFrom()
    {
        _active = false; CancelWork(); _image = null; InputPreview = null; MatchPreview = null;
        ClearResults(); ImageSummary = string.Empty; recognition.ClearSession(); NotifyActions();
    }
    private void NotifyActions() { OnPropertyChanged(nameof(CanRecognize)); OnPropertyChanged(nameof(CanSearch)); }
    partial void OnModeIndexChanged(int value) { CancelWork(); ClearResults(); }
    partial void OnSearchKeywordChanged(string value) { LocalMatches = []; HasLookedLocal = false; }
    private bool Current(int generation) => _active && generation == _generation;
    private (int Generation, CancellationToken Token) Begin()
    {
        CancelWork(); _work = new(); IsBusy = true; IsMessageOpen = false;
        return (_generation, _work.Token);
    }
    private void CancelWork()
    {
        _work?.Cancel(); _work?.Dispose(); _work = null; _generation++; IsBusy = false;
    }
    private void ClearResults()
    {
        Items = []; LocalMatches = []; MatchPreview = null; SearchKeyword = string.Empty;
        HasNoMatches = false; HasLookedLocal = false; ResultSummary = string.Empty; QuotaText = string.Empty;
    }

    [RelayCommand]
    private async Task PickImageAsync()
    {
        if (!_active || IsBusy) return;
        var work = Begin();
        try
        {
            string? path = await ui.PickImageAsync();
            if (!Current(work.Generation) || path is null) return;
            _image = null; InputPreview = null; ClearResults(); ImageSummary = string.Empty; NotifyActions();
            RecognitionImage image = await loader.LoadAsync(path, work.Token);
            if (!Current(work.Generation)) return;
            _image = image; InputPreview = image.CopyBytes();
            ImageSummary = _text("Recognition_ImageSummary", [image.Width, image.Height, image.Length / 1024]);
            NotifyActions();
        }
        catch (OperationCanceledException) { /* The current cancel action owns its message. */ }
        catch (Exception error) { if (Current(work.Generation)) Report(error); }
        finally { if (Current(work.Generation)) IsBusy = false; }
    }

    [RelayCommand]
    private async Task RecognizeAsync()
    {
        if (!CanRecognize || _image is not { } image) return;
        RecognitionMode mode = (RecognitionMode)ModeIndex;
        var work = Begin(); ClearResults();
        try
        {
            if (!await ui.ConfirmUploadAsync(mode) || !Current(work.Generation) || ModeIndex != (int)mode) return;
            RecognitionResult result = await recognition.RecognizeAsync(image, mode, work.Token);
            if (!Current(work.Generation)) return;
            Items = result.Candidates.Select(candidate => new RecognitionCandidateItem(candidate, _text)).ToArray();
            HasNoMatches = Items.Count == 0;
            ResultSummary = _text("Recognition_ResultSummary", [Items.Count]);
            if (result.Quota is { } quota) QuotaText = _text(result.QuotaBeforeRequest ? "Recognition_QuotaBefore" : "Recognition_Quota",
                [quota.Remaining, quota.Total, quota.Concurrency]);
            if (result.AiDetected is true) { Message = _text("Recognition_AiNotice", []); IsMessageOpen = true; }
        }
        catch (OperationCanceledException) { /* No late response is published after cancel/navigation. */ }
        catch (Exception error) { if (Current(work.Generation)) Report(error); }
        finally { if (Current(work.Generation)) IsBusy = false; }
    }

    [RelayCommand]
    private void Cancel()
    {
        CancelWork();
        if (_active) { Message = _text("Recognition_Cancelled", []); IsMessageOpen = true; }
    }

    [RelayCommand]
    private async Task SelectCandidateAsync(RecognitionCandidateItem? item)
    {
        if (item is null || !Items.Contains(item) || IsBusy) return;
        SearchKeyword = item.Source.Work;
        await FindLocalMatchesAsync(item.Source.Aliases.Append(item.Source.Work));
    }
    [RelayCommand] private Task FindLocalAsync() => FindLocalMatchesAsync([SearchKeyword]);
    private async Task FindLocalMatchesAsync(IEnumerable<string> names)
    {
        if (!_active || IsBusy) return;
        int generation = _generation;
        string keyword = SearchKeyword;
        var titles = names.Where(name => !string.IsNullOrWhiteSpace(name)).Select(name => name.Trim()).ToHashSet(StringComparer.OrdinalIgnoreCase);
        try
        {
            var entries = await collection.GetAllAsync();
            if (!Current(generation) || keyword != SearchKeyword) return;
            LocalMatches = entries.Where(entry => titles.Contains(entry.Metadata.Title.Trim()) ||
                entry.Metadata.OriginalTitle is { } original && titles.Contains(original.Trim()))
                .Select(entry => new RecognitionLocalMatch(entry.Id, entry.Metadata.Title)).ToArray();
            HasLookedLocal = true;
        }
        catch (Exception error) { if (Current(generation)) Report(error); }
    }

    [RelayCommand]
    private void SearchBangumi()
    {
        if (!CanSearch) return;
        string keyword = SearchKeyword.Trim();
        if (keyword.Length > 200 || keyword.Any(char.IsControl))
        { Message = _text("Bangumi_InvalidQuery", []); IsMessageOpen = true; return; }
        Navigation.NavigateTo(typeof(AnimeLookupViewModel).FullName!, new AnimeLookupRequest(Keyword: keyword));
    }
    [RelayCommand]
    private async Task OpenLocalAsync(RecognitionLocalMatch? match)
    {
        if (!_active || IsBusy || match is null || !LocalMatches.Contains(match)) return;
        int generation = _generation;
        try
        {
            var entry = await collection.GetByIdAsync(match.Id) ?? throw new InvalidOperationException();
            if (Current(generation)) Navigation.NavigateTo(typeof(AnimeDetailViewModel).FullName!, entry.Id);
        }
        catch (Exception error) { if (Current(generation)) Report(error); }
    }

    [RelayCommand]
    private async Task PreviewAsync(RecognitionCandidateItem? item)
    {
        if (!_active || IsBusy || item is null || !Items.Contains(item) || item.Source.Preview is not { } uri) return;
        var work = Begin(); MatchPreview = null;
        try
        {
            byte[] preview = await recognition.GetPreviewAsync(uri, work.Token);
            if (Current(work.Generation)) MatchPreview = preview;
        }
        catch (OperationCanceledException) { }
        catch (Exception error) { if (Current(work.Generation)) Report(error); }
        finally { if (Current(work.Generation)) IsBusy = false; }
    }

    public void RejectInputPreview()
    {
        CancelWork(); _image = null; InputPreview = null; ClearResults(); NotifyActions();
        Report(new RecognitionException(RecognitionFailure.Image));
    }
    public void RejectMatchPreview() { MatchPreview = null; Report(new RecognitionException(RecognitionFailure.Image)); }
    private void Report(Exception error)
    {
        RecognitionFailure failure = error is RecognitionException known ? known.Failure : RecognitionFailure.InvalidResponse;
        // Do not log a picker/IO exception that could contain a private local filename.
        log.Error(new RecognitionException(failure), "Recognition did not complete; local collection unchanged.");
        Message = _text("Recognition_Error_" + failure, []); IsMessageOpen = true;
    }
}
