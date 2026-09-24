using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace eScratch;
public sealed class Settings
{
    public string Shortcut { get; set; } = "Control+J";
    public string NewShortcut { get; set; } = "Control+T";
    public string MarkdownShortcut { get; set; } = "Control+Shift+V";
    public string CopyShortcut { get; set; } = "Control+Shift+C";
    public bool AlwaysOnTop { get; set; }
    public string IndentType { get; set; } = "space";
    public int IndentSize { get; set; } = 2;
    public bool ShowWhitespace { get; set; }
    public int HistoryLimit { get; set; } = 10;
    public bool LightTheme { get; set; } = true;
    public List<string> OcrLanguages { get; set; } = ["eng"];
}
public sealed record HistoryEntry(string Id, string Text, DateTimeOffset CreatedAt)
{
    [JsonIgnore] public string Preview => Text.Replace('\r', ' ').Replace('\n', ' ');
    [JsonIgnore] public string Date => CreatedAt.ToLocalTime().ToString("g");
}
public sealed class AppState
{
    public Settings Settings { get; set; } = new();
    public List<HistoryEntry> History { get; set; } = [];
    public string Draft { get; set; } = "";
}
public sealed class StateStore
{
    public static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true, WriteIndented = true };
    public string DirectoryPath { get; }
    public AppState State { get; private set; } = new();
    public string? Warning { get; private set; }
    public StateStore(string? directory = null)
    {
        DirectoryPath = directory ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "eScratch", "Wpf");
        Directory.CreateDirectory(DirectoryPath);
        var path = Path.Combine(DirectoryPath, "state.json");
        if (File.Exists(path))
        {
            try { State = JsonSerializer.Deserialize<AppState>(File.ReadAllText(path), JsonOptions) ?? throw new JsonException(); }
            catch (JsonException)
            {
                File.Copy(path, path + ".corrupt-" + DateTime.UtcNow.Ticks);
                Warning = "Unreadable saved data was backed up. Defaults have been restored.";
            }
        }
        else if (directory == null) ImportElectron();
        Normalize();
    }
    private void ImportElectron()
    {
        var roaming = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        foreach (var name in new[] { "eScratch", "escratch", "one-time-editor" })
        {
            var folder = Path.Combine(roaming, name);
            if (!File.Exists(Path.Combine(folder, "config.json"))) continue;
            try
            {
                State.Settings = JsonSerializer.Deserialize<Settings>(File.ReadAllText(Path.Combine(folder, "config.json")), JsonOptions) ?? new();
                if (File.Exists(Path.Combine(folder, "history.json")))
                    State.History = JsonSerializer.Deserialize<List<HistoryEntry>>(File.ReadAllText(Path.Combine(folder, "history.json")), JsonOptions) ?? [];
                // Language data differs between engines; English is available immediately.
                State.Settings.OcrLanguages = ["eng"];
                Warning = "Imported Electron settings and history. Additional OCR languages can be downloaded in Settings.";
            }
            catch (JsonException) { Warning = "Electron data could not be imported; the original files are unchanged."; }
            break;
        }
    }
    private void Normalize()
    {
        State.Settings ??= new(); State.History ??= []; State.Draft ??= "";
        var s = State.Settings;
        s.Shortcut = ValidShortcut(s.Shortcut, "Control+J");
        s.NewShortcut = ValidShortcut(s.NewShortcut, "Control+T");
        s.MarkdownShortcut = ValidShortcut(s.MarkdownShortcut, "Control+Shift+V");
        s.CopyShortcut = ValidShortcut(s.CopyShortcut, "Control+Shift+C");
        s.HistoryLimit = Math.Clamp(s.HistoryLimit, 1, 1000);
        if (!new[] { 2, 4, 6, 8 }.Contains(s.IndentSize)) s.IndentSize = 2;
        s.OcrLanguages ??= ["eng"];
        State.History = State.History.Where(h => h != null && !string.IsNullOrWhiteSpace(h.Text)).Take(s.HistoryLimit).ToList();
    }
    private static string ValidShortcut(string? value, string fallback)
    {
        try { if (!string.IsNullOrWhiteSpace(value) && Hotkey.Parse(value).Modifiers != System.Windows.Input.ModifierKeys.None) return value; }
        catch (Exception) { }
        return fallback;
    }
    public void Save()
    {
        Normalize();
        var path = Path.Combine(DirectoryPath, "state.json");
        var temporary = path + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(State, JsonOptions));
        File.Move(temporary, path, true);
    }
    public void Remember(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return;
        State.History.Insert(0, new HistoryEntry(Guid.NewGuid().ToString("N"), text, DateTimeOffset.Now));
        Save();
    }
    public bool RestoreDefaults(Func<string, bool> registerShortcut)
    {
        var defaults = new Settings();
        if (!registerShortcut(defaults.Shortcut)) return false;
        var previous = State.Settings;
        var history = State.History.ToList();
        try { State.Settings = defaults; Save(); }
        catch
        {
            State.Settings = previous; State.History = history;
            registerShortcut(previous.Shortcut);
            throw;
        }
        return true;
    }
}
