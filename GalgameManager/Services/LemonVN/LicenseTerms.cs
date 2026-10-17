using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace LemonVN.Services;

/// <summary>Local, immutable documents shown to the user; never fetched over the network.</summary>
public sealed class LicenseTerms
{
    public const string SdkIdentity = "Microsoft.Windows.SDK.NET.Ref/10.0.22621.57";
    public const string SdkRelativePath = "licenses/supplemental/Microsoft.Windows.SDK.NET.Ref/10.0.22621.57/sdk_license.rtf";
    public const string SdkOriginalHash = "DD07EB178E00C6BBA4148457FC00FF77CD4887EB521D504186FE59C9EC8BBE62";
    public const string AppSdkIdentity = "Microsoft.WindowsAppSDK/2.4.0";
    public const string AppSdkRelativePath = "licenses/components/microsoft.windowsappsdk/2.4.0/license.txt";
    public const string AppSdkOriginalHash = "5B11E6347756E40FE0274BC08C97F89201B94F0D50181A09A00F1F4740840501";
    public const string WinRtMitIdentity = "Microsoft.Windows.CsWinRT/2.2.0";
    public const string WinRtMitRelativePath = "licenses/supplemental/Microsoft.Windows.CsWinRT/2.2.0/LICENSE";
    public const string WinRtMitOriginalHash = "9906940F61B1F0B533FA7D99BAF55178B2808FBE113EA51DFBFAD8572CCD5F2B";
    public const string WindowsMlIdentity = "Microsoft.Windows.AI.MachineLearning/2.1.74";
    public const string WindowsMlRelativePath = "licenses/components/Microsoft.Windows.AI.MachineLearning/2.1.74/license.txt";
    public const string WindowsMlOriginalHash = "66395F8CB219087FAE2BD025010BD9076B736C14F03B48F20295471C0C376814";
    private readonly byte[] _sdkOriginal;
    public string EndUserText { get; }
    public string ApacheText { get; }
    public string AppSdkText { get; }
    public string WinRtMitText { get; }
    public string WindowsMlText { get; }
    public string ContentHash { get; }

    private LicenseTerms(byte[] sdkOriginal, string endUserText, string apacheText, string appSdkText, string winRtMitText, string windowsMlText)
    {
        _sdkOriginal = sdkOriginal;
        EndUserText = endUserText;
        ApacheText = apacheText;
        AppSdkText = appSdkText;
        WinRtMitText = winRtMitText;
        WindowsMlText = windowsMlText;
        if (string.IsNullOrWhiteSpace(endUserText) || string.IsNullOrWhiteSpace(apacheText))
            throw new InvalidDataException("The applicable license text is empty.");
        ContentHash = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new
        {
            SdkIdentity, SdkOriginalHash, AppSdkIdentity, AppSdkOriginalHash, WinRtMitIdentity, WinRtMitOriginalHash,
            AppSdkLicense = Normalize(appSdkText), WinRtLicense = Normalize(winRtMitText),
            WindowsMlIdentity, WindowsMlOriginalHash, WindowsMlLicense = Normalize(windowsMlText),
            EndUserTerms = Normalize(endUserText),
            SourceLicense = Normalize(apacheText)
        })));
    }

    public Stream OpenSdkOriginal() => new MemoryStream(_sdkOriginal, writable: false);

    public static LicenseTerms Load(string applicationDirectory)
    {
        var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(applicationDirectory));
        byte[] sdk = ReadDocument(root, SdkRelativePath);
        if (Convert.ToHexString(SHA256.HashData(sdk)) != SdkOriginalHash)
            throw new InvalidDataException("The Windows SDK license does not match the original supplied document.");
        byte[] appSdk = ReadDocument(root, AppSdkRelativePath);
        byte[] winRtMit = ReadDocument(root, WinRtMitRelativePath);
        if (Convert.ToHexString(SHA256.HashData(appSdk)) != AppSdkOriginalHash ||
            Convert.ToHexString(SHA256.HashData(winRtMit)) != WinRtMitOriginalHash)
            throw new InvalidDataException("An additional component license does not match its original supplied document.");
        byte[] windowsMl = ReadDocument(root, WindowsMlRelativePath);
        if (Convert.ToHexString(SHA256.HashData(windowsMl)) != WindowsMlOriginalHash)
            throw new InvalidDataException("The bundled Windows ML component license does not match its original supplied document.");
        var utf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);
        return new LicenseTerms(sdk,
            utf8.GetString(ReadDocument(root, "SDK_END_USER_TERMS_DRAFT.md")).TrimStart('\uFEFF'),
            utf8.GetString(ReadDocument(root, "LICENSE")).TrimStart('\uFEFF'),
            utf8.GetString(appSdk), utf8.GetString(winRtMit), utf8.GetString(windowsMl));
    }

    private static string Normalize(string text) => text.Replace("\r\n", "\n").Replace('\r', '\n').Trim();

    private static byte[] ReadDocument(string root, string relative)
    {
        string file = Path.GetFullPath(Path.Combine(root, relative));
        if (!file.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("The license document is outside the application directory.");
        for (FileSystemInfo? item = new FileInfo(file); item is not null && item.FullName != root;
             item = item is FileInfo f ? f.Directory : ((DirectoryInfo)item).Parent)
            if (item.Exists && (item.Attributes & FileAttributes.ReparsePoint) != 0)
                throw new InvalidDataException("Linked license documents are not supported.");
        using var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (stream.Length is <= 0 or > 1024 * 1024)
            throw new InvalidDataException("The license document is empty or exceeds the size limit.");
        byte[] bytes = new byte[checked((int)stream.Length)];
        stream.ReadExactly(bytes);
        return bytes;
    }
}
