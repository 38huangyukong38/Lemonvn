using LemonVN.Contracts.Services;

namespace LemonVN.Infrastructure.Recognition;

public sealed class ImageRecognitionService(TraceMoeProvider scenes, AnimeTraceProvider characters,
    RecognitionHttpTransport transport, Func<DateTimeOffset>? now = null) : IImageRecognitionService
{
    private readonly SemaphoreSlim _requests = new(1, 1);
    private readonly object _gate = new();
    private readonly Func<DateTimeOffset> _now = now ?? (() => DateTimeOffset.UtcNow);
    private readonly Dictionary<string, (DateTimeOffset Time, RecognitionResult Result)> _cache = new();
    private int _generation;
    private DateTimeOffset _notBefore;

    public async Task<RecognitionResult> RecognizeAsync(RecognitionImage image, RecognitionMode mode, CancellationToken token)
    {
        if (!Enum.IsDefined(mode)) throw new ArgumentOutOfRangeException(nameof(mode));
        await _requests.WaitAsync(token);
        try
        {
            int generation;
            string key = mode + ":" + image.Hash;
            lock (_gate)
            {
                generation = _generation;
                if (_cache.TryGetValue(key, out var cached) && _now() - cached.Time < TimeSpan.FromMinutes(5)) return cached.Result;
                if (_notBefore > _now()) throw new RecognitionException(RecognitionFailure.RateLimited);
            }
            RecognitionResult result = mode == RecognitionMode.Scene ? await scenes.RecognizeAsync(image, token)
                : await characters.RecognizeAsync(image, token);
            token.ThrowIfCancellationRequested();
            lock (_gate)
            {
                if (generation == _generation)
                {
                    if (_cache.Count >= 8) _cache.Remove(_cache.MinBy(pair => pair.Value.Time).Key);
                    _cache[key] = (_now(), result);
                }
            }
            return result;
        }
        catch (RecognitionException error) when (error.Failure is RecognitionFailure.Quota or RecognitionFailure.Busy or RecognitionFailure.RateLimited)
        {
            lock (_gate) _notBefore = _now().AddSeconds(5);
            throw;
        }
        finally { _requests.Release(); }
    }

    public async Task<byte[]> GetPreviewAsync(Uri uri, CancellationToken token)
    {
        await _requests.WaitAsync(token);
        try
        {
            byte[] bytes = await transport.SendAsync(uri, null, true, token);
            return (await Task.Run(() => RecognitionImageLoader.Normalize(bytes, token), token)).CopyBytes();
        }
        finally { _requests.Release(); }
    }

    public void ClearSession()
    {
        lock (_gate) { _generation++; _cache.Clear(); }
    }
}
