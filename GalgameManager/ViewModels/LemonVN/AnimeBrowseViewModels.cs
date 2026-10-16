using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GalgameManager.Contracts.Services;
using GalgameManager.Contracts.ViewModels;
using LemonVN.Application.Anime;
using LemonVN.Contracts.Services;
using LemonVN.Domain.Anime;
using LemonVN.Helpers;
using LemonVN.Services;
using LemonVN.ViewModels.Anime;

namespace LemonVN.ViewModels;

public enum AnimeCategoryMode { WatchStatus, AnimationStudios }

public abstract partial class AnimeBrowseViewModel(INavigationService navigation, AnimeCollectionService collection,
    AnimeQueryService query, ICoverStorageService covers, AppLogService log, Func<string, object[], string>? translate)
    : LemonLandingViewModel(navigation), INavigationAware
{
    protected readonly AnimeQueryService Query = query;
    protected readonly ICoverStorageService Covers = covers;
    protected readonly Func<string, object[], string> Text = translate ?? LemonLocalization.GetString;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private bool _isErrorOpen;
    [ObservableProperty] private string _errorMessage = string.Empty;
    private bool _active;
    private int _version;

    public virtual async void OnNavigatedTo(object parameter) => await ActivateAsync();
    public void OnNavigatedFrom() { _active = false; _version++; }
    public async Task ActivateAsync() { _active = true; await LoadAsync(); }

    [RelayCommand]
    private async Task LoadAsync()
    {
        int version = ++_version;
        IsBusy = true;
        try
        {
            IReadOnlyList<AnimeEntry> entries = await collection.GetAllAsync();
            if (!_active || version != _version) return;
            Project(entries);
            IsErrorOpen = false;
        }
        catch (Exception error)
        {
            if (!_active || version != _version) return;
            log.Error(error, "Loading anime browse projection failed.");
            ErrorMessage = Text("Anime_LoadFailed", [error.Message]); IsErrorOpen = true;
        }
        finally { if (version == _version) IsBusy = false; }
    }

    protected abstract void Project(IReadOnlyList<AnimeEntry> entries);
    protected void OpenGroup(AnimeLibraryRequest target) => Navigation.NavigateTo(typeof(AnimeLibraryViewModel).FullName!, target);
    protected void OpenAnime(AnimeCardViewModel? card)
    {
        if (card is not null) Navigation.NavigateTo(typeof(AnimeDetailViewModel).FullName!, card.Id);
    }
    protected void OpenStudioCategories() => Navigation.NavigateTo(typeof(LemonCategoriesViewModel).FullName!, AnimeCategoryMode.AnimationStudios);
    [RelayCommand] private void OpenAll() => OpenGroup(new());
}

public sealed class AnimeShelfViewModel(string title, IReadOnlyList<AnimeEntry> entries, AnimeLibraryRequest target,
    ICoverStorageService covers, Func<string, object[], string> text, Action<AnimeLibraryRequest> openGroup, Action<AnimeCardViewModel?> openAnime,
    string emptyKey = "Browse_EmptySection")
{
    public const int PreviewLimit = 12;
    public string Title { get; } = title;
    public int Count { get; } = entries.Count;
    public string CountText { get; } = text("Browse_Count", [entries.Count]);
    public bool IsEmpty => Count == 0;
    public bool HasItems => !IsEmpty;
    public string EmptyText { get; } = text(emptyKey, []);
    public IReadOnlyList<AnimeCardViewModel> Items { get; } = entries.Take(PreviewLimit).Select(e => new AnimeCardViewModel(e, covers, text)).ToArray();
    public IRelayCommand MoreCommand { get; } = new RelayCommand(() => openGroup(target));
    public IRelayCommand<AnimeCardViewModel> OpenAnimeCommand { get; } = new RelayCommand<AnimeCardViewModel>(openAnime);
}

