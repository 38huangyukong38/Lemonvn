using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GalgameManager.Contracts.Services;
using GalgameManager.Contracts.ViewModels;
using GalgameManager.Core.Helpers;
using GalgameManager.Enums;
using GalgameManager.Helpers;
using GalgameManager.Models;
using GalgameManager.Services;
using GalgameManager.Views.Dialog;
using GalgameManager.WinApp.Base.Contracts.PluginUi;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace GalgameManager.ViewModels;

public partial class AccountViewModel : ObservableObject, INavigationAware
{
    public ObservableCollection<UIElement?> PluginUis = [];
    private readonly HashSet<Guid> _loadedPluginIds = [];
    private readonly ILocalSettingsService _localSettingsService;
    private readonly IBgmOAuthService _bgmService;
    private readonly IVndbAuthService _vndbAuthService;
    private readonly IInfoService _infoService;
    private readonly IGalgameCollectionService _gameService;
    private readonly IPluginService _pluginService;
    
    
    public AccountViewModel(ILocalSettingsService localSettingsService, IPvnService pvnService, 
        IBgmOAuthService bgmService, IVndbAuthService vndbAuthService, IInfoService infoService,
        IGalgameCollectionService gameService, IPluginService pluginService)
    {
        _localSettingsService = localSettingsService;
        _pvnService = pvnService;
        _bgmService = bgmService;
        _infoService = infoService;
        _vndbAuthService = vndbAuthService;
        _gameService = gameService;
        _pluginService = pluginService;
    }
    
    public async void OnNavigatedTo(object parameter)
    {
        _localSettingsService.OnSettingChanged += OnLocalSettingsChanged;
        _bgmService.OnAuthResultChange += BgmAuthResultNotify;
        _pvnService.StatusChanged += HandelPvnServiceStatusChanged;
        OnPropertyChanged(nameof(PvnServerTypeMsg));
#pragma warning disable MVVMTK0034 //防止触发属性更改事件，该事件是手动切换服务器类型时才应该触发的
        _pvnServerType = await _localSettingsService.ReadSettingAsync<PvnServerType>(KeyValues.PvnServerType);
#pragma warning restore MVVMTK0034
        PvnSyncGames = await _localSettingsService.ReadSettingAsync<bool>(KeyValues.SyncGames);
        PvnSyncCharacters = await _localSettingsService.ReadSettingAsync<bool>(KeyValues.SyncGameCharacters);
        PvnSyncStaffs = await _localSettingsService.ReadSettingAsync<bool>(KeyValues.SyncStaff);
        PvnSyncHeaderImages = await _localSettingsService.ReadSettingAsync<bool>(KeyValues.SyncHeaderImage);
        await UpdateAccountDisplay();
        await LoadPluginUi(); //加载插件UI
        return;
        
        async Task LoadPluginUi()
        {
            ObservableCollection<PluginX> plugins = await _pluginService.GetAllPluginsAsync();
            // ReSharper disable once SuspiciousTypeConversion.Global
            foreach (PluginX p in plugins)
            {
                if (p.Plugin is not IPluginAccount accPlugin) continue;
                if (_loadedPluginIds.Contains(p.Id)) continue;
                UIElement? ui = PluginInvokeHelper.Invoke(p.Info, accPlugin.CreateAccountUi, _infoService);
                if (ui is null) continue;
                PluginUis.Add(ui);
                _loadedPluginIds.Add(p.Id);
            }
        }
    }

    public void OnNavigatedFrom()
    {
        _localSettingsService.OnSettingChanged -= OnLocalSettingsChanged;
        _bgmService.OnAuthResultChange -= BgmAuthResultNotify;
        _pvnService.StatusChanged -= HandelPvnServiceStatusChanged;
    }

    private async void OnLocalSettingsChanged(string key, object? value)
    {
        switch (key)
        {
            case KeyValues.PvnAccount:
            case KeyValues.BangumiAccount:
            case KeyValues.VndbAccount:
                await UpdateAccountDisplay();
                break;
            case KeyValues.SyncGames:
                PvnSyncGames = value as bool? ?? false;
                break;
        }
    }
    
