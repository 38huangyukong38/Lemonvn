using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GalgameManager.Contracts.Services;
using GalgameManager.Contracts.ViewModels;
using LemonVN.Application.Anime;
using LemonVN.Contracts.Services;
using LemonVN.Domain.Anime;
using LemonVN.Helpers;
using LemonVN.Infrastructure.Anime;
using LemonVN.Infrastructure.Bangumi;
using LemonVN.Services;

namespace LemonVN.ViewModels.Anime;

public partial class BangumiResultItem(BangumiAnime source) : ObservableObject
{
    public BangumiAnime Source { get; } = source;
    public string Title => Source.Title;
    public string OriginalTitle => Source.OriginalTitle ?? string.Empty;
    public string DateAndId => (Source.AirDate?.ToString("yyyy-MM-dd") ?? "—") + " · Bangumi #" + Source.Id;
    public string Summary => Source.Description is { Length: > 240 } text ? text[..240] + "…" : Source.Description ?? string.Empty;
    [ObservableProperty] private string _cover = CoverStorageService.Placeholder;
}

public sealed record MetadataDiffItem(string Field, string Local, string Remote, string Status);

public partial class AnimeLookupViewModel(IBangumiAnimeProvider provider, AnimeMetadataService metadata,
    IRemoteAnimeCoverCache covers, AnimeCollectionService collection, INavigationService navigation,
    AppLogService log, Func<string, object[], string>? translate = null) : ObservableObject, INavigationAware
{
    private readonly Func<string, object[], string> _translate = translate ?? LemonLocalization.GetString;
    private AnimeLookupRequest _request = new();
    private AnimeMetadataPreview? _preview;
    private CancellationTokenSource? _work;
    private int _generation, _offset, _total;
    private bool _active;
    private string? _lastId;
    private Guid? _duplicateId;
    [ObservableProperty] private string _keyword = string.Empty;
    [ObservableProperty] private IReadOnlyList<BangumiResultItem> _items = [];
    [ObservableProperty] private IReadOnlyList<MetadataDiffItem> _differences = [];
    [ObservableProperty] private string _title = string.Empty;
    [ObservableProperty] private string _originalTitle = string.Empty;
    [ObservableProperty] private string _description = string.Empty;
    [ObservableProperty] private string _dateAndEpisodes = string.Empty;
    [ObservableProperty] private string _sourceId = string.Empty;
    [ObservableProperty] private string _cover = CoverStorageService.Placeholder;
    [ObservableProperty] private string _message = string.Empty;
    [ObservableProperty] private string _notice = string.Empty;
    [ObservableProperty] private string _summary = string.Empty;
    [ObservableProperty] private string _confirmLabel = string.Empty;
    [ObservableProperty] private bool _isErrorOpen;
    [ObservableProperty] private bool _isNoticeOpen;
    [ObservableProperty] private bool _isEmpty;
    [ObservableProperty] private bool _hasPreview;
    [ObservableProperty] private bool _hasDuplicate;
    [ObservableProperty] private bool _showSearch = true;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(CanInteract)), NotifyPropertyChangedFor(nameof(CanConfirm))] private bool _isBusy;
    public bool CanInteract => !IsBusy;
    public bool CanConfirm => HasPreview && !IsBusy;
    partial void OnHasPreviewChanged(bool value) => OnPropertyChanged(nameof(CanConfirm));

    public async void OnNavigatedTo(object parameter) => await ActivateAsync(parameter as AnimeLookupRequest ?? new());
    public async Task ActivateAsync(AnimeLookupRequest request)
    {
        _active = true; _request = request; Keyword = request.Keyword; ShowSearch = !request.Refresh;
        ConfirmLabel = Text(request.ExistingId is null ? "Bangumi_ConfirmAdd" : request.Refresh ? "Bangumi_ConfirmRefresh" : "Bangumi_ConfirmLink");
        if (request.Refresh)
        {
            try
            {
                var entry = await collection.GetByIdAsync(request.ExistingId!.Value) ?? throw new KeyNotFoundException();
                if (entry.Metadata.BangumiId is not { } id) throw new InvalidOperationException();
                await PreviewIdAsync(id);
            }
            catch (Exception error) { Report(error); }
        }
    }
    public void OnNavigatedFrom() { _active = false; Cancel(); }

    private (int Generation, CancellationToken Token) Begin()
    {
        _work?.Cancel(); _work?.Dispose(); _work = new();
        int generation = ++_generation;
        IsBusy = true; IsErrorOpen = false; IsNoticeOpen = false; Notice = string.Empty;
        HasDuplicate = false; HasPreview = false; _preview = null;
        return (generation, _work.Token);
    }
    private bool Current(int generation) => _active && _generation == generation;
    [RelayCommand]
    private void Cancel()
    {
        _work?.Cancel(); _generation++; IsBusy = false; HasPreview = false; _preview = null;
        if (_active) { Message = Text("Bangumi_Cancelled"); IsErrorOpen = true; }
    }

    [RelayCommand] private Task SearchAsync() { _offset = 0; _lastId = null; return SearchPageAsync(); }
    [RelayCommand] private Task RetryAsync() => _lastId is { } id ? PreviewIdAsync(id) : SearchPageAsync();
    [RelayCommand] private Task NextAsync() { if (_offset + BangumiAnimeProvider.PageSize < _total) _offset += BangumiAnimeProvider.PageSize; return SearchPageAsync(); }
    [RelayCommand] private Task PreviousAsync() { _offset = Math.Max(0, _offset - BangumiAnimeProvider.PageSize); return SearchPageAsync(); }

    private async Task SearchPageAsync()
    {
        _lastId = null;
        var work = Begin();
        try
        {
            BangumiSearchResults results = await provider.SearchAnimeAsync(Keyword, _offset, work.Token);
            if (!Current(work.Generation)) return;
            _total = results.Total;
            Items = results.Items.Select(source => new BangumiResultItem(source)).ToArray();
            IsEmpty = Items.Count == 0;
            Summary = Text("Bangumi_SearchCount", Items.Count, _offset + 1, _total);
            await Task.WhenAll(Items.Select(async item =>
            {
                try
                {
                    string image = await covers.GetDisplaySourceAsync(item.Source.ThumbnailUri, work.Token);
                    if (Current(work.Generation)) item.Cover = image;
                }
                catch (IOException) { /* Result metadata stays usable with its local placeholder. */ }
                catch (UnauthorizedAccessException) { /* Cache is optional. */ }
            }));
        }
        catch (OperationCanceledException) { if (Current(work.Generation)) { Message = Text("Bangumi_Cancelled"); IsErrorOpen = true; } }
        catch (Exception error) { if (Current(work.Generation)) Report(error); }
        finally { if (Current(work.Generation)) IsBusy = false; }
    }

    [RelayCommand] private Task PreviewAsync(BangumiResultItem? item) => item is null ? Task.CompletedTask : PreviewIdAsync(item.Source.Id);
    private async Task PreviewIdAsync(string id)
    {
        _lastId = id;
        var work = Begin();
        try
        {
            AnimeMetadataPreview plan = await metadata.PrepareAsync(id, _request.ExistingId, _request.Refresh, work.Token);
            if (!Current(work.Generation)) return;
            _preview = plan;
            Title = plan.Source.Title; OriginalTitle = plan.Source.OriginalTitle ?? string.Empty;
            Description = plan.Source.Description ?? string.Empty; SourceId = "Bangumi #" + plan.Source.Id;
            DateAndEpisodes = Text("Bangumi_DateEpisodes", plan.Source.AirDate?.ToString("yyyy-MM-dd") ?? Text("Anime_Unknown"),
                plan.Source.TotalEpisodes?.ToString() ?? Text("Anime_Unknown"));
            Differences = plan.Changes.Select(change => new MetadataDiffItem(Text("Bangumi_Field_" + change.Field),
                change.Local, change.Remote, Text("Bangumi_Diff_" + change.Kind))).ToArray();
            var warnings = new List<string>();
            if (plan.HasOutOfRangeEpisodes) warnings.Add(Text("Bangumi_KeepEpisodes"));
            if (plan.CoverUnavailable) warnings.Add(Text("Bangumi_CoverUnavailable"));
            if (_request.ExistingId is null && (await collection.GetAllAsync()).Any(entry => entry.Metadata.BangumiId is null &&
                    string.Equals(entry.Metadata.Title, plan.Source.Title, StringComparison.OrdinalIgnoreCase)))
                warnings.Add(Text("Bangumi_SameName"));
            if (!Current(work.Generation)) return;
            string image;
            try { image = await covers.GetDisplaySourceAsync(plan.Source.CoverUri, work.Token); }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException) { image = CoverStorageService.Placeholder; }
            if (!Current(work.Generation)) return;
            Notice = string.Join(Environment.NewLine, warnings); IsNoticeOpen = warnings.Count != 0;
            Cover = image;
            HasPreview = true;
        }
        catch (AnimeAlreadyCollectedException duplicate)
        {
            if (!Current(work.Generation)) return;
            _duplicateId = duplicate.ExistingId; HasDuplicate = true; Message = Text("Bangumi_AlreadyCollected"); IsErrorOpen = true;
        }
        catch (OperationCanceledException) { if (Current(work.Generation)) { Message = Text("Bangumi_Cancelled"); IsErrorOpen = true; } }
        catch (Exception error) { if (Current(work.Generation)) Report(error); }
        finally { if (Current(work.Generation)) IsBusy = false; }
    }

    [RelayCommand]
    private async Task ConfirmAsync()
    {
        if (_preview is null || IsBusy) return;
        IsBusy = true;
        try
        {
            AnimeEntry saved = await metadata.CommitAsync(_preview, _work?.Token ?? default);
            if (_active) navigation.NavigateTo(typeof(AnimeDetailViewModel).FullName!, saved.Id, clearNavigation: true);
        }
        catch (AnimeAlreadyCollectedException duplicate)
        { _duplicateId = duplicate.ExistingId; HasDuplicate = true; HasPreview = false; Message = Text("Bangumi_AlreadyCollected"); IsErrorOpen = true; }
        catch (OperationCanceledException)
        { HasPreview = false; _preview = null; Message = Text("Bangumi_Cancelled"); IsErrorOpen = _active; }
        catch (Exception error) { HasPreview = false; _preview = null; Report(error); }
        finally { IsBusy = false; }
    }
    [RelayCommand] private void OpenExisting() { if (_duplicateId is { } id) navigation.NavigateTo(typeof(AnimeDetailViewModel).FullName!, id, clearNavigation: true); }
    [RelayCommand] private void Manual() { Cancel(); navigation.NavigateTo(typeof(AnimeEditViewModel).FullName!, _request.ExistingId is { } id ? id : null); }
    private string Text(string key, params object[] args) => _translate(key, args);
    private void Report(Exception error)
    {
        log.Error(error, "Bangumi UI operation did not complete.");
        Message = Text(error is BangumiRequestException problem ? "Bangumi_Error_" + problem.Failure
            : error is AnimeConflictException ? "Bangumi_Conflict" : error is ArgumentException ? "Bangumi_InvalidQuery" : "Bangumi_LocalFailure");
        IsErrorOpen = true;
    }
}
