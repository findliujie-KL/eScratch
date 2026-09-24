using System;
using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;

namespace eScratch;
public static class ClipboardImage
{
    public static bool IsAvailable(IDataObject? data) => data != null &&
        (data.GetDataPresent(DataFormats.Bitmap) || data.GetDataPresent("PNG") || data.GetDataPresent("image/png"));

    public static BitmapSource Read(IDataObject data)
    {
        if (data.GetDataPresent(DataFormats.Bitmap) && data.GetData(DataFormats.Bitmap) is BitmapSource bitmap) return bitmap;
        foreach (var format in new[] { "PNG", "image/png" })
        {
            if (!data.GetDataPresent(format)) continue;
            var value = data.GetData(format);
            if (value is byte[] bytes)
            {
                using var stream = new MemoryStream(bytes, false);
                return Decode(stream);
            }
            if (value is Stream streamValue)
            {
                long position = streamValue.CanSeek ? streamValue.Position : 0;
                try { if (streamValue.CanSeek) streamValue.Position = 0; return Decode(streamValue); }
                finally { if (streamValue.CanSeek) streamValue.Position = position; }
            }
        }
        throw new InvalidOperationException("Could not read the clipboard image. Copy the screenshot again and retry.");
    }
    private static BitmapSource Decode(Stream stream)
    {
        var frame = BitmapDecoder.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad).Frames[0];
        frame.Freeze();
        return frame;
    }
}