public sealed class AnimeGroupViewModel(string title, int count, AnimeLibraryRequest target,
    Func<string, object[], string> text, Action<AnimeLibraryRequest> openGroup)
{
    public string Title { get; } = title;
    public int Count { get; } = count;
    public string CountText { get; } = text("Browse_Count", [count]);
    public AnimeLibraryRequest Target { get; } = target;
    public IRelayCommand OpenCommand { get; } = new RelayCommand(() => openGroup(target));
}

public sealed partial class LemonHomeViewModel : AnimeBrowseViewModel
{
    [ObservableProperty] private AnimeShelfViewModel _recent = null!;
    [ObservableProperty] private AnimeShelfViewModel _watching = null!;
    [ObservableProperty] private AnimeShelfViewModel _completed = null!;
    [ObservableProperty] private AnimeShelfViewModel _planned = null!;
    [ObservableProperty] private IReadOnlyList<AnimeGroupViewModel> _studios = [];
    [ObservableProperty] private string _librarySummary = string.Empty;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(NoStudios))] private bool _hasStudios;
    public bool NoStudios => !HasStudios;

    public LemonHomeViewModel(INavigationService navigation, AnimeCollectionService collection, AnimeQueryService query,
        ICoverStorageService covers, AppLogService log, Func<string, object[], string>? translate = null)
        : base(navigation, collection, query, covers, log, translate) => Project([]);

    protected override void Project(IReadOnlyList<AnimeEntry> entries)
    {
        AnimeShelfViewModel Shelf(string key, AnimeLibraryRequest target, AnimeQuery filter, string emptyKey = "Browse_EmptySection") =>
            new(Text(key, []), Query.Apply(entries, filter), target, Covers, Text, OpenGroup, OpenAnime, emptyKey);
        Recent = Shelf("Browse_Recent", new(Recent: true), new(Sort: AnimeSort.LastWatched, OnlyWithWatchTime: true), "Browse_NoWatchTime");
        Watching = Shelf("Anime_Status_Watching", new(WatchStatus.Watching), new(Status: WatchStatus.Watching));
        Completed = Shelf("Anime_Status_Completed", new(WatchStatus.Completed), new(Status: WatchStatus.Completed));
        Planned = Shelf("Anime_Status_Planned", new(WatchStatus.Planned), new(Status: WatchStatus.Planned));
        Studios = Query.GetStudioGroups(entries).Take(12).Select(g => new AnimeGroupViewModel(g.Studio ?? Text("Browse_Unassigned", []),
            g.Count, g.Target, Text, OpenGroup)).ToArray();
        HasStudios = Studios.Count > 0;
        LibrarySummary = Text("Browse_LibrarySummary", [entries.Count]);
    }

    [RelayCommand] private void BrowseStudios() => OpenStudioCategories();
}

public sealed partial class LemonCategoriesViewModel : AnimeBrowseViewModel
{
    [ObservableProperty] private int _mode;
    [ObservableProperty] private IReadOnlyList<AnimeGroupViewModel> _statusGroups = [];
    [ObservableProperty] private IReadOnlyList<AnimeGroupViewModel> _studioGroups = [];
    [ObservableProperty] private bool _noStudios;

    public LemonCategoriesViewModel(INavigationService navigation, AnimeCollectionService collection, AnimeQueryService query,
        ICoverStorageService covers, AppLogService log, Func<string, object[], string>? translate = null)
        : base(navigation, collection, query, covers, log, translate) => Project([]);

    public override void OnNavigatedTo(object parameter)
    {
        if (parameter is AnimeCategoryMode mode) Mode = (int)mode;
        base.OnNavigatedTo(parameter);
    }

    protected override void Project(IReadOnlyList<AnimeEntry> entries)
    {
        StatusGroups = Enum.GetValues<WatchStatus>().Select(status => new AnimeGroupViewModel(Text("Anime_Status_" + status, []),
            Query.Apply(entries, new(Status: status)).Count, new(status), Text, OpenGroup)).ToArray();
        StudioGroups = Query.GetStudioGroups(entries).Select(g => new AnimeGroupViewModel(g.Studio ?? Text("Browse_Unassigned", []),
            g.Count, g.Target, Text, OpenGroup)).ToArray();
        NoStudios = StudioGroups.Count == 0;
    }
}
