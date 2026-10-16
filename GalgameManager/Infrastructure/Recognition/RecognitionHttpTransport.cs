using System.Net;
using System.Text.RegularExpressions;
using LemonVN.Contracts.Services;

namespace LemonVN.Infrastructure.Recognition;

public sealed class RecognitionHttpTransport : IDisposable
{
    public static readonly Uri TraceSearch = new("https://api.trace.moe/search?anilistInfo&cutBorders=1");
    public static readonly Uri TraceQuota = new("https://api.trace.moe/me");
    public static readonly Uri CharacterSearch = new("https://api.animetrace.com/v1/search");
    private readonly HttpClient _client;
    private readonly Func<string, CancellationToken, Task<IPAddress[]>> _resolve;
    private readonly Func<DateTimeOffset> _now;
    private readonly TimeSpan _timeout;
    private readonly object _gate = new();
    private readonly Dictionary<string, DateTimeOffset> _cooldown = new();

    public RecognitionHttpTransport(HttpMessageHandler handler,
        Func<string, CancellationToken, Task<IPAddress[]>>? resolve = null, TimeSpan? timeout = null, Func<DateTimeOffset>? now = null)
    {
        _client = new(handler) { Timeout = Timeout.InfiniteTimeSpan };
        _client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", AppIdentity.HttpUserAgent);
        _resolve = resolve ?? ((host, token) => Dns.GetHostAddressesAsync(host, token));
        _timeout = timeout ?? TimeSpan.FromSeconds(45);
        _now = now ?? (() => DateTimeOffset.UtcNow);
    }
    public static RecognitionHttpTransport CreateProduction() => new(new HttpClientHandler
    {
        AllowAutoRedirect = false, UseCookies = false, UseDefaultCredentials = false,
        Credentials = null, DefaultProxyCredentials = null, MaxConnectionsPerServer = 2
    });

    public static void ValidatePreview(Uri uri)
    {
        if (!uri.IsAbsoluteUri || uri.Scheme != Uri.UriSchemeHttps || !uri.IsDefaultPort || uri.UserInfo.Length != 0 ||
            uri.Fragment.Length != 0 || !string.Equals(uri.IdnHost, "api.trace.moe", StringComparison.OrdinalIgnoreCase) ||
            !Regex.IsMatch(uri.AbsolutePath, "^/image/[A-Za-z0-9_-]{8,256}$", RegexOptions.CultureInvariant) || uri.Query.Length > 512)
            throw new RecognitionException(RecognitionFailure.UnsafeSource);
    }

    public async Task<byte[]> SendAsync(Uri uri, Func<HttpContent>? content, bool preview, CancellationToken caller)
    {
        caller.ThrowIfCancellationRequested();
        if (preview) ValidatePreview(uri);
        else if (uri != TraceSearch && uri != TraceQuota && uri != CharacterSearch)
            throw new RecognitionException(RecognitionFailure.UnsafeSource);
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(caller);
        deadline.CancelAfter(preview ? TimeSpan.FromSeconds(10) : _timeout);
        CancellationToken token = deadline.Token;
        int maximum = preview ? 4 * 1024 * 1024 : 2 * 1024 * 1024;
        try
        {
            for (int redirects = 0; ; redirects++)
            {
                IPAddress[] addresses = await _resolve(uri.IdnHost, token);
                if (addresses.Length == 0 || addresses.Any(ip => !IsPublicAddress(ip)))
                    throw new RecognitionException(RecognitionFailure.UnsafeSource);
                lock (_gate)
                    if (_cooldown.TryGetValue(uri.IdnHost, out var until) && until > _now())
                        throw new RecognitionException(RecognitionFailure.RateLimited);
                using var request = new HttpRequestMessage(content is null ? HttpMethod.Get : HttpMethod.Post, uri);
                request.Content = content?.Invoke();
                request.Headers.Accept.ParseAdd(preview ? "image/jpeg, image/png" : "application/json");
                using HttpResponseMessage response = await _client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token);
                int status = (int)response.StatusCode;
                if (status is 301 or 302 or 303 or 307 or 308)
                {
                    if (!preview || redirects >= 2 || response.Headers.Location is not { } location)
                        throw new RecognitionException(RecognitionFailure.UnsafeSource);
                    uri = new(uri, location); ValidatePreview(uri); continue;
                }
                if (status is 401 or 403) throw new RecognitionException(RecognitionFailure.Authentication);
                if (status == 402) throw new RecognitionException(RecognitionFailure.Quota);
                if (status is 429 or >= 500)
                {
                    TimeSpan wait = response.Headers.RetryAfter?.Delta ??
                        (response.Headers.RetryAfter?.Date is { } date ? date - _now() : TimeSpan.FromSeconds(5));
                    if (wait < TimeSpan.Zero) wait = TimeSpan.Zero;
                    DateTimeOffset until;
                    try { until = _now() + wait; } catch (ArgumentOutOfRangeException) { until = DateTimeOffset.MaxValue; }
                    lock (_gate) _cooldown[uri.IdnHost] = until;
                    throw new RecognitionException(status == 429 ? RecognitionFailure.RateLimited : RecognitionFailure.Busy);
                }
                if (status is 400 or 413 or 415) throw new RecognitionException(RecognitionFailure.Image);
                if (!response.IsSuccessStatusCode) throw new RecognitionException(RecognitionFailure.InvalidResponse);
                if (response.Content.Headers.ContentLength is { } length && length > maximum)
                    throw new RecognitionException(RecognitionFailure.InvalidResponse);
                using Stream input = await response.Content.ReadAsStreamAsync(token);
                using var output = new MemoryStream();
                byte[] buffer = new byte[16 * 1024];
                int count;
                while ((count = await input.ReadAsync(buffer, token)) > 0)
                {
                    if (output.Length + count > maximum) throw new RecognitionException(RecognitionFailure.InvalidResponse);
                    await output.WriteAsync(buffer.AsMemory(0, count), token);
                }
                return output.ToArray();
            }
        }
        catch (OperationCanceledException) when (caller.IsCancellationRequested) { throw new OperationCanceledException(caller); }
        catch (OperationCanceledException) { throw new RecognitionException(RecognitionFailure.Timeout); }
        catch (Exception error) when (error is HttpRequestException or System.Net.Sockets.SocketException || error is IOException and not RecognitionException)
        { throw new RecognitionException(RecognitionFailure.Network); }
    }

    private static bool IsPublicAddress(IPAddress address)
    {
        if (address.IsIPv4MappedToIPv6) address = address.MapToIPv4();
        byte[] b = address.GetAddressBytes();
        if (b.Length == 16) return (b[0] & 0xe0) == 0x20 && !(b[0] == 0x20 && b[1] == 1 && b[2] == 0x0d && b[3] == 0xb8);
        return b[0] is not (0 or 10 or 127) && b[0] < 224 && !(b[0] == 100 && b[1] is >= 64 and <= 127) &&
            !(b[0] == 169 && b[1] == 254) && !(b[0] == 172 && b[1] is >= 16 and <= 31) &&
            !(b[0] == 192 && b[1] is 0 or 2 or 168) && !(b[0] == 198 && (b[1] is 18 or 19 || b[1] == 51 && b[2] == 100)) &&
            !(b[0] == 203 && b[1] == 0 && b[2] == 113);
    }
    public void Dispose() => _client.Dispose();
}
