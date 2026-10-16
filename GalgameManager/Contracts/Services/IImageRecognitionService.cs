namespace LemonVN.Contracts.Services;

public enum RecognitionMode { Scene, Character }
public enum RecognitionFailure { Image, Network, Timeout, Authentication, RateLimited, Quota, Busy, InvalidResponse, UnsafeSource }

// Safe errors deliberately exclude server bodies, request URLs, file paths and inner exceptions.
public sealed class RecognitionException(RecognitionFailure failure) : IOException("Recognition: " + failure)
{
    public RecognitionFailure Failure { get; } = failure;
}

public sealed class RecognitionImage
{
    private readonly byte[] _bytes;
    public string Hash { get; }
    public int Width { get; }
    public int Height { get; }
    public int Length => _bytes.Length;
    internal RecognitionImage(byte[] bytes, int width, int height)
    {
        _bytes = bytes.ToArray(); Width = width; Height = height;
        Hash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(_bytes));
    }
    public byte[] CopyBytes() => _bytes.ToArray();
}

public sealed record RecognitionBox(double Left, double Top, double Right, double Bottom);
public sealed record RecognitionCandidate(string Work, IReadOnlyList<string> Aliases, string? Character = null,
    long? AniListId = null, string? Episode = null, double? FromSeconds = null, double? ToSeconds = null,
    double? Similarity = null, int Group = 0, bool? LowConfidence = null, RecognitionBox? Box = null, Uri? Preview = null);
public sealed record RecognitionQuota(int Total, int Used, int Concurrency)
{
    public int Remaining => Math.Max(0, Total - Used);
}
public sealed record RecognitionResult(RecognitionMode Mode, IReadOnlyList<RecognitionCandidate> Candidates,
    RecognitionQuota? Quota = null, bool? AiDetected = null, bool QuotaBeforeRequest = false);

public interface IRecognitionImageLoader
{
    Task<RecognitionImage> LoadAsync(string path, CancellationToken token);
}
public interface IImageRecognitionService
{
    Task<RecognitionResult> RecognizeAsync(RecognitionImage image, RecognitionMode mode, CancellationToken token);
    Task<byte[]> GetPreviewAsync(Uri uri, CancellationToken token);
    void ClearSession();
}
public interface IRecognitionUiService
{
    Task<string?> PickImageAsync();
    Task<bool> ConfirmUploadAsync(RecognitionMode mode);
}
