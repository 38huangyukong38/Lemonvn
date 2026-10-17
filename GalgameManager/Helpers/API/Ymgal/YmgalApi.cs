// LemonVN modification from the fixed PotatoVN baseline; see LICENSE, NOTICE and UPSTREAM_PROVENANCE.json.
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Refit;

namespace GalgameManager.Helpers.API.Ymgal;

public static class YmgalApi
{
    private static readonly string BaseUrl = "https://www.ymgal.games/";
    
    public static IYmgalApi GetApi(string? accessToken = null)
    {
        HttpClient client = Utils.GetDefaultHttpClient();
        
        if (!string.IsNullOrEmpty(accessToken))
        {
            client = client.WithApplicationJson();
            client.DefaultRequestHeaders.Add("Authorization", $"Bearer {accessToken}");
            client.DefaultRequestHeaders.Add("version", "1");
        }

        client.BaseAddress = new Uri(BaseUrl);
        
        return RestService.For<IYmgalApi>(client, new RefitSettings
        {
            ContentSerializer = new NewtonsoftJsonContentSerializer(new JsonSerializerSettings
            {
                ContractResolver = new CamelCasePropertyNamesContractResolver()
            }),
        });
    }
    
    // LemonVN modification: this upstream game provider is not registered.
    // Do not carry or use its default authentication identity in this fork.
    public static Task<IYmgalApi> GetAuthenticatedApiAsync() =>
        Task.FromException<IYmgalApi>(new NotSupportedException(
            "Legacy game authentication is not configured in LemonVN."));
}
