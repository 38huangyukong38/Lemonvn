using System.Collections.Concurrent;
using System.Security.Cryptography;
using GalFingerPrint.Client.Api;
using GalFingerPrint.Client.Client;
using GalFingerPrint.Client.Model;
using GalgameManager.Contracts.Services;
using GalgameManager.Enums;
using GalgameManager.Helpers;
using GalgameManager.Models.Sources;
using Microsoft.UI.Xaml.Controls;

namespace GalgameManager.Models.BgTasks;

public class FingerprintUploadTask : QueueTaskBase<Galgame>
{
    private readonly ILocalSettingsService _settingsService = App.GetService<ILocalSettingsService>();
    private readonly IGalgameSourceCollectionService _sourceService = App.GetService<IGalgameSourceCollectionService>();
    private readonly IInfoService _infoService = App.GetService<IInfoService>();
    private readonly SemaphoreSlim _saveLock = new(1, 1);
    private readonly HashSet<Guid> _enqueued = new();
    private ConcurrentDictionary<string, bool> _uploadedStatus = new();
    private readonly HttpClient _httpClient = Utils.GetDefaultHttpClient();

    protected async override Task InitializeAsync()
    {
        Dictionary<string, bool> stored = await _settingsService
            .ReadSettingAsync<Dictionary<string, bool>>(KeyValues.FingerprintUploadedMap, isLarge: true) ?? [];
        _uploadedStatus = new ConcurrentDictionary<string, bool>(stored);

        foreach (GalgameSourceBase source in _sourceService.GetGalgameSources()
                     .Where(s => s.SourceType == GalgameSourceType.LocalFolder))
        {
            foreach (Galgame galgame in source.GetGalgameList())
            {
                if (!ShouldProcess(galgame)) continue;
                if (_enqueued.Add(galgame.Uuid)) Queue.Enqueue(galgame);
            }
        }

        UpdateProgressMsg();
        return;

        bool ShouldProcess(Galgame game)
        {
            if (string.IsNullOrWhiteSpace(game.Ids[(int)RssType.Vndb])) return false;
            return game.LocalInstallations
                .Where(e => e.Source?.SourceType == GalgameSourceType.LocalFolder)
                .Any(e => !IsUploaded(game, e));
        }
    }
    
    public override string Title => "FingerprintUploadTask_Title".GetLocalized();

    protected async override Task ProcessItemAsync(Galgame item)
    {
        try
        {
            foreach (GalgameAndPath installation in item.LocalInstallations
                         .Where(e => e.Source?.SourceType == GalgameSourceType.LocalFolder && !IsUploaded(item, e)))
            {
                string path = installation.Path;
                if (!Directory.Exists(path)) continue;
                List<string> executables;
                try
                {
                    executables = Directory.EnumerateFiles(path, "*.exe", SearchOption.AllDirectories).ToList();
                }
                catch (Exception e)
                {
                    _infoService.DeveloperEvent(InfoBarSeverity.Warning,
                        "FingerprintUploadTask_EnumerateFailed".GetLocalized(path, item.Name.Value ?? string.Empty), e);
                    continue;
                }

                List<(string RelativePath, string Hash)> fingerprints = new();
                foreach (string exePath in executables)
                {
                    try
                    {
                        string hash = await ComputeSha256Async(exePath);
                        string relativePath = Path.GetRelativePath(path, exePath);
                        fingerprints.Add((relativePath, hash));
                    }
                    catch (Exception e)
                    {
                        _infoService.DeveloperEvent(InfoBarSeverity.Warning,
                            "FingerprintUploadTask_HashFailed".GetLocalized(exePath, item.Name.Value ?? string.Empty), e);
                    }
                }

                if (fingerprints.Count == 0) continue;
                await UploadFingerprintsAsync(item, fingerprints);
                await MarkUploadedAsync(item, installation);
            }
        }
        catch (Exception e)
        {
            _infoService.DeveloperEvent(InfoBarSeverity.Warning,
                "FingerprintUploadTask_GameFailed".GetLocalized(item.Name.Value ?? string.Empty), e);
        }
        return;

        async Task MarkUploadedAsync(Galgame game, GalgameAndPath installation)
        {
            string key = GetUploadKey(game, installation);
            _uploadedStatus[key] = true;
            await _saveLock.WaitAsync();
            try
            {
                Dictionary<string, bool> snapshot = _uploadedStatus.ToDictionary(pair => pair.Key, pair => pair.Value);
                await _settingsService.SaveSettingAsync(KeyValues.FingerprintUploadedMap, snapshot, isLarge: true);
            }
            finally
            {
                _saveLock.Release();
            }
        }
        
        static async Task<string> ComputeSha256Async(string filePath)
        {
            await using FileStream stream = File.OpenRead(filePath);
            using SHA256 sha = SHA256.Create();
            var hash = await sha.ComputeHashAsync(stream);
            return Convert.ToHexString(hash).ToLowerInvariant();
        }
        
        Task UploadFingerprintsAsync(Galgame game, List<(string RelativePath, string Hash)> fingerprints)
        {
            if (string.IsNullOrEmpty(game.Ids[(int)RssType.Vndb])) throw new PvnException("Vndb ID is missing."); //不应该发生
            VoteApi api = new(_httpClient, new Configuration{BasePath = "https://dinzhen.api.potatovn.net"});
            return api.VoteVndbIdPatchAsync(game.Ids[(int)RssType.Vndb]!,
                new VotePatchRequest(fingerprints.Select(f => f.Hash).ToList()));
        }
    }

    private bool IsUploaded(Galgame game, GalgameAndPath installation)
    {
        if (_uploadedStatus.TryGetValue(GetUploadKey(game, installation), out bool done)) return done;
        return _uploadedStatus.TryGetValue(game.Uuid.ToString(), out bool legacyDone) && legacyDone;
    }

    private static string GetUploadKey(Galgame game, GalgameAndPath installation) =>
        $"{game.Uuid}:{installation.EntryId}";
    
    protected override string ProgressTitle() => "FingerprintUploadTask_Progress_Title";

    protected override string ProgressMsg(Galgame item) => $"{item.Name.Value}";

    protected override string ProgressWaitingMsg() => "FingerprintUploadTask_Progress_Waiting";
}
