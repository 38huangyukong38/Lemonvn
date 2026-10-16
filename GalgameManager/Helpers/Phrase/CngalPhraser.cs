using System.Collections.ObjectModel;
using System.Net.Http.Headers;
using System.Text;
using System.Web;
using GalgameManager.Contracts.Phrase;
using GalgameManager.Enums;
using GalgameManager.Models;
using HtmlAgilityPack;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace GalgameManager.Helpers.Phrase;
public class CngalPhraser : IGalInfoPhraser, IGalCoversParser
{

    private HttpClient _httpClient;
    public CngalPhraser()
    {
        _httpClient = Utils.GetDefaultHttpClient().WithApplicationJson(); ;


    }
    public async Task<Galgame?> GetGalgameInfo(Galgame galgame)
    {
        var name = galgame.Name.Value ?? "";
        int? id;
        try
        {
            id = Convert.ToInt32(galgame.Ids[(int)RssType.Cngal] ?? "");
        }
        catch (Exception)
        {
            id = await GetId(name);
        }

        if(id == null) return null;

        HttpResponseMessage response = await _httpClient.GetAsync($"https://api.cngal.org/api/entries/GetEntryView/{id}");
        if (!response.IsSuccessStatusCode) return null;

        JToken jsonToken = JToken.Parse(await response.Content.ReadAsStringAsync());

        Galgame result = new()
        {
            RssType = RssType.Cngal,
            Id = id.ToString() ?? "",
            Name = jsonToken["name"]!.ToObject<string>() ?? "",
            CnName = jsonToken["name"]!.ToObject<string>() ?? "", // cnGal 只有中文名，和name相同
            Description = jsonToken["briefIntroduction"]!.ToObject<string>() ?? "",
            ImageUrl = jsonToken["mainPicture"]!.ToObject<string>() ?? ""
        };

        // developer
        List<JToken>? productionGroups = jsonToken["productionGroups"]?.ToObject<List<JToken>>();
        if (productionGroups != null )
        {
            result.Developer = productionGroups.Count switch
            {
                0 => Galgame.DefaultString,
                1 => productionGroups[0]["displayName"]!.ToObject<string>()!,
                _ => string.Join(",", productionGroups.Select(dev => dev["name"]!.ToObject<string>()!))
            };
        }

        // release date
        List<JToken>? releases = jsonToken["releases"]?.ToObject<List<JToken>>();
        if (releases != null && releases.Count > 0)
        {
            // 选择第一个release
            var timeStr = releases[0]["time"]!.ToObject<string>()!;
            // 解析 ISO 8601 格式的时间字符串并转换为本地时间
            if (DateTime.TryParse(timeStr, out DateTime dateTime))
            {
                result.ReleaseDate = dateTime.ToLocalTime().Date;
            }
        }

        // characters
         List<JToken>? characters = jsonToken["roles"]?.ToObject<List<JToken>>();
         if (characters == null) return result;

         result.Characters = new ObservableCollection<GalgameCharacter>();
         foreach (JToken character in characters)
         {
            GalgameCharacter c = new GalgameCharacter()
            {
                Name = character["name"]!.ToObject<string>()!,
                Relation = character["roleIdentity"]!.ToObject<string>()!
            };
            c.Ids[(int)GetPhraseType()] = character["id"]!.ToObject<string>()!;
            result.Characters.Add(c);
         }

         return result;
    }

    private async Task<int?> GetId(string name)
    {
        try
        {
            var url = "https://api.cngal.org/api/home/Search?Types=Game&Text=" + HttpUtility.UrlEncode(name);
            HttpResponseMessage response = await _httpClient.GetAsync(url);
            if (!response.IsSuccessStatusCode) return null;
            JToken jsonToken = JToken.Parse(await response.Content.ReadAsStringAsync());
            List<JToken>? games = jsonToken["pagedResultDto"]!["data"]!.ToObject<List<JToken>>();
            if (games == null || games.Count == 0) return null;

            // 计算相似度，疑似没有成功
            double maxSimilarity = 0;
            var target = 0;
            foreach (JToken game in games)
            {
                if (IGalInfoPhraser.Similarity(name, game["entry"]!["name"]!.ToObject<string>()!) > maxSimilarity)
                {
                    maxSimilarity = IGalInfoPhraser.Similarity(name, game["entry"]!["name"]!.ToObject<string>()!);
                    target = games.IndexOf(game);
                }
            }
            return games[target]["entry"]!["id"]!.ToObject<int>();
        }
        catch (Exception)
        {
            return null;
        }
    }

    public async Task<List<string>> GetGalgameImagesAsync(Galgame galgame)
    {
        Galgame? info = await GetGalgameInfo(galgame);
        return info?.ImageUrl is not null ? [info.ImageUrl] : [];
    }

    /// <summary>
    /// 获取封面图片
    /// </summary>
    public async Task<List<string>> GetGalCoversAsync(Galgame galgame)
    {
        var name = galgame.Name.Value ?? "";
        int? id;
        try
        {
            id = Convert.ToInt32(galgame.Ids[(int)RssType.Cngal] ?? "");
        }
        catch (Exception)
        {
            id = await GetId(name);
        }

        if (id == null) return [];

        try
        {
            HttpResponseMessage response = await _httpClient.GetAsync($"https://api.cngal.org/api/entries/GetEntryView/{id}");
            if (!response.IsSuccessStatusCode) return [];

            JToken jsonToken = JToken.Parse(await response.Content.ReadAsStringAsync());
            var url = jsonToken["mainPicture"]?.ToObject<string>();
            return url is not null ? [url] : [];
        }
        catch
        {
            return [];
        }
    }

    public RssType GetPhraseType()
    {
        return RssType.Cngal;
    }

}
