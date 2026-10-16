using System.Net;
using System.Net.Http.Headers;
using System.Text;
using LemonVN.Contracts.Services;
using LemonVN.Infrastructure.Recognition;
using SkiaSharp;

namespace GalgameManager.Test.Services;

[TestFixture]
public sealed class RecognitionProviderTest
{
    private const string Scene = """
        {"error":"","quota":100,"quotaUsed":1,"result":[
        {"anilist":{"id":123,"title":{"native":"テスト","romaji":"Test","english":null},"synonyms":["Alias"]},
        "episode":[2,3],"from":120.25,"to":122,"similarity":0.91,"image":"https://api.trace.moe/image/abcdefghijk"},
        {"anilist":456,"episode":null,"from":null,"to":null,"similarity":0.5,"image":"http://127.0.0.1/private"}]}
        """;
    private const string Characters = """
        {"code":0,"ai":false,"trace_id":"synthetic-request","data":[
        {"box":[0.1,0.1,0.4,0.6],"not_confident":true,"character":[{"work":"A","character":"One"},{"work":"B","character":"Two"}]},
        {"box":[0.5,0.1,0.9,0.7],"not_confident":false,"character":[{"work":"C","character":"Three"}]}]}
        """;
    private sealed class Handler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token) => send(request, token);
    }
    private static HttpResponseMessage Json(string json) => new(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
    private static byte[] Bytes(string value) => Encoding.UTF8.GetBytes(value);
    private static RecognitionHttpTransport Transport(HttpMessageHandler handler, TimeSpan? timeout = null) =>
        new(handler, (_, _) => Task.FromResult(new[] { IPAddress.Parse("1.1.1.1") }), timeout);
    public static byte[] Image(int width = 20, int height = 10, SKEncodedImageFormat format = SKEncodedImageFormat.Png)
    {
        using var bitmap = new SKBitmap(width, height);
        bitmap.Erase(SKColors.Gold);
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(format, 90);
        return data.ToArray();
    }

    [Test]
    public void SceneParser_PreservesIdsAliasesEpisodesAndSimilarityWithoutInventingTitle()
    {
        RecognitionResult result = TraceMoeProvider.Parse(Bytes(Scene));
        Assert.That(result.Candidates, Has.Count.EqualTo(2));
        RecognitionCandidate one = result.Candidates[0], two = result.Candidates[1];
        Assert.That(one.Work, Is.EqualTo("テスト")); Assert.That(one.Aliases, Is.EqualTo(new[] { "テスト", "Test", "Alias" }));
        Assert.That(one.AniListId, Is.EqualTo(123)); Assert.That(one.Episode, Is.EqualTo("2, 3"));
        Assert.That(one.FromSeconds, Is.EqualTo(120.25)); Assert.That(one.Similarity, Is.EqualTo(0.91));
        Assert.That(one.Preview, Is.Not.Null); Assert.That(two.Preview, Is.Null);
        Assert.That(two.Work, Is.Empty); Assert.That(two.Episode, Is.Null); Assert.That(two.AniListId, Is.EqualTo(456));
        Assert.That(result.Quota!.Remaining, Is.EqualTo(99));
    }

    [Test]
    public void CharacterParser_PreservesMultiplePeopleAndQualitativeConfidence()
    {
        var result = AnimeTraceProvider.Parse(Bytes(Characters));
        Assert.That(result.Candidates.Select(c => c.Group), Is.EqualTo(new[] { 1, 1, 2 }));
        Assert.That(result.Candidates.Select(c => c.Character), Is.EqualTo(new[] { "One", "Two", "Three" }));
        Assert.That(result.Candidates[0].LowConfidence, Is.True); Assert.That(result.Candidates[2].LowConfidence, Is.False);
        Assert.That(result.Candidates.All(c => c.Similarity is null && c.AniListId is null), Is.True);
        Assert.That(result.Candidates[0].Box, Is.EqualTo(new RecognitionBox(0.1, 0.1, 0.4, 0.6)));
        Assert.That(result.AiDetected, Is.False);
    }

    [TestCase(RecognitionMode.Scene, "{\"error\":\"\",\"result\":[]}")]
    [TestCase(RecognitionMode.Character, "{\"code\":0,\"data\":[]}")]
    public void NoMatches_AreValidAndDoNotFabricateCandidates(RecognitionMode mode, string json) =>
        Assert.That((mode == RecognitionMode.Scene ? TraceMoeProvider.Parse(Bytes(json)) : AnimeTraceProvider.Parse(Bytes(json))).Candidates, Is.Empty);

    [TestCase("{")]
    [TestCase("null")]
    [TestCase("{}")]
    [TestCase("{\"result\":{}}")]
    [TestCase("{\"result\":[{\"anilist\":0,\"similarity\":0.8}]}")]
    [TestCase("{\"result\":[{\"anilist\":1,\"similarity\":1.5}]}")]
    [TestCase("{\"result\":[{\"anilist\":1,\"similarity\":0.8,\"from\":4,\"to\":2}]}")]
    public void MalformedSceneResponses_AreRejectedWithSafeErrors(string json) =>
        Assert.Throws<RecognitionException>(() => TraceMoeProvider.Parse(Bytes(json)));

    [TestCase("{}")]
    [TestCase("{\"code\":0,\"data\":[{\"character\":[{}]}]}")]
    [TestCase("{\"code\":0,\"data\":[{\"box\":[0,0,2,1],\"character\":[]}]}")]
    [TestCase("{\"code\":0,\"data\":[{\"not_confident\":\"yes\",\"character\":[]}]}")]
    public void MalformedCharacterResponses_AreRejected(string json) =>
        Assert.Throws<RecognitionException>(() => AnimeTraceProvider.Parse(Bytes(json)));

    [TestCase(17728, RecognitionFailure.Quota)]
    [TestCase(17702, RecognitionFailure.Busy)]
    [TestCase(17731, RecognitionFailure.Busy)]
    [TestCase(17704, RecognitionFailure.Authentication)]
    [TestCase(17701, RecognitionFailure.Image)]
    public void CharacterServiceCodes_AreNotTreatedAsMatches(int code, RecognitionFailure expected) =>
        Assert.That(Assert.Throws<RecognitionException>(() => AnimeTraceProvider.Parse(Bytes("{\"code\":" + code + "}")))!.Failure, Is.EqualTo(expected));

    [TestCase("http://api.trace.moe/image/abcdefghijk")]
    [TestCase("https://api.trace.moe.evil.example/image/abcdefghijk")]
    [TestCase("https://api.trace.moe:444/image/abcdefghijk")]
    [TestCase("https://user@api.trace.moe/image/abcdefghijk")]
    [TestCase("https://api.trace.moe/video/abcdefghijk")]
    [TestCase("file:///C:/private.png")]
    public void PreviewAllowList_RejectsUnsafeSources(string url) =>
        Assert.Throws<RecognitionException>(() => RecognitionHttpTransport.ValidatePreview(new(url)));

    [TestCase(401, RecognitionFailure.Authentication)]
    [TestCase(403, RecognitionFailure.Authentication)]
    [TestCase(402, RecognitionFailure.Quota)]
    [TestCase(429, RecognitionFailure.RateLimited)]
    [TestCase(500, RecognitionFailure.Busy)]
    [TestCase(503, RecognitionFailure.Busy)]
    [TestCase(413, RecognitionFailure.Image)]
    public void HttpErrors_AreBoundedAndDoNotExposeBodies(int status, RecognitionFailure expected)
    {
        int calls = 0;
        using var transport = Transport(new Handler((_, _) =>
        {
            calls++;
            return Task.FromResult(new HttpResponseMessage((HttpStatusCode)status) { Content = new StringContent("private-body-marker") });
        }));
        var error = Assert.ThrowsAsync<RecognitionException>(() => transport.SendAsync(RecognitionHttpTransport.CharacterSearch,
            () => new StringContent("synthetic"), false, default));
        Assert.That(error!.Failure, Is.EqualTo(expected)); Assert.That(error.ToString(), Does.Not.Contain("private-body-marker"));
        Assert.That(calls, Is.EqualTo(1), "An upload must not be automatically retried.");
    }

    [Test]
    public async Task Requests_UseSanitizedBytesAndGenericMultipartFilename()
    {
        RecognitionImage input = RecognitionImageLoader.Normalize(Image());
        var paths = new List<string>();
        using var transport = Transport(new Handler(async (request, token) =>
        {
            paths.Add(request.RequestUri!.AbsolutePath);
            Assert.That(request.Headers.Authorization, Is.Null); Assert.That(request.Headers.Contains("Cookie"), Is.False);
            Assert.That(request.Headers.UserAgent.ToString(), Does.Contain("38huangyukong38/LemonVN"));
            Assert.That(request.Headers.UserAgent.ToString(), Does.Contain("https://github.com/38huangyukong38/Lemonvn"));
            if (request.RequestUri.AbsolutePath == "/me") return Json("{\"quota\":100,\"quotaUsed\":0,\"concurrency\":1,\"id\":\"private-ip-marker\"}");
            if (request.RequestUri.AbsolutePath == "/search")
            {
                Assert.That(await request.Content!.ReadAsByteArrayAsync(token), Is.EqualTo(input.CopyBytes()));
                Assert.That(request.Content.Headers.ContentType!.MediaType, Is.EqualTo("image/jpeg"));
                return Json(Scene);
            }
            var form = (MultipartFormDataContent)request.Content!;
            var file = form.Single(p => p.Headers.ContentDisposition!.Name!.Trim('"') == "file");
            Assert.That(file.Headers.ContentDisposition!.FileName!.Trim('"'), Is.EqualTo("upload.jpg"));
            Assert.That(form.Select(p => p.Headers.ContentDisposition!.Name!.Trim('"')),
                Is.EquivalentTo(new[] { "is_multi", "ai_detect", "file" }));
            Assert.That(await file.ReadAsByteArrayAsync(token), Is.EqualTo(input.CopyBytes()));
            return Json(Characters);
        }));
        await new TraceMoeProvider(transport).RecognizeAsync(input, default);
        await new AnimeTraceProvider(transport).RecognizeAsync(input, default);
        Assert.That(paths, Is.EqualTo(new[] { "/me", "/search", "/v1/search" }));
    }

    [Test]
    public void EmptyQuota_PreventsImageUpload()
    {
        int calls = 0;
        using var transport = Transport(new Handler((request, _) =>
        {
            calls++; Assert.That(request.Method, Is.EqualTo(HttpMethod.Get));
            return Task.FromResult(Json("{\"quota\":100,\"quotaUsed\":100,\"concurrency\":1}"));
        }));
        Assert.That(Assert.ThrowsAsync<RecognitionException>(() => new TraceMoeProvider(transport)
            .RecognizeAsync(RecognitionImageLoader.Normalize(Image()), default))!.Failure, Is.EqualTo(RecognitionFailure.Quota));
        Assert.That(calls, Is.EqualTo(1));
    }

    [TestCase(true)]
    [TestCase(false)]
    public async Task QuotaFallback_IsExplicitlyIdentifiedAsBeforeTheUpload(bool responseContainsQuota)
    {
        using var transport = Transport(new Handler((request, _) => Task.FromResult(Json(request.Method == HttpMethod.Get
            ? "{\"quota\":100,\"quotaUsed\":5,\"concurrency\":1}"
            : responseContainsQuota ? Scene : "{\"error\":\"\",\"result\":[]}"))));
        RecognitionResult result = await new TraceMoeProvider(transport).RecognizeAsync(RecognitionImageLoader.Normalize(Image()), default);
        Assert.That(result.QuotaBeforeRequest, Is.EqualTo(!responseContainsQuota));
        Assert.That(result.Quota!.Remaining, Is.EqualTo(responseContainsQuota ? 99 : 95));
    }

    [TestCase(true)]
    [TestCase(false)]
    public void OversizedResponses_AreRejectedEvenWithoutContentLength(bool withLength)
    {
        byte[] bytes = new byte[2 * 1024 * 1024 + 1];
        using var transport = Transport(new Handler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        { Content = withLength ? new ByteArrayContent(bytes) : new UnknownLengthContent(bytes) })));
        Assert.That(Assert.ThrowsAsync<RecognitionException>(() => transport.SendAsync(RecognitionHttpTransport.TraceQuota,
            null, false, default))!.Failure, Is.EqualTo(RecognitionFailure.InvalidResponse));
    }

    private sealed class UnknownLengthContent(byte[] bytes) : HttpContent
    {
        protected override bool TryComputeLength(out long length) { length = 0; return false; }
        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) => stream.WriteAsync(bytes).AsTask();
    }

    [Test]
    public void GifInput_IsRejectedEvenWhenItsPixelsCouldBeDecoded()
    {
        byte[] gif = Convert.FromBase64String("R0lGODlhAQABAIAAAAAAAP///yH5BAEAAAAALAAAAAABAAEAAAIBRAA7");
        Assert.That(Assert.Throws<RecognitionException>(() => RecognitionImageLoader.Normalize(gif))!.Failure, Is.EqualTo(RecognitionFailure.Image));
    }

    [Test]
    public void TimeoutAndCallerCancellation_AreDistinct()
    {
        using var transport = Transport(new Handler(async (_, token) => { await Task.Delay(Timeout.Infinite, token); return Json("{}"); }), TimeSpan.FromMilliseconds(30));
        Assert.That(Assert.ThrowsAsync<RecognitionException>(() => transport.SendAsync(RecognitionHttpTransport.TraceQuota, null, false, default))!.Failure, Is.EqualTo(RecognitionFailure.Timeout));
        using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
        Assert.ThrowsAsync<OperationCanceledException>(() => transport.SendAsync(RecognitionHttpTransport.TraceQuota, null, false, cancelled.Token));
    }

    [Test]
    public void RetryAfter_DoesNotCauseAnotherUpload()
    {
        int calls = 0;
        using var transport = Transport(new Handler((_, _) =>
        {
            calls++; var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
            response.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromMinutes(1));
            return Task.FromResult(response);
        }));
        for (int i = 0; i < 2; i++) Assert.ThrowsAsync<RecognitionException>(() => transport.SendAsync(
            RecognitionHttpTransport.CharacterSearch, () => new StringContent("synthetic"), false, default));
        Assert.That(calls, Is.EqualTo(1));
    }

    [Test]
    public void PrivateDnsAndRedirectedUpload_AreRejected()
    {
        int calls = 0;
        var handler = new Handler((_, _) => { calls++; return Task.FromResult(new HttpResponseMessage(HttpStatusCode.TemporaryRedirect)
        { Headers = { Location = new Uri("https://api.animetrace.com/v1/search") } }); });
        using var privateDns = new RecognitionHttpTransport(handler, (_, _) => Task.FromResult(new[] { IPAddress.Loopback }));
        Assert.ThrowsAsync<RecognitionException>(() => privateDns.SendAsync(RecognitionHttpTransport.TraceQuota, null, false, default));
        Assert.That(calls, Is.Zero);
        using var redirect = Transport(new Handler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.TemporaryRedirect)
        { Headers = { Location = new Uri("https://api.animetrace.com/v1/search") } })));
        Assert.That(Assert.ThrowsAsync<RecognitionException>(() => redirect.SendAsync(RecognitionHttpTransport.CharacterSearch,
            () => new StringContent("synthetic"), false, default))!.Failure, Is.EqualTo(RecognitionFailure.UnsafeSource));
    }

    [Test]
    public async Task SessionCache_AvoidsRepeatedUploadsAndClearsOnExit()
    {
        int calls = 0;
        using var transport = Transport(new Handler((_, _) => { calls++; return Task.FromResult(Json(Characters)); }));
        var service = new ImageRecognitionService(new(transport), new(transport), transport);
        RecognitionImage input = RecognitionImageLoader.Normalize(Image());
        await service.RecognizeAsync(input, RecognitionMode.Character, default);
        await service.RecognizeAsync(input, RecognitionMode.Character, default);
        Assert.That(calls, Is.EqualTo(1));
        service.ClearSession(); await service.RecognizeAsync(input, RecognitionMode.Character, default);
        Assert.That(calls, Is.EqualTo(2));
    }

    [TestCase(SKEncodedImageFormat.Png)]
    [TestCase(SKEncodedImageFormat.Jpeg)]
    public void NormalizedImage_IsBoundedJpegAndImmutable(SKEncodedImageFormat format)
    {
        RecognitionImage input = RecognitionImageLoader.Normalize(Image(1800, 20, format));
        Assert.That(input.Width, Is.EqualTo(1600)); Assert.That(input.Height, Is.LessThanOrEqualTo(20));
        byte[] one = input.CopyBytes(); one[0] = 0;
        Assert.That(input.CopyBytes()[0], Is.EqualTo(255));
        using var stream = new MemoryStream(input.CopyBytes()); using var codec = SKCodec.Create(stream);
        Assert.That(codec.EncodedFormat, Is.EqualTo(SKEncodedImageFormat.Jpeg));
    }

    [TestCase("corrupt")]
    [TestCase("oversize")]
    [TestCase("dimensions")]
    public void InvalidImages_AreRejectedBeforeUse(string kind)
    {
        byte[] bytes = kind == "corrupt" ? [1, 2, 3] : kind == "oversize" ? new byte[RecognitionImageLoader.MaxBytes + 1] : Image(8193, 1);
        Assert.That(Assert.Throws<RecognitionException>(() => RecognitionImageLoader.Normalize(bytes))!.Failure, Is.EqualTo(RecognitionFailure.Image));
    }

    [Test]
    public void ImageMetadata_IsRemoved()
    {
        byte[] jpeg = Image(format: SKEncodedImageFormat.Jpeg), comment = Bytes("sensitive-image-comment");
        byte[] source = [255, 216, 255, 254, 0, (byte)(comment.Length + 2), .. comment, .. jpeg.Skip(2)];
        RecognitionImage normalized = RecognitionImageLoader.Normalize(source);
        Assert.That(Encoding.ASCII.GetString(normalized.CopyBytes()), Does.Not.Contain("sensitive-image-comment"));
    }

    [Test]
    public void LocalReadFailure_DoesNotExposeThePrivatePath()
    {
        string path = Path.Combine(TestEnvironmentSetup.Root, "private-path-marker", "missing.png");
        var error = Assert.ThrowsAsync<RecognitionException>(() => new RecognitionImageLoader().LoadAsync(path, default));
        Assert.That(error!.ToString(), Does.Not.Contain("private-path-marker"));
    }
}
