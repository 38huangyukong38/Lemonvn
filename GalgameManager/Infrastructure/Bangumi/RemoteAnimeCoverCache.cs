using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using LemonVN.Contracts.Services;
using LemonVN.Infrastructure.Anime;

namespace LemonVN.Infrastructure.Bangumi;

public sealed class RemoteAnimeCoverCache(BangumiHttpTransport transport, IAppDataPathService paths) : IRemoteAnimeCoverCache
{
    private readonly SemaphoreSlim _downloads = new(3, 3);
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _assets = new();
    private readonly string _folder = Path.Combine(paths.CachePath, "Bangumi");

    private void CheckDirectory()
    {
        foreach (string directory in new[] { paths.RootPath, paths.CachePath, _folder })
            if (Directory.Exists(directory) && new DirectoryInfo(directory).Attributes.HasFlag(FileAttributes.ReparsePoint))
                throw new IOException("Refusing linked image cache directory.");
    }

    private async Task<byte[]?> ReadCachedAsync(string key, CancellationToken token)
    {
        CheckDirectory();
        if (!Directory.Exists(_folder)) return null;
        foreach (string file in Directory.EnumerateFiles(_folder, key + "-*").OrderByDescending(File.GetLastWriteTimeUtc))
        {
            string name = Path.GetFileName(file);
            if (!System.Text.RegularExpressions.Regex.IsMatch(name, "^[A-F0-9]{64}-[A-F0-9]{64}\\.(png|jpg)$") ||
                DateTime.UtcNow - File.GetLastWriteTimeUtc(file) > TimeSpan.FromDays(1)) continue;
            if (new FileInfo(file).Attributes.HasFlag(FileAttributes.ReparsePoint)) throw new IOException("Refusing linked image cache file.");
            using var input = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read);
            if (input.Length is <= 0 or > CoverStorageService.MaxBytes) continue;
            byte[] bytes = new byte[(int)input.Length];
            await input.ReadExactlyAsync(bytes, token);
            try { CoverStorageService.ValidateBytes(bytes); return bytes; }
            catch (InvalidDataException) { /* Replace only after a fresh validated download. */ }
        }
        return null;
    }

    public async Task<byte[]?> GetBytesAsync(Uri? uri, CancellationToken token, bool fresh = false)
    {
        token.ThrowIfCancellationRequested();
        if (uri is null) return null;
        BangumiNetworkPolicy.ValidateUri(uri, true);
        string key = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(uri.AbsoluteUri)));
        SemaphoreSlim asset = _assets.GetOrAdd(key, _ => new(1, 1));
        await asset.WaitAsync(token);
        try
        {
            byte[]? cached = fresh ? null : await ReadCachedAsync(key, token);
            if (cached is not null) return cached;
            await _downloads.WaitAsync(token);
            byte[] bytes;
            try { bytes = await transport.GetImageAsync(uri, token); }
            finally { _downloads.Release(); }
            string extension = CoverStorageService.ValidateBytes(bytes);
            Directory.CreateDirectory(_folder);
            CheckDirectory();
            string destination = Path.Combine(_folder, key + "-" + Convert.ToHexString(SHA256.HashData(bytes)) + extension);
            string temporary = Path.Combine(_folder, key + "-" + Guid.NewGuid().ToString("N") + ".tmp");
            try
            {
                using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    await output.WriteAsync(bytes, token);
                    output.Flush(flushToDisk: true);
                }
                token.ThrowIfCancellationRequested();
                File.Move(temporary, destination, overwrite: true);
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
            return bytes;
        }
        finally { asset.Release(); }
    }

    public async Task<string> GetDisplaySourceAsync(Uri? uri, CancellationToken token)
    {
        byte[]? bytes = await GetBytesAsync(uri, token);
        if (bytes is null || uri is null) return CoverStorageService.Placeholder;
        string key = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(uri.AbsoluteUri)));
        return new Uri(Path.Combine(_folder, key + "-" + Convert.ToHexString(SHA256.HashData(bytes)) + CoverStorageService.ValidateBytes(bytes))).AbsoluteUri;
    }
}
