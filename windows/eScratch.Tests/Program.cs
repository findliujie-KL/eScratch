using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using eScratch;

internal static class Program
{
    private static int passed;
    private static void Check(bool condition, string name)
    {
        if (!condition) throw new Exception("FAIL: " + name);
        Console.WriteLine("PASS: " + name); passed++;
    }
    [STAThread]
    private static int Main(string[] args)
    {
        var folder = Path.Combine(Path.GetTempPath(), "eScratch-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            using var cases = System.Text.Json.JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "markdown-cases.json")));
            foreach (var test in cases.RootElement.EnumerateArray())
            {
                var actual = MarkdownPaste.Convert(test.GetProperty("plain").GetString()!, test.GetProperty("html").GetString()!, test.GetProperty("rtf").GetBoolean());
                Check(actual == test.GetProperty("expected").GetString(), "Markdown: " + test.GetProperty("name").GetString() + " => " + actual);
            }
            Check(LoginStartup.Command(@"C:\My Apps\eScratch.exe", "unused") == "\"C:\\My Apps\\eScratch.exe\" --login", "Startup quotes portable executable path");
            Check(LoginStartup.Command(@"C:\dotnet\dotnet.exe", @"C:\My Apps\eScratch.dll") == "\"C:\\dotnet\\dotnet.exe\" \"C:\\My Apps\\eScratch.dll\" --login", "Development startup includes quoted assembly path");
            using (var wordCases = System.Text.Json.JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "word-count-cases.json"))))
            foreach (var test in wordCases.RootElement.GetProperty("cases").EnumerateArray())
            {
                var input = test.GetProperty("text").GetString()!;
                Check(WordCounter.Count(input) == test.GetProperty("expected").GetInt32(), "Word-compatible count: " + System.Text.Json.JsonSerializer.Serialize(input));
            }
            var store = new StateStore(folder);
            Check(store.State.Settings.MarkdownShortcut == "Control+Shift+V", "Markdown shortcut defaults to Ctrl+Shift+V");
            store.State.Settings.MarkdownShortcut = "Control+Alt+M"; store.Save();
            Check(new StateStore(folder).State.Settings.MarkdownShortcut == "Control+Alt+M", "Custom Markdown shortcut survives restart");
            Check(store.RestoreDefaults(_ => true) && store.State.Settings.MarkdownShortcut == "Control+Shift+V", "Restore defaults resets Markdown shortcut");
            Check(store.State.Settings.HistoryLimit == 10, "History defaults to 10");
            store.Remember("  "); Check(store.State.History.Count == 0, "Blank drafts do not enter history");
            for (var i = 0; i < 12; i++) store.Remember("Draft " + i);
            Check(store.State.History.Count == 10 && store.State.History[0].Text == "Draft 11", "Newest ten drafts retained in order");
            Check(store.State.History.Select(h => h.Id).Distinct().Count() == 10, "Rapid history writes have unique IDs");
            store.State.Draft = "Unsaved draft 中文 日本語\nSecond line";
            store.State.Settings.LightTheme = true; store.Save();
            store = new StateStore(folder);
            Check(store.State.Draft.Contains("中文") && store.State.Settings.LightTheme, "Unicode draft and settings survive restart");
            store.State.Settings.HistoryLimit = 3; store.Save();
            Check(new StateStore(folder).State.History.Count == 3, "Reducing limit persists trimmed history");
            store.State.History.Clear(); store.Save();
            Check(new StateStore(folder).State.History.Count == 0 && new StateStore(folder).State.Draft.Length > 0, "Clear history preserves draft");
            File.WriteAllText(Path.Combine(folder, "state.json"), "{broken json");
            store = new StateStore(folder);
            Check(store.Warning != null && Directory.GetFiles(folder, "*.corrupt-*").Length == 1, "Corrupt state is backed up before recovery");
            File.WriteAllText(Path.Combine(folder, "state.json"), "{\"Settings\":null,\"History\":null,\"Draft\":null}");
            store = new StateStore(folder);
            Check(store.State.Settings.HistoryLimit == 10 && store.State.Draft == "", "Null persisted fields safely normalized");
            Check(Hotkey.Parse("Control+Shift+C").Modifiers == (ModifierKeys.Control | ModifierKeys.Shift), "Electron shortcut format imports correctly");
            using (var source = new HwndSource(new HwndSourceParameters("eScratch hotkey test") { Width = 1, Height = 1 }))
            using (var first = new Hotkey(source.Handle, () => { }))
            using (var otherSource = new HwndSource(new HwndSourceParameters("eScratch conflicting hotkey test") { Width = 1, Height = 1 }))
            using (var second = new Hotkey(otherSource.Handle, () => { }))
            {
                Check(first.Register("Ctrl+Alt+Shift+F11"), "Global hotkey registers with Windows");
                Check(!second.Register("Ctrl+Alt+Shift+F11"), "Conflicting shortcut is rejected");
                Check(!first.Register("not a shortcut") && first.Register("Ctrl+Alt+Shift+F11"), "Invalid replacement preserves the active hotkey");
            }
            var ocr = new OcrService(folder);
            Check(ocr.Installed("eng") && ocr.Catalog.Count >= 160, "Bundled English and full language/script catalog available");
            bool rejected = false;
            try { ocr.Installed("../escape"); } catch (ArgumentException) { rejected = true; }
            Check(rejected, "Language path traversal rejected");
            rejected = false; try { ocr.Remove("eng"); } catch (InvalidOperationException) { rejected = true; }
            Check(rejected, "Bundled English cannot be removed");
            var visual = new DrawingVisual();
            using (var dc = visual.RenderOpen())
            {
                dc.DrawRectangle(Brushes.White, null, new Rect(0, 0, 900, 180));
                dc.DrawText(new FormattedText("Hello eScratch 123", CultureInfo.InvariantCulture, FlowDirection.LeftToRight, new Typeface("Arial"), 56, Brushes.Black, 1), new Point(35, 45));
            }
            var bitmap = new RenderTargetBitmap(900, 180, 96, 96, PixelFormats.Pbgra32); bitmap.Render(visual);
            var png = new PngBitmapEncoder(); png.Frames.Add(BitmapFrame.Create(bitmap));
            using var bytes = new MemoryStream(); png.Save(bytes);
            var clipboard = new DataObject();
            clipboard.SetData("PNG", bytes.ToArray());
            clipboard.SetData(DataFormats.Bitmap, BitmapSource.Create(1, 1, 96, 96, PixelFormats.Bgra32, null, new byte[] { 255, 255, 255, 255 }, 4));
            Check(ClipboardImage.Read(clipboard).PixelWidth == 900, "PNG takes priority over clipboard Bitmap");
            clipboard.SetData("PNG", new byte[] { 0, 1, 2 });
            Check(ClipboardImage.Read(clipboard).PixelWidth == 1, "Malformed PNG falls back to Bitmap");
            var transparent = BitmapSource.Create(1, 1, 96, 96, PixelFormats.Bgra32, null, new byte[] { 0, 0, 0, 0 }, 4);
            var pixel = new byte[4]; ClipboardImage.Normalize(transparent, false).CopyPixels(pixel, 4, 0);
            Check(pixel.All(c => c == 255), "Transparent pixels flatten onto opaque white");
            ClipboardImage.Normalize(transparent, true).CopyPixels(pixel, 4, 0);
            Check(pixel[0] == 0 && pixel[3] == 255, "Bitmap alpha repair preserves raw RGB");
            var raw = new byte[900 * 180 * 4];
            new FormatConvertedBitmap(bitmap, PixelFormats.Bgra32, null, 0).CopyPixels(raw, 900 * 4, 0);
            for (int i = 3; i < raw.Length; i += 4) raw[i] = 0;
            var broken = BitmapSource.Create(900, 180, 96, 96, PixelFormats.Bgra32, null, raw, 900 * 4);
            var brokenData = new DataObject(DataFormats.Bitmap, broken);
            var repaired = ClipboardImage.ReadCandidates(brokenData);
            Check(repaired.Count == 2, "Bitmap with alpha has normal and repaired candidates");
            var repairPng = new PngBitmapEncoder(); repairPng.Frames.Add(BitmapFrame.Create(repaired[1]));
            using var repairBytes = new MemoryStream(); repairPng.Save(repairBytes);
            Check(ocr.RecognizeAsync(repairBytes.ToArray(), ["eng"]).GetAwaiter().GetResult().Contains("Hello"), "Real OCR reads text after repairing unset Bitmap alpha");
            var text = ocr.RecognizeAsync(bytes.ToArray(), ["eng"]).GetAwaiter().GetResult();
            Check(text.Contains("Hello") && text.Contains("123"), "Real native OCR recognizes generated screenshot text");
            text = ocr.RecognizeAsync(bytes.ToArray(), ["missing"]).GetAwaiter().GetResult();
            Check(text.Contains("Hello"), "Unavailable language falls back to English");
            if (args.Contains("--network"))
            {
                ocr.DownloadAsync("spa", new Progress<string>(), CancellationToken.None).GetAwaiter().GetResult();
                Check(ocr.Installed("spa"), "Spanish download validates and installs");
                text = ocr.RecognizeAsync(bytes.ToArray(), ["eng", "spa"]).GetAwaiter().GetResult();
                Check(text.Contains("Hello"), "Multiple OCR languages load together");
                ocr.Remove("spa"); Check(!ocr.Installed("spa"), "Downloaded language can be removed");
                using var canceled = new CancellationTokenSource(); canceled.Cancel();
                try { ocr.DownloadAsync("spa", new Progress<string>(), canceled.Token).GetAwaiter().GetResult(); } catch (OperationCanceledException) { }
                Check(!ocr.Installed("spa") && !Directory.GetFiles(ocr.LanguageDirectory, "*.download").Any(), "Canceled download leaves no installed or partial model");
            }
            var resetStore = new StateStore(Path.Combine(folder, "reset"));
            resetStore.State.Settings.HistoryLimit = 20;
            resetStore.State.Settings.Shortcut = "Control+K";
            resetStore.State.Settings.LightTheme = false;
            resetStore.State.Settings.OcrLanguages = ["spa"];
            resetStore.State.Draft = "Keep this draft";
            for (var i = 0; i < 12; i++) resetStore.Remember("History " + i);
            var languageFile = Path.Combine(resetStore.DirectoryPath, "spa.traineddata");
            File.WriteAllText(languageFile, "Installed language");
            Check(!resetStore.RestoreDefaults(_ => false) && resetStore.State.Settings.Shortcut == "Control+K" && resetStore.State.History.Count == 12, "Default-shortcut conflict prevents resetting settings or history");
            Check(resetStore.RestoreDefaults(_ => true), "Restore defaults succeeds with an available shortcut");
            var restored = new StateStore(resetStore.DirectoryPath).State;
            Check(restored.Settings.LightTheme && restored.Settings.Shortcut == "Control+J" && restored.Settings.OcrLanguages.SequenceEqual(new[] { "eng" }) && restored.Settings.HistoryLimit == 10 && restored.History.Count == 10 && restored.History[0].Text == "History 11" && restored.Draft == "Keep this draft" && File.Exists(languageFile), "Restored defaults persist, retaining draft, downloads and newest ten entries");
            UiChecks.Run(folder, bitmap, Check);
            Console.WriteLine($"{passed} checks passed."); return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
        finally { Directory.Delete(folder, true); }
    }
}
