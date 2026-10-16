using System.Collections.ObjectModel;
using GalgameManager.Contracts.Services;
using GalgameManager.Enums;
using GalgameManager.Helpers;
using GalgameManager.Models;
using GalgameManager.Models.Filters;
using GalgameManager.WinApp.Base.Models.Filters;

namespace GalgameManager.Services;

public class FilterService : IFilterService
{
    private const string TagFilterType = "Tag";
    private const string CategoryFilterType = "Category";
    private const string SourceFilterType = "Source";
    private const string StaffFilterType = "Staff";

    public ObservableCollection<FilterBase> Filters;
    public event Action? OnFilterChanged;
    private readonly ILocalSettingsService _localSettingsService;
    private readonly SemaphoreSlim _persistLock = new(1, 1);
    private bool _isInitializing;

    public FilterService(ILocalSettingsService localSettingsService)
    {
        _localSettingsService = localSettingsService;
        _localSettingsService.OnSettingChanged += async (key, _) => await OnSettingChangedAsync(key);
        Filters = new ObservableCollection<FilterBase>();
    }

    public async Task InitAsync()
    {
        _isInitializing = true;
        try
        {
            Filters.Clear();
            await RestoreFiltersAsync();
            await SetFiltersAsync();
        }
        finally
        {
            _isInitializing = false;
        }

        await PersistFiltersAsync();
        OnFilterChanged?.Invoke();
    }

    public ObservableCollection<FilterBase> GetFilters() => Filters;

    public bool ApplyFilters(Galgame galgame)
    {
        return Filters.All(filter => filter.Revert ^ filter.Apply(galgame));
    }

    public void AddFilter(FilterBase filter)
    {
        if (FindFilter(filter) is not null) return;
        Filters.Add(filter);
        if (filter is VirtualGameFilter)
            _localSettingsService.SaveSettingAsync(KeyValues.DisplayVirtualGame, false);
        NotifyFiltersChanged();
    }

    public void RemoveFilter(FilterBase filter)
    {
        FilterBase? existingFilter = FindFilter(filter);
        if (existingFilter is null) return;
        Filters.Remove(existingFilter);
        if (existingFilter is VirtualGameFilter)
            _localSettingsService.SaveSettingAsync(KeyValues.DisplayVirtualGame, true);
        NotifyFiltersChanged();
    }

    public void ClearFilters()
    {
        List<FilterBase> toRemove = Filters.Where(filter => filter is not VirtualGameFilter).ToList();
        foreach (FilterBase filter in toRemove)
            Filters.Remove(filter);
        NotifyFiltersChanged();
    }

    public void SetFilter(FilterBase filter) => NotifyFiltersChanged();

    private async Task OnSettingChangedAsync(string key)
    {
        if (key == KeyValues.DisplayVirtualGame)
            await SetFiltersAsync();
        else if (key == KeyValues.KeepFilters)
            await HandleKeepFiltersChangedAsync();
    }

    private async Task SetFiltersAsync()
    {
        if (await _localSettingsService.ReadSettingAsync<bool>(KeyValues.DisplayVirtualGame))
            RemoveFilter(typeof(VirtualGameFilter));
        else
            AddFilter(typeof(VirtualGameFilter));
    }

    private void AddFilter(Type type)
    {
        if (Filters.Any(filter => filter.GetType() == type)) return;
        Filters.Add((Activator.CreateInstance(type) as FilterBase)!);
        NotifyFiltersChanged();
    }

    private void RemoveFilter(Type type)
    {
        if (Filters.Any(filter => filter.GetType() == type) == false) return;
        Filters.Remove(Filters.First(filter => filter.GetType() == type));
        NotifyFiltersChanged();
    }

    public async Task<List<FilterBase>> SearchFilters(string str)
    {
        List<FilterBase> result = new();
        if (str.Contains('/'))
            str = str[..(str.LastIndexOf('/') - 1)];
        await Task.Run((async Task() =>
        {
            IList<Galgame> games = (App.GetService<IGalgameCollectionService>() as GalgameCollectionService)!.Galgames;
            IEnumerable<CategoryGroup> categoryGroups = await App.GetService<ICategoryService>().GetCategoryGroupsAsync();
            //Category
            HashSet<string> addedCategories = new();
            result.AddRange(from categoryGroup in categoryGroups
                from category in categoryGroup.Categories
                where category.Name.ContainX(str)
                where addedCategories.Add(category.Name)
                select new CategoryFilter(category));
            result.RemoveAll(filter => Filters.Any(f => f is CategoryFilter && f.Name == filter.Name));
            //Tags
            HashSet<string> addedTags = new();
            result.AddRange(from game in games
                from tag in game.Tags.Value ?? new ObservableCollection<string>()
                where tag.ContainX(str)
                where addedTags.Add(tag)
                select new TagFilter(tag));
            result.RemoveAll(filter => Filters.Any(f => f is TagFilter && f.Name == filter.Name));
            //本地游戏
            if(Filters.Any(f => f is VirtualGameFilter) == false)
                result.Add(new VirtualGameFilter());
        })!);
        return result;
    }

    private void NotifyFiltersChanged()
    {
        OnFilterChanged?.Invoke();
        if (_isInitializing) return;
        _ = PersistFiltersAsync();
    }

    private FilterBase? FindFilter(FilterBase target)
        => Filters.FirstOrDefault(filter => IsSameFilter(filter, target));

