using System.Collections.ObjectModel;
using GalgameManager.Contracts.Services;
using GalgameManager.Enums;
using GalgameManager.Helpers;
using GalgameManager.Models;
using Microsoft.UI.Xaml.Controls;
using Newtonsoft.Json;

namespace GalgameManager.Services;

public class FaqService : IFaqService
{
    private const string JsonName = "FAQ.json";
    private DateTime _lastUpdateDateTime;
    private readonly TimeSpan _minDateTime = new(1, 0, 0, 0);
    private ObservableCollection<Faq> _faqs = new();
    private readonly ILocalSettingsService _localSettingsService;
    private readonly IInfoService _infoService;
    private bool _isInitialized;
    public bool IsUpdating { get; private set; }
    public event Action? UpdateStatusChangeEvent;

    public FaqService(ILocalSettingsService localSettingsService, IInfoService infoService)
    {
        _localSettingsService = localSettingsService;
        _infoService = infoService;
    }

    private async Task Init()
    {
        _lastUpdateDateTime = _localSettingsService.ReadSettingAsync<DateTime>(KeyValues.FaqLastUpdate).Result;
        // 从本地文件读取
        await LoadFaqs();
        _isInitialized = true;
    }

    public async Task<ObservableCollection<Faq>> GetFaqAsync(bool forceRefresh = false)
    {
        if (!_isInitialized)
            await Init();

        if (!forceRefresh && DateTime.Now - _lastUpdateDateTime < _minDateTime || IsUpdating)
            return _faqs;

        IsUpdating = true;
        UpdateStatusChangeEvent?.Invoke();
        var local = ResourceExtensions.GetLocal();
        await DownloadAndSaveFaqs($"https://raw.gitmirror.com/GoldenPotato137/GalgameManager/main/docs/FAQ/{local}.json");
        await LoadFaqs();
        _lastUpdateDateTime = DateTime.Now;
        await _localSettingsService.SaveSettingAsync(KeyValues.FaqLastUpdate, _lastUpdateDateTime);

        IsUpdating = false;
        UpdateStatusChangeEvent?.Invoke();
        return _faqs;
    }

    private async Task DownloadAndSaveFaqs(string? jsonUrl)
    {
        if (jsonUrl == null) return;
        HttpClient httpClient = Utils.GetDefaultHttpClient();
        try
        {
            HttpResponseMessage response = await httpClient.GetAsync(jsonUrl);
            response.EnsureSuccessStatusCode();
            var data = await response.Content.ReadAsByteArrayAsync();
            var targetPath = Path.Combine(_localSettingsService.LocalFolder.FullName, JsonName);
            Directory.CreateDirectory(_localSettingsService.LocalFolder.FullName);
            await File.WriteAllBytesAsync(targetPath, data);
        }
        catch (Exception e)
        {
            _infoService.Event(EventType.FaqEvent, InfoBarSeverity.Error, "FaqService_DownloadError".GetLocalized(), e);
        }
    }

    private async Task LoadFaqs()
    {
        try
        {
            var targetPath = Path.Combine(_localSettingsService.LocalFolder.FullName, JsonName);
            if (File.Exists(targetPath))
            {
                var json = await File.ReadAllTextAsync(targetPath);
                _faqs.Clear();
                _faqs = JsonConvert.DeserializeObject<ObservableCollection<Faq>>(json) ??
                        new ObservableCollection<Faq>();
            }
        }
        catch (Exception e)
        {
            _infoService.Event(EventType.FaqEvent, InfoBarSeverity.Error, "FaqService_LoadError".GetLocalized(), e);
        }
    }
}
