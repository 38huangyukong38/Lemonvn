using GalgameManager.Models;

namespace GalgameManager.Contracts.Services;

public interface ISourceScanResultService
{
    void SaveScanResult(GalgameScanResult scanResult);
 
    GalgameScanResult? GetScanResult(Guid id);
    
    List<GalgameScanResult> GetAllScanResults();
    
    void DeleteScanResult(Guid id);
}