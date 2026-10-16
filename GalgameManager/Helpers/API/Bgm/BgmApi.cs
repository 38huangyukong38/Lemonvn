using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Refit;

namespace GalgameManager.Helpers.API.Bgm;

public static class BgmApi
{
    public static IBgmApi GetApi(string? token)
    {
        HttpClient client = Utils.GetDefaultHttpClient().WithApplicationJson();
        if (!string.IsNullOrEmpty(token))
            client.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");
        client.BaseAddress = new Uri("https://api.bgm.tv");
        return RestService.For<IBgmApi>(client, new RefitSettings
        {
            ContentSerializer = new NewtonsoftJsonContentSerializer(new JsonSerializerSettings
            {
                Converters =
                {
                    new StringEnumConverter(),
                },
            }),
        });
    }
}