    private async Task UpdateAccountDisplay()
    {
        PvnAccount? account = await _localSettingsService.ReadSettingAsync<PvnAccount>(KeyValues.PvnAccount);
        BgmAccount? bgmAccount = await _localSettingsService.ReadSettingAsync<BgmAccount>(KeyValues.BangumiAccount);
        VndbAccount? vndbAccount = await _localSettingsService.ReadSettingAsync<VndbAccount>(KeyValues.VndbAccount);
        await UiThreadInvokeHelper.InvokeAsync(() =>
        {
            PvnAccount = account;
            BgmAccount = bgmAccount;
            VndbAccount = vndbAccount;
        });
    }

    #region POTATOVN_ACCOUNT

    private readonly IPvnService _pvnService;
    public string PvnDisplayName => PvnAccount?.UserDisplayName ?? "NoName".GetLocalized();
    public string PvnStateMsg => PvnAccount is null
        ? "AccountPage_Pvn_AccountStatus_Unlogin".GetLocalized()
        : "AccountPage_Pvn_LoginedDescription".GetLocalized(PvnAccount.Id,
            PvnAccount.ExpireTimestamp.ToDateTime().ToStringDefault(),
            PvnAccount.RefreshTimestamp.ToDateTime().ToStringDefault());
    public string? PvnAvatar => PvnAccount?.Avatar;
    // ReSharper disable once CollectionNeverQueried.Global
    public readonly PvnServerType[] PvnServerTypes = { PvnServerType.OfficialServer, PvnServerType.CustomServer };
    [ObservableProperty] private PvnServerType _pvnServerType;
    public string PvnServerTypeMsg => "AccountPage_Pvn_ServerType_Description".GetLocalized(_pvnService.BaseUri.ToString());
    public string PvnLoginButtonText => PvnAccount is null ? "Login".GetLocalized() : "Logout".GetLocalized();
    public string PvnLoginDescription => PvnAccount is null
        ? "AccountPage_Pvn_AccountStatus_Unlogin".GetLocalized()
        : "AccountPage_Pvn_AccountStatus_Login".GetLocalized(PvnAccount.Id, PvnAccount.LoginMethod.GetLocalized());
    public ICommand PvnLoginButtonCommand => PvnAccount is null ? new RelayCommand(PvnLogin) : new RelayCommand(PvnLogout);
    public bool IsPvnLogin => PvnAccount is not null;
    [ObservableProperty] private bool _pvnSyncGames;
    [ObservableProperty] private bool _pvnSyncCharacters;
    [ObservableProperty] private bool _pvnSyncStaffs;
    [ObservableProperty] private bool _pvnSyncHeaderImages;
    public string UsedSpace => $"{((double)(PvnAccount?.UsedSpace ?? 0) / 1024 / 1024)
        .ToString("F1", CultureInfo.InvariantCulture)} MB";
    public string TotalSpace => $"{((double)(PvnAccount?.TotalSpace ?? 0) / 1024 / 1024)
        .ToString("F1", CultureInfo.InvariantCulture)} MB";
    public string UsedPercent => "AccountPage_Pvn_SpaceUsedPercent".GetLocalized(UsedPercentValue
        .ToString("F1", CultureInfo.InvariantCulture));
    public double UsedPercentValue => (double)(PvnAccount?.UsedSpace ?? 0) / (PvnAccount?.TotalSpace ?? 1) * 100;
    
    [NotifyPropertyChangedFor(nameof(IsPvnLogin))]
    [NotifyPropertyChangedFor(nameof(PvnStateMsg))]
    [NotifyPropertyChangedFor(nameof(PvnDisplayName))]
    [NotifyPropertyChangedFor(nameof(PvnAvatar))]
    [NotifyPropertyChangedFor(nameof(PvnLoginDescription))]
    [NotifyPropertyChangedFor(nameof(PvnLoginButtonText))]
    [NotifyPropertyChangedFor(nameof(PvnLoginButtonCommand))]
    [NotifyPropertyChangedFor(nameof(UsedSpace))]
    [NotifyPropertyChangedFor(nameof(TotalSpace))]
    [NotifyPropertyChangedFor(nameof(UsedPercent))]
    [NotifyPropertyChangedFor(nameof(UsedPercentValue))]
    [ObservableProperty] private PvnAccount? _pvnAccount;
    
    async partial void OnPvnServerTypeChanged(PvnServerType value)
    {
        if (value == PvnServerType.CustomServer && await TrySetCustomServer() == false)
        {
            PvnServerType = PvnServerType.OfficialServer;
            return;
        }

        await _localSettingsService.SaveSettingAsync(KeyValues.PvnServerType, value);
        OnPropertyChanged(nameof(PvnServerTypeMsg));
    }

    private async Task<bool> TrySetCustomServer()
    {
        SelectPvnServerDialog dialog = new();
        await dialog.ShowAsync();
        if (string.IsNullOrEmpty(dialog.ServerUrl))
            return false;
        await _localSettingsService.SaveSettingAsync(KeyValues.PvnServerEndpoint, dialog.ServerUrl);
        return true;
    }
    
    private void HandelPvnServiceStatusChanged(PvnServiceStatus status)
    {
        _infoService.Info(InfoBarSeverity.Informational, msg:status.GetLocalized());
    }

    private async void PvnLogin()
    {
        try
        {
            Task<PvnServerInfo?> getAccountTask = _pvnService.GetServerInfoAsync();
            PvnLoginDialog dialog = new(getAccountTask,
                await _localSettingsService.ReadSettingAsync<string>(KeyValues.PvnAccountUserName));
            await dialog.ShowAsync();
            if (dialog.GetInfoTaskException is not null) throw dialog.GetInfoTaskException;

            if (dialog.Type != PvnLoginType.None)
                _infoService.Info(InfoBarSeverity.Informational, msg: "AccountPage_Pvn_Logging".GetLocalized());
            switch (dialog.Type)
            {
                case PvnLoginType.DefaultLogin:
                    await _localSettingsService.SaveSettingAsync(KeyValues.PvnAccountUserName, dialog.UserName!);
                    await _pvnService.LoginAsync(dialog.UserName!, dialog.Password!);
                    _infoService.Info(InfoBarSeverity.Success, msg: "AccountPage_Pvn_LoginSuccess".GetLocalized());
                    break;
                case PvnLoginType.DefaultRegister:
                    await _localSettingsService.SaveSettingAsync(KeyValues.PvnAccountUserName, dialog.UserName!);
                    await _pvnService.RegisterAsync(dialog.UserName!, dialog.Password!);
                    _infoService.Info(InfoBarSeverity.Success, msg: "AccountPage_Pvn_RegisterSuccess".GetLocalized());
                    break;
                case PvnLoginType.Bangumi:
                    await _pvnService.LoginViaBangumiAsync();
                    _infoService.Info(InfoBarSeverity.Success, msg: "AccountPage_Pvn_LoginSuccess".GetLocalized());
                    break;
            }
        }
        catch (Exception e)
        {
            _infoService.Info(InfoBarSeverity.Error, msg: e.Message);
        }
    }

    private async void PvnLogout()
    {
        await _pvnService.LogOutAsync();
        _infoService.Info(InfoBarSeverity.Success, msg: "AccountPage_LogoutSuccess".GetLocalized());
    }

    [RelayCommand]
    private async Task PvnSetAccount()
    {
        PvnSetAccountDialog dialog =
            new((await _localSettingsService.ReadSettingAsync<PvnAccount>(KeyValues.PvnAccount))!);
        await dialog.ShowAsync();
        if(dialog.Canceled) return;
        try
        {
            await _pvnService.ModifyAccountAsync(dialog.UserDisplayName, dialog.AvatarPath);
            _infoService.Info(InfoBarSeverity.Success, msg: "AccountPage_Pvn_Modified".GetLocalized());
        }
        catch (Exception e)
        {
            _infoService.Info(InfoBarSeverity.Error, msg: e.ToString());
        }
    }

    partial void OnPvnSyncGamesChanged(bool value)
    {
        _localSettingsService.SaveSettingAsync(KeyValues.SyncGames, value);
        if (value)
            _pvnService.SyncGames();
    }

    partial void OnPvnSyncCharactersChanged(bool value) =>
        _localSettingsService.SaveSettingAsync(KeyValues.SyncGameCharacters, value);

    partial void OnPvnSyncStaffsChanged(bool value) =>
        _localSettingsService.SaveSettingAsync(KeyValues.SyncStaff, value);

    partial void OnPvnSyncHeaderImagesChanged(bool value) =>
        _localSettingsService.SaveSettingAsync(KeyValues.SyncHeaderImage, value);

    [RelayCommand]
    private async Task PvnRefreshToken()
    {
        if (PvnAccount is null) return;
        if (DateTime.Now.ToUnixTime() > PvnAccount.ExpireTimestamp)
        {
            _infoService.Info(InfoBarSeverity.Error, msg: "AccountPage_Pvn_TokenExpired".GetLocalized());
            return;
        }
        
        try
        {
            _infoService.Info(InfoBarSeverity.Informational, msg: "AccountPage_Pvn_Logging".GetLocalized());
            await _pvnService.RefreshTokenAsync();
            _infoService.Info(InfoBarSeverity.Success, msg: "AccountPage_Pvn_RefreshSuccess".GetLocalized());
        }
        catch (Exception e)
        {
            _infoService.Info(InfoBarSeverity.Error, msg: e.ToString());
        }
    }

    [RelayCommand]
    private async Task PvnBatchUpload()
    {
        if (PvnAccount is null) return;
        try
        {
            PvnBatchUploadDialog dialog = new();
            await dialog.ShowAsync();
            if (dialog.Canceled || dialog.SelectedProperties == PvnUploadProperties.None) return;
            _infoService.Info(InfoBarSeverity.Informational, msg: "AccountPage_Pvn_BatchUploading".GetLocalized());
            var totalGames = _gameService.Galgames.Count;
            var uploadedCount = 0;
            foreach (Galgame galgame in _gameService.Galgames)
            {
                _pvnService.Upload(galgame, dialog.SelectedProperties);
                uploadedCount++;
            }
            _infoService.Info(InfoBarSeverity.Success, 
                msg: "AccountPage_Pvn_BatchUploadStarted".GetLocalized(uploadedCount, totalGames));
        }
        catch (Exception e)
        {
            _infoService.Info(InfoBarSeverity.Error, msg: e.Message);
        }
    }

    #endregion

    #region BANUGMI_ACCOUNT

    public string BgmName => BgmAccount?.Name ?? "NoName".GetLocalized();
