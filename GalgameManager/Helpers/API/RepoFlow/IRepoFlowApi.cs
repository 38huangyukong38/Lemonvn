using Refit;

namespace GalgameManager.Helpers.API.RepoFlow;

public interface IRepoFlowApi
{
    [Get("/{workspaceId}/repositories")]
    Task<List<Repository>> GetRepositoriesAsync(string workspaceId);
    
    [Get("/package/{workspaceName}/{repositoryName}/{packageName}")]
    Task<PackageMeta> GetPackageMetaAsync(string workspaceName, string repositoryName, string packageName);

    [Get("/package/{workspaceName}/{repositoryName}/{packageName}/versions")]
    Task<List<PackageDetailVersion>> GetPackageDetailVersionAsync(string workspaceName, string repositoryName,
        string packageName);
}