    private static bool IsSameFilter(FilterBase left, FilterBase right)
    {
        if (left.GetType() != right.GetType()) return false;

        return left switch
        {
            TagFilter => left.Name == right.Name,
            CategoryFilter leftCategory when right is CategoryFilter rightCategory
                => leftCategory.Category.Id == rightCategory.Category.Id || left.Name == right.Name,
            SourceFilter leftSource when right is SourceFilter rightSource
                => leftSource.Source.Id == rightSource.Source.Id || leftSource.Source.Url == rightSource.Source.Url,
            StaffFilter => left.Name == right.Name,
            VirtualGameFilter => true,
            _ => left.Name == right.Name,
        };
    }

    private async Task HandleKeepFiltersChangedAsync()
    {
        if (await _localSettingsService.ReadSettingAsync<bool>(KeyValues.KeepFilters))
            await PersistFiltersAsync();
        else
            await _localSettingsService.RemoveSettingAsync(KeyValues.Filters);
    }

    private async Task RestoreFiltersAsync()
    {
        if (await _localSettingsService.ReadSettingAsync<bool>(KeyValues.KeepFilters) == false)
            return;

        List<PersistedFilter> persistedFilters =
            await _localSettingsService.ReadSettingAsync<List<PersistedFilter>>(KeyValues.Filters) ?? [];
        foreach (PersistedFilter persistedFilter in persistedFilters)
        {
            FilterBase? filter = CreateFilter(persistedFilter);
            if (filter is null || FindFilter(filter) is not null) continue;
            filter.Revert = persistedFilter.Revert;
            Filters.Add(filter);
        }
    }

    private async Task PersistFiltersAsync()
    {
        await _persistLock.WaitAsync();
        try
        {
            if (!await _localSettingsService.ReadSettingAsync<bool>(KeyValues.KeepFilters))
            {
                await _localSettingsService.RemoveSettingAsync(KeyValues.Filters);
                return;
            }

            List<PersistedFilter> persistedFilters = Filters
                .Where(filter => filter is not VirtualGameFilter)
                .Select(CreatePersistedFilter)
                .Where(filter => filter is not null)
                .Cast<PersistedFilter>()
                .ToList();
            await _localSettingsService.SaveSettingAsync(KeyValues.Filters, persistedFilters);
        }
        finally
        {
            _persistLock.Release();
        }
    }

    private static PersistedFilter? CreatePersistedFilter(FilterBase filter)
    {
        return filter switch
        {
            TagFilter => new PersistedFilter
            {
                Type = TagFilterType,
                Name = filter.Name,
                Revert = filter.Revert,
            },
            CategoryFilter categoryFilter => new PersistedFilter
            {
                Type = CategoryFilterType,
                Name = categoryFilter.Name,
                Id = categoryFilter.Category.Id,
                Revert = filter.Revert,
            },
            SourceFilter sourceFilter => new PersistedFilter
            {
                Type = SourceFilterType,
                Name = sourceFilter.Name,
                Id = sourceFilter.Source.Id,
                Extra = sourceFilter.Source.Url,
                Revert = filter.Revert,
            },
            StaffFilter => new PersistedFilter
            {
                Type = StaffFilterType,
                Name = filter.Name,
                Revert = filter.Revert,
            },
            _ => null,
        };
    }

    private static FilterBase? CreateFilter(PersistedFilter persistedFilter)
    {
        return persistedFilter.Type switch
        {
            TagFilterType when string.IsNullOrWhiteSpace(persistedFilter.Name) == false
                => new TagFilter(persistedFilter.Name),
            CategoryFilterType => CreateCategoryFilter(persistedFilter),
            SourceFilterType => CreateSourceFilter(persistedFilter),
            StaffFilterType => CreateStaffFilter(persistedFilter),
            _ => null,
        };
    }

    private static FilterBase? CreateCategoryFilter(PersistedFilter persistedFilter)
    {
        ICategoryService categoryService = App.GetService<ICategoryService>();
        Category? category = persistedFilter.Id is null
            ? null
            : categoryService.GetCategory(persistedFilter.Id.Value);
        category ??= persistedFilter.Name is null ? null : categoryService.GetCategory(persistedFilter.Name);
        return category is null ? null : new CategoryFilter(category);
    }

    private static FilterBase? CreateSourceFilter(PersistedFilter persistedFilter)
    {
        IGalgameSourceCollectionService sourceService = App.GetService<IGalgameSourceCollectionService>();
        Models.Sources.GalgameSourceBase? source = persistedFilter.Id is null
            ? null
            : sourceService.GetGalgameSourceFromId(persistedFilter.Id.Value);
        source ??= persistedFilter.Extra is null ? null : sourceService.GetGalgameSourceFromUrl(persistedFilter.Extra);
        return source is null ? null : new SourceFilter(source);
    }

    private static FilterBase? CreateStaffFilter(PersistedFilter persistedFilter)
    {
        if (string.IsNullOrWhiteSpace(persistedFilter.Name)) return null;

        IStaffService staffService = App.GetService<IStaffService>();
        Staff? staff = staffService.GetStaff(persistedFilter.Id)
                       ?? staffService.GetStaffs().FirstOrDefault(s => s.Name == persistedFilter.Name);
        return staff is null ? null : new StaffFilter(staff);
    }

    private sealed class PersistedFilter
    {
        public string Type { get; set; } = string.Empty;
        public string? Name { get; set; }
        public Guid? Id { get; set; }
        public string? Extra { get; set; }
        public bool Revert { get; set; }
    }
}
