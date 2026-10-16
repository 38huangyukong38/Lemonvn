using GalgameManager.Models;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Refit;

namespace GalgameManager.Helpers.API.RepoFlow;

public class RepoFlowApi
{
    public static string WorkspaceId { get;} 
    public static string WorkspaceName { get; }
    public static string ApiUrl { get;}

    static RepoFlowApi()
    {
        IConfiguration config = App.GetService<IConfiguration>();
        ApiUrl = config["PluginOptions:PluginServer"] ?? throw new PvnException("未配置PluginServer地址"); //不应该发生
        WorkspaceId = config["PluginOptions:PluginWorkspaceId"] ?? throw new PvnException("未配置PluginWorkspaceId"); //不应该发生
        WorkspaceName = config["PluginOptions:PluginWorkspaceName"] ?? throw new PvnException("未配置PluginWorkspaceName"); //不应该发生
    }
    
    public static IRepoFlowApi GetApi()
    {
        HttpClient client = Utils.GetDefaultHttpClient().WithApplicationJson();
        client.BaseAddress = new Uri(ApiUrl);
        return RestService.For<IRepoFlowApi>(client, new RefitSettings
        {
            ContentSerializer = new NewtonsoftJsonContentSerializer(new JsonSerializerSettings
            {
                Converters =
                {
                    new StringEnumConverter(),
                    new VersionConverter(),
                },
                NullValueHandling = NullValueHandling.Ignore 
            }),
        });

    }

    public static string GetDownloadUrl(string repoName, string packageName, string version, string fileName,
        string? workSpaceName = null)
    {
        return $"{ApiUrl}/universal/{workSpaceName ?? WorkspaceName}/{repoName}/{packageName}/{version}/{fileName}";
    }
}