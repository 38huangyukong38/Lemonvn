using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json.Nodes;
using LemonVN.Infrastructure.Bangumi;
using LemonVN.Services;

namespace GalgameManager.Test.Services;

[TestFixture]
public sealed class BangumiProviderTest
{
    private sealed class Handler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        public int Calls { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            Calls++;
            return send(request, token);
        }
    }

    private static Task<IPAddress[]> PublicDns(string host, CancellationToken token) => Task.FromResult(new[] { IPAddress.Parse("8.8.8.8") });
    private static HttpResponseMessage Json(string content, HttpStatusCode status = HttpStatusCode.OK) => new(status)
        { Content = new StringContent(content, Encoding.UTF8, "application/json") };
    private const string Subject = """
        {"id":328609,"type":2,"name":"ぼっち・ざ・ろっく！","name_cn":"孤独摇滚！","summary":"公开简介","date":"2022-10-08","eps":12,"total_episodes":16,
        "images":{"large":"https://lain.bgm.tv/pic/cover/l/e2/e7/328609_test.jpg","small":"https://lain.bgm.tv/r/200/pic/cover/l/e2/e7/328609_test.jpg"},"tags":[{"name":"public tag"}],"rating":{"score":8}}
        """;
    private static string Search(string subject = Subject) => "{\"total\":1,\"data\":[" + subject + "]}";

    [TestCase("孤独摇滚")]
    [TestCase("ぼっち・ざ・ろっく！")]
    public async Task Search_SendsOnlyKeywordAndAnimationFilter_WithoutCredentials_AndCaches(string keyword)
    {
        var handler = new Handler(async (request, _) =>
        {
            Assert.That(request.Method, Is.EqualTo(HttpMethod.Post));
            Assert.That(request.Headers.Authorization, Is.Null);
            Assert.That(request.Headers.Contains("Cookie"), Is.False);
            Assert.That(request.Headers.GetValues("User-Agent").Single(), Is.EqualTo("38huangyukong38/LemonVN/0.1.0 (Windows) (https://github.com/38huangyukong38/Lemonvn)"));
            JsonNode body = JsonNode.Parse(await request.Content!.ReadAsStringAsync())!;
            Assert.That(body["keyword"]!.GetValue<string>(), Is.EqualTo(keyword));
            Assert.That(body["filter"]!["type"]![0]!.GetValue<int>(), Is.EqualTo(2));
            Assert.That(body.AsObject().Select(pair => pair.Key), Is.EquivalentTo(new[] { "keyword", "sort", "filter" }));
            return Json(Search());
        });
        using var transport = new BangumiHttpTransport(handler, resolve: PublicDns);
        var provider = new BangumiAnimeProvider(transport);
        var result = await provider.SearchAnimeAsync(keyword, 0, default);
        Assert.That(result.Items.Single().Title, Is.EqualTo("孤独摇滚！"));
        await provider.SearchAnimeAsync(keyword, 0, default);
        Assert.That(handler.Calls, Is.EqualTo(1));
    }

    [Test]
    public void Mapping_UsesDeclaredRegularEpisodes_AndNeverImportsPublicPersonalData()
    {
        var anime = BangumiAnimeProvider.ParseDetails(Encoding.UTF8.GetBytes(Subject));
        Assert.That(anime.TotalEpisodes, Is.EqualTo(12));
        Assert.That(anime.AirDate, Is.EqualTo(new DateOnly(2022, 10, 8)));
        Assert.That(anime.OriginalTitle, Is.EqualTo("ぼっち・ざ・ろっく！"));
        var node = JsonNode.Parse(Subject)!;
        node["eps"] = 0; node["date"] = "2022"; node["name_cn"] = "";
        anime = BangumiAnimeProvider.ParseDetails(Encoding.UTF8.GetBytes(node.ToJsonString()));
        Assert.That(anime.TotalEpisodes, Is.Null);
        Assert.That(anime.AirDate, Is.Null);
        Assert.That(anime.Title, Is.EqualTo(anime.OriginalTitle));
    }

    [Test]
    public void Search_FiltersNonAnime_AndRepresentsEmptyResults()
    {
        var game = JsonNode.Parse(Subject)!; game["type"] = 4; game["id"] = 1;
        var result = BangumiAnimeProvider.ParseSearch(Encoding.UTF8.GetBytes("{\"total\":2,\"data\":[" + Subject + "," + game.ToJsonString() + "]}"), 0);
        Assert.That(result.Items, Has.Count.EqualTo(1));
        Assert.That(BangumiAnimeProvider.ParseSearch(Encoding.UTF8.GetBytes("{\"total\":0,\"data\":[]}"), 0).Items, Is.Empty);
    }

    [TestCase("not JSON")]
    [TestCase("{\"id\":1,\"type\":2}")]
    [TestCase("{\"id\":1,\"name\":\"missing type\"}")]
    [TestCase("{\"id\":1,\"type\":4,\"name\":\"game\"}")]
    public void InvalidDetails_AreRejected(string json) => Assert.Throws<BangumiRequestException>(() => BangumiAnimeProvider.ParseDetails(Encoding.UTF8.GetBytes(json)));

    [TestCase(401, BangumiFailure.Authentication)]
    [TestCase(403, BangumiFailure.Authentication)]
    [TestCase(404, BangumiFailure.NotFound)]
    [TestCase(500, BangumiFailure.Server)]
    public void HttpErrors_AreBoundedAndDoNotExposeResponseBodiesOrHeaders(int status, BangumiFailure failure)
    {
        const string simulatedCredential = "MOCK_CREDENTIAL_NOT_A_REAL_TOKEN";
        var handler = new Handler((_, _) => Task.FromResult(Json(simulatedCredential, (HttpStatusCode)status)));
        using var transport = new BangumiHttpTransport(handler, resolve: PublicDns, delay: (_, _) => Task.CompletedTask);
        var error = Assert.ThrowsAsync<BangumiRequestException>(() => transport.GetJsonAsync(new("https://api.bgm.tv/v0/subjects/1"), null, default));
        Assert.That(error!.Failure, Is.EqualTo(failure));
        Assert.That(error.ToString(), Does.Not.Contain(simulatedCredential));
        Assert.That(error.InnerException, Is.Null);
        Assert.That(handler.Calls, Is.EqualTo(status == 500 ? 3 : 1));
    }

    [Test]
    public async Task RetryAfter_IsHonored_AndLongCooldownDoesNotCauseEarlyRetry()
    {
        DateTimeOffset clock = DateTimeOffset.UtcNow;
        int calls = 0;
        var handler = new Handler((_, _) =>
        {
            var response = Json(Subject, ++calls == 1 ? HttpStatusCode.TooManyRequests : HttpStatusCode.OK);
            if (calls == 1) response.Headers.RetryAfter = new(TimeSpan.FromSeconds(2));
            return Task.FromResult(response);
        });
        using var transport = new BangumiHttpTransport(handler, resolve: PublicDns, now: () => clock,
            delay: (wait, _) => { Assert.That(wait, Is.EqualTo(TimeSpan.FromSeconds(2))); clock += wait; return Task.CompletedTask; });
        await transport.GetJsonAsync(new("https://api.bgm.tv/v0/subjects/1"), null, default);
        Assert.That(handler.Calls, Is.EqualTo(2));
        var limited = new Handler((_, _) =>
        {
            var response = Json("", HttpStatusCode.TooManyRequests);
            response.Headers.RetryAfter = new(TimeSpan.FromMinutes(1));
            return Task.FromResult(response);
        });
        using var cooldown = new BangumiHttpTransport(limited, resolve: PublicDns, now: () => clock);
        Assert.ThrowsAsync<BangumiRequestException>(() => cooldown.GetJsonAsync(new("https://api.bgm.tv/v0/subjects/1"), null, default));
        Assert.ThrowsAsync<BangumiRequestException>(() => cooldown.GetJsonAsync(new("https://api.bgm.tv/v0/subjects/1"), null, default));
        Assert.That(limited.Calls, Is.EqualTo(1));
    }

    [Test]
    public async Task CancelTimeoutAndOffline_AreDistinct_AndTransportExceptionsAreSanitized()
    {
        var slow = new Handler(async (_, token) => { await Task.Delay(10_000, token); return Json(Subject); });
        using var transport = new BangumiHttpTransport(slow, resolve: PublicDns, timeout: TimeSpan.FromMilliseconds(80));
        var timeout = Assert.ThrowsAsync<BangumiRequestException>(() => transport.GetJsonAsync(new("https://api.bgm.tv/v0/subjects/1"), null, default));
        Assert.That(timeout!.Failure, Is.EqualTo(BangumiFailure.Timeout));
        using var cancel = new CancellationTokenSource();
        Task request = transport.GetJsonAsync(new("https://api.bgm.tv/v0/subjects/1"), null, cancel.Token);
        cancel.Cancel();
        Assert.That(async () => await request, Throws.InstanceOf<OperationCanceledException>());
        var offline = new Handler((_, _) => throw new HttpRequestException("MOCK_SECRET_HEADER"));
        using var failed = new BangumiHttpTransport(offline, resolve: PublicDns);
        var error = Assert.ThrowsAsync<BangumiRequestException>(() => failed.GetJsonAsync(new("https://api.bgm.tv/v0/subjects/1"), null, default));
        Assert.That(error!.Failure, Is.EqualTo(BangumiFailure.Network));
        Assert.That(error.ToString(), Does.Not.Contain("MOCK_SECRET_HEADER"));
        await Task.CompletedTask;
    }

    [Test]
    public async Task DetailsCache_FreshBypass_AndDisabledNetwork_HaveNoStartupRequests()
    {
        bool enabled = true;
        var handler = new Handler((_, _) => Task.FromResult(Json(Subject)));
        using var transport = new BangumiHttpTransport(handler, () => enabled, PublicDns);
        var provider = new BangumiAnimeProvider(transport);
        Assert.That(handler.Calls, Is.Zero);
        await provider.GetAnimeDetailsAsync("328609", false, default);
        await provider.GetAnimeDetailsAsync("328609", false, default);
        await provider.GetAnimeDetailsAsync("328609", true, default);
        Assert.That(handler.Calls, Is.EqualTo(2));
        enabled = false;
        var error = Assert.ThrowsAsync<BangumiRequestException>(() => provider.GetAnimeDetailsAsync("328609", true, default));
        Assert.That(error!.Failure, Is.EqualTo(BangumiFailure.Disabled));
        Assert.That(handler.Calls, Is.EqualTo(2));
    }

    [TestCase("http://lain.bgm.tv/pic/cover/l/a.jpg")]
    [TestCase("https://localhost/pic/cover/l/a.jpg")]
    [TestCase("https://127.0.0.1/pic/cover/l/a.jpg")]
    [TestCase("https://lain.bgm.tv.evil.test/pic/cover/l/a.jpg")]
    [TestCase("https://user:mock@lain.bgm.tv/pic/cover/l/a.jpg")]
    [TestCase("https://lain.bgm.tv:444/pic/cover/l/a.jpg")]
    [TestCase("https://lain.bgm.tv/pic/cover/l/a.jpg?token=MOCK")]
    public void UnsafeCoverUris_AreRejectedBeforeHttp(string url)
    {
        var handler = new Handler((_, _) => throw new AssertionException("No unsafe request is allowed"));
        using var transport = new BangumiHttpTransport(handler, resolve: PublicDns);
        Assert.ThrowsAsync<BangumiRequestException>(() => transport.GetImageAsync(new(url), default));
        Assert.That(handler.Calls, Is.Zero);
    }

    [TestCase("127.0.0.1")]
    [TestCase("10.0.0.1")]
    [TestCase("172.16.0.1")]
    [TestCase("192.168.1.1")]
    [TestCase("169.254.169.254")]
    [TestCase("::1")]
    [TestCase("::ffff:127.0.0.1")]
    [TestCase("fc00::1")]
    [TestCase("2001:db8::1")]
    public void PrivateDnsAndLocalAddresses_AreRejected(string ip)
    {
        Assert.That(BangumiNetworkPolicy.IsPublicAddress(IPAddress.Parse(ip)), Is.False);
        var handler = new Handler((_, _) => throw new AssertionException("No private request is allowed"));
        using var transport = new BangumiHttpTransport(handler, resolve: (_, _) => Task.FromResult(new[] { IPAddress.Parse(ip) }));
        Assert.ThrowsAsync<BangumiRequestException>(() => transport.GetImageAsync(new("https://lain.bgm.tv/pic/cover/l/a.jpg"), default));
        Assert.That(handler.Calls, Is.Zero);
    }

    [Test]
    public void DangerousRedirect_AndOversizedDownload_AreRejected()
    {
        var handler = new Handler((_, _) =>
        {
            var response = Json("", HttpStatusCode.Redirect); response.Headers.Location = new("https://127.0.0.1/private");
            return Task.FromResult(response);
        });
        using var transport = new BangumiHttpTransport(handler, resolve: PublicDns);
        Assert.ThrowsAsync<BangumiRequestException>(() => transport.GetImageAsync(new("https://lain.bgm.tv/pic/cover/l/a.jpg"), default));
        Assert.That(handler.Calls, Is.EqualTo(1));
        var large = new Handler((_, _) =>
        {
            var response = Json("small"); response.Content.Headers.ContentLength = 21 * 1024 * 1024;
            return Task.FromResult(response);
        });
        using var oversized = new BangumiHttpTransport(large, resolve: PublicDns);
        Assert.ThrowsAsync<BangumiRequestException>(() => oversized.GetImageAsync(new("https://lain.bgm.tv/pic/cover/l/a.jpg"), default));
    }

    [Test]
    public async Task CoverCache_ValidatesBytes_AndAvoidsDuplicateDownloads_WithVersionedLocalPaths()
    {
        string image = Picture();
        var handler = new Handler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(File.ReadAllBytes(image)) }));
        using var transport = new BangumiHttpTransport(handler, resolve: PublicDns);
        var paths = new AppDataPathService(Path.Combine(TestEnvironmentSetup.Root, "M5Cover", Guid.NewGuid().ToString("N")));
        var cache = new RemoteAnimeCoverCache(transport, paths);
        Uri uri = new("https://lain.bgm.tv/pic/cover/l/a.jpg");
        await Task.WhenAll(cache.GetBytesAsync(uri, default), cache.GetBytesAsync(uri, default));
        string display = await cache.GetDisplaySourceAsync(uri, default);
        Assert.That(new Uri(display).IsFile, Is.True);
        Assert.That(File.ReadAllBytes(new Uri(display).LocalPath), Is.EqualTo(File.ReadAllBytes(image)));
        Assert.That(handler.Calls, Is.EqualTo(1));
        await cache.GetBytesAsync(uri, default, true);
        Assert.That(handler.Calls, Is.EqualTo(2));
        var badHandler = new Handler((_, _) => Task.FromResult(Json("invalid image")));
        using var badTransport = new BangumiHttpTransport(badHandler, resolve: PublicDns);
        var bad = new RemoteAnimeCoverCache(badTransport, paths);
        Assert.ThrowsAsync<InvalidDataException>(() => bad.GetBytesAsync(new("https://lain.bgm.tv/pic/cover/l/b.jpg"), default));
    }

    private static string Picture()
    {
        for (DirectoryInfo? directory = new(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            string file = Path.Combine(directory.FullName, "GalgameManager", "Assets", "LemonPlaceholder.png");
            if (File.Exists(file)) return file;
        }
        throw new FileNotFoundException("Missing project-owned image fixture.");
    }

    [Test]
    public void PersistedErrorLog_DoesNotContainMockSensitiveHttpHeaderOrInnerTransportMessage()
    {
        const string mockHeader = "Authorization: Bearer MOCK_ONLY_SECRET_HEADER";
        var handler = new Handler((_, _) => throw new HttpRequestException(mockHeader));
        using var transport = new BangumiHttpTransport(handler, resolve: PublicDns);
        var error = Assert.ThrowsAsync<BangumiRequestException>(() => transport.GetJsonAsync(new("https://api.bgm.tv/v0/subjects/1"), null, default));
        var paths = new AppDataPathService(Path.Combine(TestEnvironmentSetup.Root, "M5SafeLog", Guid.NewGuid().ToString("N")));
        using (var log = new AppLogService(paths)) { log.Initialize(); log.Error(error!, "Mock network failure."); }
        string saved = string.Join("\n", Directory.GetFiles(paths.LogsPath).Select(File.ReadAllText));
        Assert.That(saved, Does.Contain("Bangumi: Network"));
        Assert.That(saved, Does.Not.Contain("MOCK_ONLY_SECRET_HEADER"));
        Assert.That(saved, Does.Not.Contain("Authorization:"));
    }
}
