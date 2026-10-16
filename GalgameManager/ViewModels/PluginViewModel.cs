using System.Collections.ObjectModel;
using System.Collections.Specialized;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GalgameManager.Contracts.Services;
using GalgameManager.Contracts.ViewModels;
using GalgameManager.Helpers;
using GalgameManager.Models;
using GalgameManager.Views.Dialog;
using GalgameManager.WinApp.Base.Contracts.PluginUi;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Storage;
using Windows.Storage.Pickers;
using GalgameManager.Enums;
using SharpCompress.Archives;
using SharpCompress.Common;
using SharpCompress.Readers;

namespace GalgameManager.ViewModels;

public partial class PluginViewModel(IPluginService pluginService, IInfoService infoService,
    INavigationService navService, ILocalSettingsService settingsService)
    : ObservableRecipient, INavigationAware
{
    [ObservableProperty] private bool _isDevMode;
    public ObservableCollection<PluginSettingViewModel> Plugins = [];
    private ObservableCollection<PluginX> _plugins = null!;

    public static event Action<Guid>? DevPluginHotReloaded;

    public static void NotifyDevPluginHotReloaded(Guid pluginId)
    {
        DevPluginHotReloaded?.Invoke(pluginId);
    }

    public async void OnNavigatedTo(object parameter)
    {
        try
        {
            _plugins = await pluginService.GetAllPluginsAsync();
            PluginsOnCollectionChanged(null!, null!);
            _plugins.CollectionChanged += PluginsOnCollectionChanged;
            IsDevMode = await settingsService.ReadSettingAsync<bool>(KeyValues.DevelopmentMode);
        }
        catch (Exception)
        {
            //ignore
        }
    }

    public void OnNavigatedFrom()
    {
        _plugins.CollectionChanged -= PluginsOnCollectionChanged;
    }

    [RelayCommand]
    private async Task AddPluginFromDev()
    {
        try
        {
            FolderPicker folderPicker = new();
            folderPicker.FileTypeFilter.Add("*");
            WinRT.Interop.InitializeWithWindow.Initialize(folderPicker, App.MainWindow!.GetWindowHandle());
            StorageFolder? folder = await folderPicker.PickSingleFolderAsync();
            if (folder is null) return;
            await pluginService.AddPluginAsync(folder.Path, true);
        }
        catch (Exception e)
        {
            infoService.Info(InfoBarSeverity.Error, "PluginPage_Add_Failed".GetLocalized(),
                e is PvnException pvnE ? pvnE.FullMsg : e.ToString());
        }
    }

    [RelayCommand]
    private async Task AddPluginFromLocalZip()
    {
        try
        {
            PvnFilePicker filePicker = new()
            {
                AllowMultiSelect = false,
                Filters =
                [
                    new PvnFilePicker.Filter
                    {
                        Name = "Zip",
                        Pattern = "*.zip",
                    },
                ],
            };
            PickerResult result = filePicker.ShowDialog(App.MainWindow!.GetWindowHandle());
            if (result != PickerResult.OK || filePicker.SelectedPath is null) return;

            var folderName = FileHelper.RemoveInvalidFileNameChars(Path.GetFileNameWithoutExtension(filePicker.SelectedPath));
            var pluginFolderPath = await GetPluginFolderPathAsync(folderName);
            if (Directory.Exists(pluginFolderPath)) Directory.Delete(pluginFolderPath, true);
            Directory.CreateDirectory(pluginFolderPath);

            try
            {
                await ExtractPluginAsync(filePicker.SelectedPath, pluginFolderPath);
                await pluginService.AddPluginAsync(pluginFolderPath, await settingsService.ReadSettingAsync<bool>(KeyValues.DevelopmentMode));
            }
            catch
            {
                try
                {
                    if (Directory.Exists(pluginFolderPath))
                        Directory.Delete(pluginFolderPath, true);
                }
                catch
                {
                    // ignore
                }
                throw;
            }
        }
        catch (Exception e)
        {
            infoService.Info(InfoBarSeverity.Error, "PluginPage_Add_Failed".GetLocalized(),
                e is PvnException pvnE ? pvnE.FullMsg : e.ToString());
        }
        return;

        async Task<string> GetPluginFolderPathAsync(string folderName)
        {
            if (string.IsNullOrWhiteSpace(folderName))
                folderName = Guid.NewGuid().ToString();
            ObservableCollection<PluginX> plugins = await pluginService.GetAllPluginsAsync();
            for (var i = 0; ; i++)
            {
                var currentFolderName = i == 0 ? folderName : $"{folderName}_{i}";
                var path = Path.Combine(pluginService.PluginDir.FullName, currentFolderName);
                if (plugins.All(p => !Utils.ArePathsEqual(path, p.Path)))
                    return path;
            }
        }

        static Task ExtractPluginAsync(string zipPath, string targetDirectory)
        {
            return Task.Run(() =>
            {
                using IArchive archive = ArchiveFactory.OpenArchive(zipPath, ReaderOptions.ForFilePath);
                archive.WriteToDirectory(targetDirectory, new ExtractionOptions
                {
                    ExtractFullPath = true,
                    Overwrite = true,
                });
            });
        }
    }

    [RelayCommand]
    private void AddPluginFromStore()
    {
        navService.NavigateTo(typeof(PluginStoreViewModel).FullName!);
    }

    private async void PluginsOnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        try
        {
            if (e is null || e.Action == NotifyCollectionChangedAction.Reset)
            {
                await ReloadPlugins();
                return;
            }

            switch (e.Action)
            {
                case NotifyCollectionChangedAction.Add when e.NewItems is not null:
                    foreach (PluginX newItem in e.NewItems)
                    {
                        PluginSettingViewModel newVm = new(newItem, pluginService);
                        var index = 0;
                        while (index < Plugins.Count && Plugins[index].Plugin.CompareTo(newItem) < 0) index++;
                        Plugins.Insert(index, newVm);
                    }
                    break;

                case NotifyCollectionChangedAction.Remove when e.OldItems is not null:
                    foreach (PluginX oldItem in e.OldItems)
                    {
                        PluginSettingViewModel? vm = Plugins.FirstOrDefault(p => p.Plugin == oldItem);
                        if (vm is not null) Plugins.Remove(vm);
                    }
                    break;

                default:
                    await ReloadPlugins();
                    break;
            }
        }
        catch (Exception ex)
        {
            infoService.DeveloperEvent(e: ex);
        }
    }

    private async Task ReloadPlugins()
    {
        Plugins.Clear();
        ObservableCollection<PluginX> sourcePlugins = await pluginService.GetAllPluginsAsync();
        List<PluginX> sorted = [.. sourcePlugins];
        sorted.Sort();
        foreach (PluginX plugin in sorted)
            Plugins.Add(new PluginSettingViewModel(plugin, pluginService));
    }
}

