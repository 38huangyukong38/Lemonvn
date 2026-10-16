#pragma warning disable MVVMTK0049 //警告INotifyPropertyChanged无法NativeAOT，等后续处理

using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GalgameManager.Contracts.Services;
using GalgameManager.Helpers;
using GalgameManager.Models;
using GalgameManager.Models.Sources;
using GalgameManager.MultiStreamPage.Lists;
using GalgameManager.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace GalgameManager.Views.Dialog
{   
    [INotifyPropertyChanged]
    public partial class MultiStreamPageSettingDialog
    {
        public ObservableCollection<IList> Result { get; } = new();
        public List<MultiStreamPageSortKeys> GameListKeys { get; } = new()
        {
            MultiStreamPageSortKeys.LastPlayed, 
            MultiStreamPageSortKeys.ReleaseDate,
        };
        public List<MultiStreamPageSortKeys> CategoryListKeys { get; } = new()
        {
            MultiStreamPageSortKeys.LastPlayed, 
            MultiStreamPageSortKeys.LastClicked,
        };
        public List<MultiStreamPageSortKeys> SourceListKeys { get; } = new()
        {
            MultiStreamPageSortKeys.LastPlayed, 
            MultiStreamPageSortKeys.LastClicked,
        };
        
        public List<Category> Categories { get; } = new();
        public List<CategoryGroup> CategoryGroups { get; } = new();
        public List<GalgameSourceBase> Sources { get; } = new();
        [ObservableProperty] private bool _addGameListSourceAble;
        [ObservableProperty] private bool _addSourceListSubSourceAble;
            
        private readonly ICategoryService _categoryService = App.GetService<ICategoryService>();
        private readonly IGalgameSourceCollectionService _sourceCollectionService =
            App.GetService<IGalgameSourceCollectionService>();

        public MultiStreamPageSettingDialog(ObservableCollection<IList> lists)
        {
            InitializeComponent();
            RequestedTheme = App.MainWindow?.Content is Microsoft.UI.Xaml.FrameworkElement element ? element.RequestedTheme : RequestedTheme;

            XamlRoot = App.MainWindow!.Content.XamlRoot;
            PrimaryButtonText = "ConfirmLiteral".GetLocalized();
            SecondaryButtonText = "Cancel".GetLocalized();
            Loaded += async (_, _) =>
            {
                foreach (CategoryGroup group in await _categoryService.GetCategoryGroupsAsync())
                    Categories.AddRange(group.Categories);
                CategoryGroups.AddRange(await _categoryService.GetCategoryGroupsAsync());
                Sources.AddRange(_sourceCollectionService.GetGalgameSources());
                AddGameListSourceAble = Sources.Count > 0;
                AddSourceListSubSourceAble = Sources.Count > 0;
            };
            
            foreach (IList list in lists)
                Result.Add(list);
        }

        private void Delete(object sender, RoutedEventArgs e)
        {
            if (sender is Button { DataContext: IList item })
                Result.Remove(item);
        }

        [RelayCommand]
        private void Reset()
        {
            List<IList> tmp = MultiStreamViewModel.GetInitList();
            Result.Clear();
            Result.SyncCollection(tmp);
        }

        [RelayCommand]
        private void AddGameListFull()
        {
            Result.Add(new GameList("MultiStreamPageSettingDialog_NewGameList".GetLocalized(),
                MultiStreamPageSortKeys.LastPlayed));
        }

        [RelayCommand]
        private void AddGameListCategory()
        {
            if (Categories.Count == 0) return; //不应该
            Result.Add(new GameList("MultiStreamPageSettingDialog_NewGameList".GetLocalized(),
                MultiStreamPageSortKeys.LastPlayed, category: Categories[0]));
        }
        
        [RelayCommand]
        private void AddGameListSource()
        {
            if (Sources.Count == 0) return;
            Result.Add(new GameList("MultiStreamPageSettingDialog_NewGameList".GetLocalized(),
                MultiStreamPageSortKeys.LastPlayed, source: Sources[0]));
        }
        
        [RelayCommand]
        private void AddCategoryList()
        {
            Result.Add(new CategoryList(_categoryService.StatusGroup)
                { Title = "MultiStreamPageSettingDialog_NewCategoryList".GetLocalized() });
        }
        
        [RelayCommand]
        private void AddSourceListFull()
        {
            Result.Add(new SourceList {Title = "MultiStreamPageSettingDialog_NewSourceList".GetLocalized()});
        }

        [RelayCommand]
        private void AddSourceListSubSource()
        {
            if (Sources.Count == 0) return;
            Result.Add(new SourceList(Sources[0])
                { Title = "MultiStreamPageSettingDialog_NewSourceList".GetLocalized() });
        }
    }
}

#pragma warning restore MVVMTK0049

namespace MultiStreamPageSettingDialog
{
    public class TemplateSelector : DataTemplateSelector
    {
        public DataTemplate GameListTemplate { get; set; } = null!;
        public DataTemplate CategoryListTemplate { get; set; } = null!;
        public DataTemplate SourceListTemplate { get; set; } = null!;
        
        protected override DataTemplate SelectTemplateCore(object item, DependencyObject container)
        {
            if (item is GameList)
                return GameListTemplate;
            if (item is CategoryList)
                return CategoryListTemplate;
            if (item is SourceList)
                return SourceListTemplate;
            return base.SelectTemplateCore(item, container);
        }
    }
}