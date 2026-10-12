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
            CompatibilityChecks.Run(folder, Check);
            InlineMarkdownChecks.Run(Check);
            var copyFormats = NumericText.CopyFormats("3.84M");
            Check(copyFormats.Contains("3,840,000") && copyFormats.Contains("3840K") && !copyFormats.Contains("384K"), "Copy formats preserve millions value in full and thousands");
            Check(NumericText.CopyFormats("3840000").Contains("3.84M"), "Plain results can be copied as millions");
            Check(NumericText.CopyFormats("-3.84M").Contains("-3,840,000") && !copyFormats.Contains("0.00384B"), "Copy formats preserve signs and skip units below 0.01");
            Check(NumericText.CopyFormats("48.25%").Contains("0.4825"), "Percentage copy formats preserve value");
            Check(Calculator.TryLiveResult("23+56", out var live) && live == "79", "Live result contains only the computed value");
            Check(Calculator.TryLiveResult("1.5M * 60%", out live) && live == "0.9M", "Live results support units and percentages");
            Check(!Calculator.TryLiveResult("hello", out _) && !Calculator.TryLiveResult("42", out _) && !Calculator.TryLiveResult("1/0", out _) && !Calculator.TryLiveResult("2+", out _), "Live results omit prose, standalone numbers and invalid expressions");
            Check(!Calculator.TryLiveResult("2+3 = 5", out _), "Explicit results do not receive duplicate live results");
            Check(Calculator.TryCompleteLine("35+456", out var sum) && sum == "35+456 = 491", "Calculator appends the requested result");
            Check(Calculator.TryCompleteLine("2*3+1/8", out var product) && product == "2*3+1/8 = 6.13", "Asterisk multiplies and results use at most two decimals");
            Check(Calculator.TryCompleteLine(" (2 + 3) × -4 ", out var grouped) && grouped == " (2 + 3) × -4 = -20", "Calculator handles precedence, parentheses and signs");
            Check(Calculator.TryCompleteLine("0.1 + 0.2", out var decimalSum) && decimalSum == "0.1 + 0.2 = 0.3", "Calculator formats decimal results cleanly");
            Check(Calculator.TryCompleteLine("35+456 = 490", out var updated) && updated == "35+456 = 491", "Updating a result does not append zero decimals");
            Check(Calculator.TryCompleteLine("1/3", out var third) && third == "1/3 = 0.33" && Calculator.TryCompleteLine(third, out var moreDigits) && moreDigits == "1/3 = 0.3333" && Calculator.TryCompleteLine(moreDigits, out var yetMoreDigits) && yetMoreDigits == "1/3 = 0.333333", "Repeated calculations reveal two more digits each time");
            Check(!Calculator.TryCompleteLine("1/0", out _) && !Calculator.TryCompleteLine("hello + 2", out _), "Calculator rejects invalid and nonfinite expressions");
            Check(Calculator.TryCompleteLine("1.5M * 60%", out var millionPercent) && millionPercent == "1.5M * 60% = 0.9M", "Percentage and million suffix retain a readable unit");
            Check(Calculator.TryCompleteLine("150K * 8", out var scaledProduct) && scaledProduct == "150K * 8 = 1.2M", "Calculator promotes large results to a larger unit");
            Check(Calculator.TryCompleteLine("2 hundreds + 3 thousand", out var spelledUnits) && spelledUnits == "2 hundreds + 3 thousand = 3.2K", "Calculator accepts written scale names");
            Check(Calculator.TryCompleteLine(millionPercent, out var millionRepeat) && millionRepeat == "1.5M * 60% = 0.9M", "Repeated calculation does not append zero decimals to a suffixed result");
            Check(Calculator.TryCompleteLine("0.358", out var roundedValue) && roundedValue == "0.358 = 0.36"
                && Calculator.TryCompleteLine(roundedValue, out var preciseValue) && preciseValue == "0.358 = 0.358"
                && Calculator.TryCompleteLine(preciseValue, out var stableValue) && stableValue == preciseValue, "Repeated calculation stops when exact decimals are visible");
            Check(Calculator.TryCompleteLine("0.00001", out var tinyRounded) && tinyRounded == "0.00001 = 0"
                && Calculator.TryCompleteLine(tinyRounded, out var tinyPrecise) && tinyPrecise == "0.00001 = 0.00001", "Repeated calculation finds meaningful small decimals");
            Check(Calculator.TryCycleDisplayUnit("0.4825 = 0.48", out var percentValue) && percentValue == "0.4825 = 48.25%"
                && Calculator.TryCycleDisplayUnit(percentValue, out var actualValue) && actualValue == "0.4825 = 0.48", "Unit cycle initially shows two decimals for sub-100 values");
            Check(Calculator.TryCycleDisplayUnit("123/231 = 0.53", out var roundedPercent) && roundedPercent == "123/231 = 53.25%"
                && Calculator.TryCompleteLine(roundedPercent, out var morePercent) && morePercent == "123/231 = 53.2468%", "Percentage cycle rounds to two decimals and can reveal more");
            Check(Calculator.TryCycleDisplayUnit("560 = 560", out var kiloValue) && kiloValue == "560 = 0.56K", "Unit cycle offers K above 100");
            Check(Calculator.TryCycleDisplayUnit("35000 = 35K", out var millionValue) && millionValue == "35000 = 0.035M", "Unit cycle preserves value in M");
            Check(Calculator.TryCycleDisplayUnit("12321*4564 = 56.233M", out var billionValue) && billionValue == "12321*4564 = 0.0562B"
                && Calculator.TryCompleteLine(billionValue, out var moreBillion) && moreBillion == "12321*4564 = 0.056233B", "Scaled unit starts at four decimals and can reveal more");
            Check(Calculator.TryCycleDisplayUnit("5821230 = 5.82123M", out var noBillion) && noBillion == "5821230 = 5,821,230", "Unit cycle skips B below 0.01B");
            Check(NumericText.TryAddSeparators("1234567.89", out var groupedNumber) && groupedNumber == "1,234,567.89", "Selected numbers gain thousand separators");
            Check(NumericText.TryAddSeparators("1.5M", out var expandedNumber) && expandedNumber == "1,500,000", "Selected abbreviated numbers expand with separators");
            Check(NumericText.TryToWords("1234", out var numberWords) && numberWords == "one thousand two hundred thirty-four", "Selected numbers convert to English words");
            Check(NumericText.TryToWords("1.5M", out var millionWords) && millionWords == "one million five hundred thousand", "Selected abbreviated numbers convert to expanded words");
            Check(NumericText.TryToWords("60%", out var percentWords) && percentWords == "sixty percent", "Percentages convert to words");
            Check(!NumericText.TryAddSeparators("hello", out _), "Numeric actions reject text selections");
            Check(OcrTextCleaner.Clean("这 种 变 化", false) == "这种变化", "OCR removes spaces only between Chinese characters");
            Check(OcrTextCleaner.Clean("中 文 English words 123 中 文", false) == "中文 English words 123 中文", "OCR preserves English and number spacing");
            Check(OcrTextCleaner.Clean("变 化 ， 风 险 。 English 123", false) == "变化，风险。 English 123", "OCR removes spurious spaces around Chinese punctuation");
            Check(OcrTextCleaner.Clean("控制 ? 如果", false) == "控制?如果", "OCR removes spaces around punctuation in Chinese text");
            Check(OcrTextCleaner.Clean("这 种\n变 化\n\nEnglish words\ncontinue here", true) == "这种变化\n\nEnglish words continue here", "OCR joins wrapped lines while preserving paragraphs");
            Check(OcrTextCleaner.Clean("第一项\n2. 第二项", true) == "第一项\n2. 第二项", "OCR preserves numbered list lines");
            Check(OcrTextCleaner.Clean("句子。\n下一句\n\nEnglish.\nNext", true) == "句子。下一句\n\nEnglish. Next", "OCR joins sentences within a paragraph");
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
            Check(!store.State.Settings.CopyOnHide, "Copy on hide defaults off");
            store.State.Settings.CopyOnHide = true; store.Save();
            Check(new StateStore(folder).State.Settings.CopyOnHide, "Copy on hide setting persists");
            Check(store.RestoreDefaults(_ => true) && !store.State.Settings.CopyOnHide, "Restore defaults turns copy on hide off");
            var pinStore = new StateStore(Path.Combine(folder, "pins"));
            pinStore.State.Settings.HistoryLimit = 2; pinStore.Save();
            pinStore.Remember("Pinned draft"); var pinnedId = pinStore.State.History[0].Id;
            pinStore.TogglePin(pinnedId);
            pinStore.Remember("Ordinary one"); pinStore.Remember("Ordinary two"); pinStore.Remember("Ordinary three");
            Check(pinStore.State.History.Count == 3 && pinStore.State.History[0].Id == pinnedId && pinStore.State.History[0].Pinned, "Pinned history survives automatic trimming and stays first");
            Check(new StateStore(pinStore.DirectoryPath).State.History[0].Pinned, "Pinned history survives restart");
            pinStore.TogglePin(pinnedId);
            Check(!pinStore.State.History.Any(entry => entry.Id == pinnedId), "Unpinned old history is subject to the limit");
            pinStore.State.History.Clear(); pinStore.Save();
            Check(new StateStore(pinStore.DirectoryPath).State.History.Count == 0, "Clear all removes pinned history too");
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
                using var interrupted = new CancellationTokenSource();
                bool interruptedDownload = false;
                try { ocr.DownloadAsync("spa", new ImmediateProgress(_ => interrupted.Cancel()), interrupted.Token).GetAwaiter().GetResult(); }
                catch (OperationCanceledException) { interruptedDownload = true; }
                Check(interruptedDownload && !ocr.Installed("spa") && !Directory.GetFiles(ocr.LanguageDirectory, "*.download").Any(), "Canceling an active Framework download releases the response and removes partial data");
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
    private sealed class ImmediateProgress(Action<string> report) : IProgress<string>
    {
        public void Report(string value) => report(value);
    }
}
