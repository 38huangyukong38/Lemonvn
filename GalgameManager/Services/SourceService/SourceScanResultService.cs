using GalgameManager.Contracts.Services;
using GalgameManager.Models;
using LiteDB;

namespace GalgameManager.Services;

public class SourceScanResultService(ILocalSettingsService localSettings) : ISourceScanResultService
{
    private ILiteCollection<GalgameScanResult>? _scanResultDbSet;

    private const string CollectionName = "scan_results";

    private void EnsureDbSetInitialized()
    {
        if (_scanResultDbSet is not null) return;
        _scanResultDbSet = localSettings.Database.GetCollection<GalgameScanResult>(CollectionName);
    }

    public void SaveScanResult(GalgameScanResult scanResult)
    {
        EnsureDbSetInitialized();
        _scanResultDbSet!.Delete(scanResult.SourceId); //覆盖旧记录
        _scanResultDbSet!.Upsert(scanResult);
    }

    public GalgameScanResult? GetScanResult(Guid id)
    {
        EnsureDbSetInitialized();
        return _scanResultDbSet!.FindById(id);
    }

    public List<GalgameScanResult> GetAllScanResults()
    {
        EnsureDbSetInitialized();
        return _scanResultDbSet!.FindAll().ToList();
    }

    public void DeleteScanResult(Guid id)
    {
        EnsureDbSetInitialized();
        _scanResultDbSet!.Delete(id);
    }
}