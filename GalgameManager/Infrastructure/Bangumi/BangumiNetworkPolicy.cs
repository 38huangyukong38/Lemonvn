using System.Net;
using System.Text.RegularExpressions;

namespace LemonVN.Infrastructure.Bangumi;

public enum BangumiFailure { Disabled, Network, Timeout, Authentication, RateLimited, Server, NotFound, InvalidResponse, UnsafeSource }

// Only safe application-owned messages. Never attach HTTP exceptions, bodies or headers.
public sealed class BangumiRequestException(BangumiFailure failure) : IOException("Bangumi: " + failure)
{
    public BangumiFailure Failure { get; } = failure;
}

public static class BangumiNetworkPolicy
{
    public static string UserAgent => AppIdentity.HttpUserAgent;
    public static readonly Uri ApiRoot = new("https://api.bgm.tv/");
    private static readonly Regex CoverPath = new("^/(?:r/[1-9][0-9]{0,3}/)?pic/cover/[a-z]/", RegexOptions.CultureInvariant);

    public static void ValidateUri(Uri uri, bool image)
    {
        if (!uri.IsAbsoluteUri || uri.Scheme != Uri.UriSchemeHttps || !uri.IsDefaultPort ||
            uri.UserInfo.Length != 0 || uri.Fragment.Length != 0 ||
            !string.Equals(uri.IdnHost, image ? "lain.bgm.tv" : "api.bgm.tv", StringComparison.OrdinalIgnoreCase))
            throw new BangumiRequestException(BangumiFailure.UnsafeSource);
        if (image)
        {
            if (uri.Query.Length != 0 || !CoverPath.IsMatch(uri.AbsolutePath))
                throw new BangumiRequestException(BangumiFailure.UnsafeSource);
        }
        else if (uri.AbsolutePath == "/v0/search/subjects")
        {
            if (!Regex.IsMatch(uri.Query, "^\\?limit=[1-9][0-9]?&offset=[0-9]+$", RegexOptions.CultureInvariant))
                throw new BangumiRequestException(BangumiFailure.UnsafeSource);
        }
        else if (!Regex.IsMatch(uri.AbsolutePath, "^/v0/subjects/[1-9][0-9]*$", RegexOptions.CultureInvariant) || uri.Query.Length != 0)
            throw new BangumiRequestException(BangumiFailure.UnsafeSource);
    }

    public static bool IsPublicAddress(IPAddress address)
    {
        if (address.IsIPv4MappedToIPv6) address = address.MapToIPv4();
        byte[] bytes = address.GetAddressBytes();
        if (bytes.Length == 16)
            return (bytes[0] & 0xe0) == 0x20 &&
                !(bytes[0] == 0x20 && bytes[1] == 0x01 && bytes[2] == 0x0d && bytes[3] == 0xb8);
        return bytes[0] is not (0 or 10 or 127) && bytes[0] < 224 &&
            !(bytes[0] == 100 && bytes[1] is >= 64 and <= 127) &&
            !(bytes[0] == 169 && bytes[1] == 254) &&
            !(bytes[0] == 172 && bytes[1] is >= 16 and <= 31) &&
            !(bytes[0] == 192 && (bytes[1] == 168 || bytes[1] == 0 || bytes[1] == 2)) &&
            !(bytes[0] == 198 && (bytes[1] is 18 or 19 || bytes[1] == 51 && bytes[2] == 100)) &&
            !(bytes[0] == 203 && bytes[1] == 0 && bytes[2] == 113);
    }
}