#pragma warning disable MVVMTK0034 //不知道为什么使用生成的属性会导致图片无法显示，暂时关闭未使用生成字段的警告
    public string? BgmAvatar => _bgmAccount?.Avatar;
#pragma warning restore MVVMTK0034
    public string BgmDescription => BgmAccount is null
        ? "AccountPage_Bgm_NoLogin".GetLocalized()
        : "AccountPage_Bgm_LoginedDescription".GetLocalized(BgmAccount.UserId, BgmAccount.Expires.ToStringDefault(),
            BgmAccount.NextRefresh.ToStringDefault());
    public string BgmLoginBtnText => BgmAccount is null ? "Login".GetLocalized() : "Logout".GetLocalized();
    public ICommand BgmLoginBtnCommand => BgmAccount is null ? new RelayCommand(BgmLogin) : new RelayCommand(BgmLogout);
    public bool IsBgmLogin => BgmAccount is not null;
    
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(BgmName))]
    [NotifyPropertyChangedFor(nameof(BgmAvatar))]
    [NotifyPropertyChangedFor(nameof(BgmDescription))]
    [NotifyPropertyChangedFor(nameof(BgmLoginBtnText))]
    [NotifyPropertyChangedFor(nameof(BgmLoginBtnCommand))]
    [NotifyPropertyChangedFor(nameof(IsBgmLogin))]
    private BgmAccount? _bgmAccount;

    private void BgmAuthResultNotify(BgmOAuthStatus result)
    {
        switch (result)
        {
            case BgmOAuthStatus.Failed:
                _infoService.Info(InfoBarSeverity.Error); //失败走事件通知，关闭消息栏 
                break;
            case BgmOAuthStatus.Done:
                _infoService.Info(InfoBarSeverity.Success, msg: result.GetLocalized());
                break;
            default:
                _infoService.Info(InfoBarSeverity.Informational, msg: result.GetLocalized(), displayTimeMs: 1000 * 60);
                break;
        }
    }

    private async void BgmLogin()
    {
        BgmAuthDialog selectAuthModeDialog = new();
        ContentDialogResult result = await selectAuthModeDialog.ShowAsync();
        if (result != ContentDialogResult.Primary) return;
        switch (selectAuthModeDialog.SelectItem)
        {
            case 0:
                await _bgmService.StartOAuthAsync();
                break;
            case 1:
                if (!string.IsNullOrEmpty(selectAuthModeDialog.AccessToken)) 
                    await _bgmService.AuthWithAccessToken(selectAuthModeDialog.AccessToken);
                break;
        }
    }

    private async void BgmLogout()
    {
        await _bgmService.LogoutAsync();
        _infoService.Info(InfoBarSeverity.Success, msg: "AccountPage_LogoutSuccess".GetLocalized());
    }

    [RelayCommand]
    private async Task BgmRefreshToken()
    {
        _infoService.Info(InfoBarSeverity.Informational, msg: "AccountPage_Bgm_Refreshing".GetLocalized(),
            displayTimeMs: 1000 * 60);
        var result = await _bgmService.RefreshAccountAsync();
        if (result == false)
            _infoService.Info(InfoBarSeverity.Error); //失败走事件通知，关闭消息栏
    }
    
    #endregion

    #region VNDB_ACCOUNT
    
    public bool IsVndbLogin => VndbAccount is not null;

    public string VndbUsername => VndbAccount?.Username ?? "NoName".GetLocalized();

    
    public string VndbDescription => VndbAccount is null
        ? "AccountPage_Vndb_NoLogin".GetLocalized()
        : "AccountPage_Vndb_LoginedDescription".GetLocalized(
            VndbAccount.Id,
            string.Join(", ", VndbAccount.Permissions.Select(p=>p.GetLocalized()).ToList())
            );

    public string VndbLoginBtnText => VndbAccount is null ? "Login".GetLocalized() : "Logout".GetLocalized();

    public ICommand VndbLoginBtnCommand => VndbAccount is null ? new RelayCommand(VndbLogin) : new RelayCommand(VndbLogout);

    
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsVndbLogin))]
    [NotifyPropertyChangedFor(nameof(VndbUsername))]
    [NotifyPropertyChangedFor(nameof(VndbDescription))]
    [NotifyPropertyChangedFor(nameof(VndbLoginBtnText))]
    [NotifyPropertyChangedFor(nameof(VndbLoginBtnCommand))]
    private VndbAccount? _vndbAccount;
    
    private async void VndbLogin()
    {
        VndbAuthDialog vndbAuthDialog = new VndbAuthDialog();
        ContentDialogResult result = await vndbAuthDialog.ShowAsync();
        if (result != ContentDialogResult.Primary) return;
        if (vndbAuthDialog.Token.IsNullOrEmpty()) return;
        await _vndbAuthService.AuthWithToken(vndbAuthDialog.Token);
    }

    private async void VndbLogout()
    {
        await _vndbAuthService.LogoutAsync();
        _infoService.Info(InfoBarSeverity.Success, msg: "AccountPage_LogoutSuccess".GetLocalized());
    }

    #endregion
}
