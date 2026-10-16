using LemonVN.Contracts.Services;
using SkiaSharp;

namespace LemonVN.Infrastructure.Recognition;

public sealed class RecognitionImageLoader : IRecognitionImageLoader
{
    public const int MaxBytes = 8 * 1024 * 1024;
    public const long MaxPixels = 16_000_000;
    public const int MaxEdge = 1600;

    public async Task<RecognitionImage> LoadAsync(string path, CancellationToken token)
    {
        try
        {
            if (!Path.IsPathFullyQualified(path)) throw new RecognitionException(RecognitionFailure.Image);
            using var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            if (input.Length is <= 0 or > MaxBytes) throw new RecognitionException(RecognitionFailure.Image);
            byte[] bytes = new byte[(int)input.Length];
            await input.ReadExactlyAsync(bytes, token);
            return await Task.Run(() => Normalize(bytes, token), token);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        { throw new RecognitionException(RecognitionFailure.Image); }
    }

    public static RecognitionImage Normalize(byte[] bytes, CancellationToken token = default)
    {
        token.ThrowIfCancellationRequested();
        if (bytes.Length is <= 0 or > MaxBytes) throw new RecognitionException(RecognitionFailure.Image);
        using var stream = new MemoryStream(bytes, false);
        using SKCodec? codec = SKCodec.Create(stream);
        if (codec is null || codec.EncodedFormat is not (SKEncodedImageFormat.Jpeg or SKEncodedImageFormat.Png) ||
            codec.Info.Width is <= 0 or > 8192 || codec.Info.Height is <= 0 or > 8192 ||
            (long)codec.Info.Width * codec.Info.Height > MaxPixels || codec.FrameCount > 1)
            throw new RecognitionException(RecognitionFailure.Image);
        using var decoded = new SKBitmap(new SKImageInfo(codec.Info.Width, codec.Info.Height, SKColorType.Rgba8888, SKAlphaType.Premul));
        if (codec.GetPixels(decoded.Info, decoded.GetPixels()) != SKCodecResult.Success)
            throw new RecognitionException(RecognitionFailure.Image);
        token.ThrowIfCancellationRequested();
        bool swap = codec.EncodedOrigin is SKEncodedOrigin.LeftTop or SKEncodedOrigin.RightTop or SKEncodedOrigin.RightBottom or SKEncodedOrigin.LeftBottom;
        int sourceWidth = swap ? decoded.Height : decoded.Width, sourceHeight = swap ? decoded.Width : decoded.Height;
        double ratio = Math.Min(1, MaxEdge / (double)Math.Max(sourceWidth, sourceHeight));
        int width = Math.Max(1, (int)Math.Round(sourceWidth * ratio)), height = Math.Max(1, (int)Math.Round(sourceHeight * ratio));
        using var normalized = new SKBitmap(width, height, SKColorType.Rgba8888, SKAlphaType.Opaque);
        using (var canvas = new SKCanvas(normalized))
        {
            canvas.Clear(SKColors.White);
            canvas.Scale((float)ratio);
            // Apply EXIF orientation to pixels before discarding the original metadata.
            switch (codec.EncodedOrigin)
            {
                case SKEncodedOrigin.TopRight: canvas.Translate(decoded.Width, 0); canvas.Scale(-1, 1); break;
                case SKEncodedOrigin.BottomRight: canvas.Translate(decoded.Width, decoded.Height); canvas.RotateDegrees(180); break;
                case SKEncodedOrigin.BottomLeft: canvas.Translate(0, decoded.Height); canvas.Scale(1, -1); break;
                case SKEncodedOrigin.LeftTop: canvas.RotateDegrees(90); canvas.Scale(1, -1); break;
                case SKEncodedOrigin.RightTop: canvas.Translate(decoded.Height, 0); canvas.RotateDegrees(90); break;
                case SKEncodedOrigin.RightBottom: canvas.Translate(decoded.Height, decoded.Width); canvas.RotateDegrees(90); canvas.Scale(-1, 1); break;
                case SKEncodedOrigin.LeftBottom: canvas.Translate(0, decoded.Width); canvas.RotateDegrees(270); break;
            }
            using var paint = new SKPaint { FilterQuality = SKFilterQuality.Medium };
            canvas.DrawBitmap(decoded, 0, 0, paint);
        }
        using SKImage image = SKImage.FromBitmap(normalized);
        using SKData encoded = image.Encode(SKEncodedImageFormat.Jpeg, 90);
        byte[] output = encoded.ToArray();
        if (output.Length > MaxBytes) throw new RecognitionException(RecognitionFailure.Image);
        token.ThrowIfCancellationRequested();
        return new(output, width, height);
    }
}
