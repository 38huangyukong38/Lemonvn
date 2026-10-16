using System.Text.RegularExpressions;
using LemonVN.Contracts.Services;
using SkiaSharp;

namespace LemonVN.Infrastructure.Anime;

public sealed class CoverStorageService(IAppDataPathService paths) : ICoverStorageService
{
    public const int MaxBytes = 20 * 1024 * 1024;
    private const long MaxPixels = 40_000_000;
    public const string Placeholder = "ms-appx:///Assets/LemonPlaceholder.png";
    private static readonly Regex OwnedReference = new("^Covers/[0-9a-f]{32}_[0-9a-f]{32}\\.(png|jpg)$",
        RegexOptions.CultureInvariant);

    public async Task<string> ImportAsync(Guid animeId, string sourcePath)
    {
        if (animeId == Guid.Empty || !Path.IsPathFullyQualified(sourcePath))
            throw new ArgumentException("A local absolute cover path is required.");
        byte[] bytes;
        using (var source = new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            if (source.Length is <= 0 or > MaxBytes) throw new InvalidDataException("Cover must be a PNG/JPEG image no larger than 20 MB.");
            bytes = new byte[(int)source.Length];
            await source.ReadExactlyAsync(bytes);
        }
        return await ImportBytesAsync(animeId, bytes, CancellationToken.None);
    }

    public static string ValidateBytes(byte[] bytes)
    {
        if (bytes.Length is <= 0 or > MaxBytes) throw new InvalidDataException("Cover size is outside the supported limit.");
        using var stream = new MemoryStream(bytes, writable: false);
        using SKCodec? codec = SKCodec.Create(stream);
        if (codec is null || codec.Info.Width <= 0 || codec.Info.Height <= 0 ||
            (long)codec.Info.Width * codec.Info.Height > MaxPixels)
            throw new InvalidDataException("Invalid or excessively large cover image.");
        string extension = codec.EncodedFormat switch
        {
            SKEncodedImageFormat.Png => ".png",
            SKEncodedImageFormat.Jpeg => ".jpg",
            _ => throw new InvalidDataException("Only PNG and JPEG covers are supported.")
        };
        // Decode once to reject truncated/corrupt input, without altering source pixels.
        using var bitmap = new SKBitmap(codec.Info);
        if (codec.GetPixels(bitmap.Info, bitmap.GetPixels()) != SKCodecResult.Success)
            throw new InvalidDataException("The cover image could not be decoded.");
        return extension;
    }

    private void CheckOwnedDirectory()
    {
        if (new DirectoryInfo(paths.RootPath).Attributes.HasFlag(FileAttributes.ReparsePoint) ||
            new DirectoryInfo(paths.CoversPath).Attributes.HasFlag(FileAttributes.ReparsePoint))
            throw new IOException("The data/covers directory must not be a filesystem link.");
    }

    public async Task<string> ImportBytesAsync(Guid animeId, byte[] bytes, CancellationToken cancellationToken)
    {
        if (animeId == Guid.Empty) throw new ArgumentException("Invalid anime ID.");
        cancellationToken.ThrowIfCancellationRequested();
        string extension = ValidateBytes(bytes);
        Directory.CreateDirectory(paths.CoversPath);
        CheckOwnedDirectory();
        string reference = $"Covers/{animeId:N}_{Guid.NewGuid():N}{extension}";
        string destination = Resolve(reference);
        bool created = false;
        try
        {
            using var output = new FileStream(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            created = true;
            await output.WriteAsync(bytes, cancellationToken);
            output.Flush(flushToDisk: true);
            return reference;
        }
        catch
        {
            if (created && File.Exists(destination)) File.Delete(destination); // Never delete a preexisting collision.
            throw;
        }
    }

    public async Task<byte[]> ReadOwnedAsync(string coverRef, CancellationToken cancellationToken)
    {
        string file = Resolve(coverRef);
        CheckOwnedDirectory();
        if (new FileInfo(file).Attributes.HasFlag(FileAttributes.ReparsePoint))
            throw new IOException("Refusing to read a linked cover file.");
        using var source = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (source.Length is <= 0 or > MaxBytes) throw new InvalidDataException("Invalid owned cover size.");
        byte[] bytes = new byte[(int)source.Length];
        await source.ReadExactlyAsync(bytes, cancellationToken);
        ValidateBytes(bytes);
        return bytes;
    }

    public IReadOnlyList<string> ListOwnedReferences()
    {
        if (!Directory.Exists(paths.CoversPath)) return [];
        CheckOwnedDirectory();
        return Directory.EnumerateFiles(paths.CoversPath).Select(file => "Covers/" + Path.GetFileName(file))
            .Where(reference => OwnedReference.IsMatch(reference)).ToList();
    }

    private string Resolve(string reference)
    {
        if (!OwnedReference.IsMatch(reference)) throw new InvalidDataException("Cover is not an owned relative reference.");
        string file = Path.GetFullPath(Path.Combine(paths.RootPath, reference));
        string allowed = Path.GetFullPath(paths.CoversPath) + Path.DirectorySeparatorChar;
        if (!file.StartsWith(allowed, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Unsafe cover reference.");
        return file;
    }

    public string GetDisplaySource(string? coverRef)
    {
        if (coverRef is null) return Placeholder;
        try
        {
            string file = Resolve(coverRef);
            if (Directory.Exists(paths.CoversPath)) CheckOwnedDirectory();
            if (File.Exists(file) && new FileInfo(file).Attributes.HasFlag(FileAttributes.ReparsePoint)) return Placeholder;
            return File.Exists(file) ? new Uri(file).AbsoluteUri : Placeholder;
        }
        catch (Exception error) when (error is InvalidDataException or IOException or UnauthorizedAccessException) { return Placeholder; }
    }

    public void RemoveUncommitted(string coverRef)
    {
        string file = Resolve(coverRef);
        if (new DirectoryInfo(paths.CoversPath).Attributes.HasFlag(FileAttributes.ReparsePoint))
            throw new IOException("Refusing to delete through a linked covers directory.");
        if (File.Exists(file)) File.Delete(file);
    }
}
