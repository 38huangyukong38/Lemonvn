namespace LemonVN;

public static class AppIdentity
{
    public const string ProjectUrl = "https://github.com/38huangyukong38/Lemonvn";
    public static string HttpUserAgent => "38huangyukong38/LemonVN/" +
        typeof(AppIdentity).Assembly.GetName().Version!.ToString(3) + " (Windows) (" + ProjectUrl + ")";
    public const string DisplayName = "LemonVN";
    public const string PackageName = "LemonVN.Desktop";
    public const string Publisher = "CN=LemonVN Development";
    public const string ApplicationId = "LemonVN";
    public const string AppUserModelId = "LemonVN.Desktop.LemonVN";
    public const string SingleInstanceKey = "LemonVN.Desktop.Main";
}
