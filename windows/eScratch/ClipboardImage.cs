using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace eScratch;
public static class ClipboardImage
{
    public static bool IsAvailable(IDataObject? data) => data != null &&
        (data.GetDataPresent(DataFormats.Bitmap) || data.GetDataPresent("PNG") || data.GetDataPresent("image/png"));

    public static BitmapSource Read(IDataObject data) => ReadCandidates(data)[0];

    // Snapshot all representations on the UI thread before asynchronous OCR begins.
    public static List<BitmapSource> ReadCandidates(IDataObject data)
    {
        var candidates = new List<BitmapSource>();
        Exception? lastError = null;
        foreach (var format in new[] { "PNG", "image/png", DataFormats.Bitmap })
        {
            try
            {
                if (!data.GetDataPresent(format)) continue;
                var value = data.GetData(format);
                BitmapSource? bitmap = value as BitmapSource;
                if (value is byte[] bytes) { using var input = new MemoryStream(bytes, false); bitmap = Decode(input); }
                else if (value is Stream input)
                {
                    long position = input.CanSeek ? input.Position : 0;
                    try { if (input.CanSeek) input.Position = 0; bitmap = Decode(input); }
                    finally { if (input.CanSeek) input.Position = position; }
                }
                if (bitmap == null) continue;
                candidates.Add(Normalize(bitmap, false));
                // Some Windows bitmap producers leave the alpha channel unset.
                // Retain raw RGB as a last-resort candidate for Bitmap only, not PNG.
                if (format == DataFormats.Bitmap && bitmap.Format == PixelFormats.Bgra32)
                    candidates.Add(Normalize(bitmap, true));
            }
            catch (Exception ex) when (ex is not OutOfMemoryException) { lastError = ex; }
        }
        if (candidates.Count == 0) throw new InvalidOperationException("Could not read the clipboard image. Copy the screenshot again and retry.", lastError);
        return candidates;
    }

    public static BitmapSource Normalize(BitmapSource source, bool ignoreAlpha)
    {
        int width = source.PixelWidth, height = source.PixelHeight;
        if (width <= 0 || height <= 0 || (long)width * height > 50_000_000)
            throw new InvalidOperationException("This image is too large. Paste a smaller screenshot.");
        var converted = source.Format == PixelFormats.Bgra32 ? source : new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);
        int stride = checked(width * 4);
        var pixels = new byte[checked(stride * height)];
        converted.CopyPixels(pixels, stride, 0);
        for (int i = 0; i < pixels.Length; i += 4)
        {
            int alpha = ignoreAlpha ? 255 : pixels[i + 3];
            for (int channel = 0; channel < 3; channel++)
                pixels[i + channel] = (byte)((pixels[i + channel] * alpha + 255 * (255 - alpha) + 127) / 255);
            pixels[i + 3] = 255;
        }
        var result = BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgra32, null, pixels, stride);
        result.Freeze();
        return result;
    }

    private static BitmapSource Decode(Stream stream)
    {
        var frame = BitmapDecoder.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad).Frames[0];
        frame.Freeze();
        return frame;
    }
}
