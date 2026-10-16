using System.Net;

namespace LemonVN.Infrastructure.Bangumi;

public sealed class BangumiHttpTransport : IDisposable
{
    private readonly HttpClient _client;
    private readonly Func<bool> _enabled;
    private readonly Func<string, CancellationToken, Task<IPAddress[]>> _resolve;
    private readonly Func<TimeSpan, CancellationToken, Task> _delay;
    private readonly Func<DateTimeOffset> _now;
    private readonly TimeSpan _timeout;
    private readonly object _cooldownGate = new();
    private DateTimeOffset _apiNotBefore;
    private DateTimeOffset _imageNotBefore;

    public BangumiHttpTransport(HttpMessageHandler handler, Func<bool>? enabled = null,
        Func<string, CancellationToken, Task<IPAddress[]>>? resolve = null,
        Func<TimeSpan, CancellationToken, Task>? delay = null, Func<DateTimeOffset>? now = null, TimeSpan? timeout = null)
    {
        _client = new(handler) { Timeout = Timeout.InfiniteTimeSpan };
        _client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", BangumiNetworkPolicy.UserAgent);
        _enabled = enabled ?? (() => true);
        _resolve = resolve ?? ((host, token) => Dns.GetHostAddressesAsync(host, token));
        _delay = delay ?? Task.Delay;
        _now = now ?? (() => DateTimeOffset.UtcNow);
        _timeout = timeout ?? TimeSpan.FromSeconds(20);
    }

    public static BangumiHttpTransport CreateProduction(Func<bool> enabled) => new(new HttpClientHandler
    {
        AllowAutoRedirect = false, UseCookies = false, UseDefaultCredentials = false,
        Credentials = null, DefaultProxyCredentials = null, MaxConnectionsPerServer = 3
        // Respect an existing OS proxy; do not inspect/store its credentials or weaken TLS validation.
    }, enabled);

    public Task<byte[]> GetJsonAsync(Uri uri, string? body, CancellationToken token) =>
        SendAsync(uri, body, false, 2 * 1024 * 1024, token);
    public Task<byte[]> GetImageAsync(Uri uri, CancellationToken token) =>
        SendAsync(uri, null, true, Anime.CoverStorageService.MaxBytes, token);

    private async Task<byte[]> SendAsync(Uri initial, string? body, bool image, int maximumBytes, CancellationToken caller)
    {
        caller.ThrowIfCancellationRequested();
        if (!_enabled()) throw new BangumiRequestException(BangumiFailure.Disabled);
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(caller);
        deadline.CancelAfter(_timeout);
        CancellationToken token = deadline.Token;
        Uri current = initial;
        int retries = 0, redirects = 0;
        var visited = new HashSet<Uri> { current };
        try
        {
            while (true)
            {
                BangumiNetworkPolicy.ValidateUri(current, image);
                IPAddress[] addresses = await _resolve(current.IdnHost, token);
                if (addresses.Length == 0 || addresses.Any(address => !BangumiNetworkPolicy.IsPublicAddress(address)))
                    throw new BangumiRequestException(BangumiFailure.UnsafeSource);
                lock (_cooldownGate)
                    if ((image ? _imageNotBefore : _apiNotBefore) > _now())
                        throw new BangumiRequestException(BangumiFailure.RateLimited);
                using var request = new HttpRequestMessage(body is null ? HttpMethod.Get : HttpMethod.Post, current);
                if (body is not null) request.Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json");
                request.Headers.Accept.ParseAdd(image ? "image/png, image/jpeg" : "application/json");
                using HttpResponseMessage response = await _client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token);
                int status = (int)response.StatusCode;
                if (status is 301 or 302 or 303 or 307 or 308)
                {
                    Uri? location = response.Headers.Location;
                    if (!image || location is null || ++redirects > 3)
                        throw new BangumiRequestException(BangumiFailure.UnsafeSource);
                    current = new(current, location);
                    BangumiNetworkPolicy.ValidateUri(current, true);
                    if (!visited.Add(current)) throw new BangumiRequestException(BangumiFailure.UnsafeSource);
                    continue;
                }
                if (status is 401 or 403) throw new BangumiRequestException(BangumiFailure.Authentication);
                if (status == 404) throw new BangumiRequestException(BangumiFailure.NotFound);
                if (status == 429 || status is 500 or 502 or 503 or 504)
                {
                    TimeSpan wait = response.Headers.RetryAfter?.Delta ??
                        (response.Headers.RetryAfter?.Date is { } date ? date - _now() : TimeSpan.FromMilliseconds(500 * (retries + 1)));
                    if (wait < TimeSpan.Zero) wait = TimeSpan.Zero;
                    if (response.Headers.RetryAfter is not null)
                    {
                        DateTimeOffset next;
                        try { next = _now() + wait; } catch (ArgumentOutOfRangeException) { next = DateTimeOffset.MaxValue; }
                        lock (_cooldownGate) { if (image) _imageNotBefore = next; else _apiNotBefore = next; }
                    }
                    // A long server delay is never shortened; surface it and keep the cooldown.
                    if (retries++ >= 2 || wait > TimeSpan.FromSeconds(8))
                        throw new BangumiRequestException(status == 429 ? BangumiFailure.RateLimited : BangumiFailure.Server);
                    await _delay(wait, token);
                    continue;
                }
                if (!response.IsSuccessStatusCode) throw new BangumiRequestException(BangumiFailure.Server);
                if (response.Content.Headers.ContentLength is { } length && length > maximumBytes)
                    throw new BangumiRequestException(BangumiFailure.InvalidResponse);
                using Stream input = await response.Content.ReadAsStreamAsync(token);
                using var bytes = new MemoryStream();
                byte[] buffer = new byte[32 * 1024];
                int count;
                while ((count = await input.ReadAsync(buffer, token)) != 0)
                {
                    if (bytes.Length + count > maximumBytes) throw new BangumiRequestException(BangumiFailure.InvalidResponse);
                    await bytes.WriteAsync(buffer.AsMemory(0, count), token);
                }
                return bytes.ToArray();
            }
        }
        catch (OperationCanceledException) when (caller.IsCancellationRequested) { throw new OperationCanceledException(caller); }
        catch (OperationCanceledException) { throw new BangumiRequestException(BangumiFailure.Timeout); }
        catch (HttpRequestException) { throw new BangumiRequestException(BangumiFailure.Network); }
        catch (System.Net.Sockets.SocketException) { throw new BangumiRequestException(BangumiFailure.Network); }
        catch (IOException error) when (error is not BangumiRequestException) { throw new BangumiRequestException(BangumiFailure.Network); }
    }

    public void Dispose() => _client.Dispose();
}