public partial class PluginSettingViewModel(PluginX plugin, IPluginService pluginService) : ObservableRecipient
{
    public PluginX Plugin { get; } = plugin;
    [ObservableProperty] private FrameworkElement? _ui;
    [ObservableProperty] private bool _isExpanded;

    static PluginSettingViewModel()
    {
        PluginViewModel.DevPluginHotReloaded += pluginId =>
        {
            foreach (PluginSettingViewModel vm in App.GetService<PluginViewModel>().Plugins)
            {
                if (vm.Plugin.Id != pluginId) continue;
                vm.Ui = null;
                vm.IsExpanded = false;
            }
        };
    }

    partial void OnIsExpandedChanged(bool value)
    {
        if (!value) return;
        if (Ui != null) return;
        // ReSharper disable once SuspiciousTypeConversion.Global
        if (Plugin.Plugin is not IPluginSetting setting) return;
        try
        {
            FrameworkElement pluginUi = PluginInvokeHelper.Invoke(Plugin.Info, setting.CreateSettingUi,
                App.GetService<IInfoService>())!;
            if (pluginUi is null) return;
            Ui = pluginUi;
        }
        catch (Exception ex)
        {
            App.GetService<IPluginService>().ThrowPluginExceptionEvent(Plugin, ex,
                "PluginSettingViewModel_CreateUiFailed".GetLocalized());
        }
    }

    [RelayCommand]
    private async Task DeletePlugin()
    {
        BasicDialog dialog = new("PluginPage_DeleteDialog_Title".GetLocalized(),
            checkBoxText: "PluginPage_DeleteDialog_DeleteData".GetLocalized(),
            primaryButton: "PluginPage_DeleteDialog_Yes".GetLocalized());
        await dialog.ShowAsync();
        if (!dialog.PrimaryButtonClicked) return;
        await pluginService.DeletePluginAsync(Plugin, dialog.CheckBoxChecked);
    }

    [RelayCommand]
    private async Task HotReloadDevPlugin()
    {
        BasicDialog dialog = new("PluginPage_HotReloadDialog_Title".GetLocalized(),
            checkBoxText: "PluginPage_HotReloadDialog_DeleteData".GetLocalized(),
            primaryButton: "PluginPage_HotReloadDialog_Yes".GetLocalized());
        await dialog.ShowAsync();
        if (!dialog.PrimaryButtonClicked) return;
        var pluginPath = Plugin.Path;
        IsExpanded = false;
        Ui = null;
        await pluginService.DeletePluginAsync(Plugin, dialog.CheckBoxChecked);
        await pluginService.AddPluginAsync(pluginPath, true);
        PluginViewModel.NotifyDevPluginHotReloaded(Plugin.Id);
    }

}
