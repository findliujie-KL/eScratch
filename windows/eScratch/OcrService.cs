using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Tesseract;

namespace eScratch;
public sealed record OcrLanguage(string Code, string Name)
{
    public override string ToString() => $"{Name} ({Code})";
}
public sealed class OcrService
{
    private static readonly HttpClient Client = new() { Timeout = TimeSpan.FromMinutes(10) };
    public IReadOnlyList<OcrLanguage> Catalog { get; }
    public string LanguageDirectory { get; }
    public OcrService(string dataDirectory)
    {
        LanguageDirectory = Path.Combine(dataDirectory, "tessdata");
        Directory.CreateDirectory(LanguageDirectory);
        Catalog = JsonSerializer.Deserialize<List<OcrLanguage>>(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Assets", "languages.json")), StateStore.JsonOptions) ?? [];
        if (!Installed("eng")) File.Copy(Path.Combine(AppContext.BaseDirectory, "Assets", "eng.traineddata"), LanguagePath("eng"));
    }
    private string LanguagePath(string code)
    {
        if (!Regex.IsMatch(code, @"^(script/)?[A-Za-z0-9_]+$") || !Catalog.Any(l => l.Code == code)) throw new ArgumentException("Unknown OCR language.");
        return Path.Combine(LanguageDirectory, code + ".traineddata");
    }
    public bool Installed(string code) => File.Exists(LanguagePath(code));
    public void Remove(string code)
    {
        if (code == "eng") throw new InvalidOperationException("English is bundled with the application.");
        File.Delete(LanguagePath(code));
    }
    public async Task DownloadAsync(string code, IProgress<string> progress, CancellationToken cancellation)
    {
        var destination = LanguagePath(code);
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        var temporary = destination + ".download";
        try
        {
            // Only catalog entries can be requested; no clipboard or note content is sent.
            using var response = await Client.GetAsync("https://raw.githubusercontent.com/tesseract-ocr/tessdata_fast/main/" + code + ".traineddata", HttpCompletionOption.ResponseHeadersRead, cancellation);
            response.EnsureSuccessStatusCode();
            var length = response.Content.Headers.ContentLength;
            await using (var input = await response.Content.ReadAsStreamAsync(cancellation))
            await using (var output = File.Create(temporary))
            {
                var buffer = new byte[81920]; long received = 0; int count;
                while ((count = await input.ReadAsync(buffer, cancellation)) != 0)
                {
                    received += count;
                    if (received > 100 * 1024 * 1024) throw new InvalidDataException("Language download exceeded the size limit.");
                    await output.WriteAsync(buffer.AsMemory(0, count), cancellation);
                    progress.Report(length > 0 ? $"Downloading… {received * 100 / length}%" : $"Downloading… {received / 1048576d:0.0} MB");
                }
                if (received < 1024 || (length.HasValue && received != length)) throw new InvalidDataException("Incomplete language download.");
            }
            cancellation.ThrowIfCancellationRequested();
            // Test native parsing before installing the model, preserving any existing file.
            var validationDirectory = Path.Combine(LanguageDirectory, "validate-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(validationDirectory);
            try
            {
                File.Copy(temporary, Path.Combine(validationDirectory, "check.traineddata"));
                await Task.Run(() => { using var engine = new TesseractEngine(validationDirectory, "check", EngineMode.LstmOnly); }, cancellation);
            }
            finally { Directory.Delete(validationDirectory, true); }
            cancellation.ThrowIfCancellationRequested();
            File.Move(temporary, destination, true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
    public Task<string> RecognizeAsync(byte[] image, IEnumerable<string> selected)
    {
        var codes = selected.Where(c => Catalog.Any(l => l.Code == c) && Installed(c)).Distinct().ToArray();
        if (codes.Length == 0) codes = ["eng"];
        return Task.Run(() =>
        {
            // Dispose after each operation to release OCR memory while the app is idle.
            using var engine = new TesseractEngine(LanguageDirectory, string.Join('+', codes), EngineMode.LstmOnly);
            using var pix = Pix.LoadFromMemory(image);
            using var page = engine.Process(pix);
            return page.GetText().Trim();
        });
    }
}
