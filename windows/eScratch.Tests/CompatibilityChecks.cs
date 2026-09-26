using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Versioning;
using eScratch;

internal static class CompatibilityChecks
{
    public static void Run(string folder, Action<bool, string> check)
    {
        check(typeof(object).Assembly.GetName().Name == "mscorlib", "Tests run on the .NET Framework CLR, not modern .NET");
        check(typeof(StateStore).Assembly.GetCustomAttribute<TargetFrameworkAttribute>()!.FrameworkName == ".NETFramework,Version=v4.8", "Application targets Framework 4.8 reference assemblies");
        check(Compatibility.ProfileHash(@"C:\eScratch-compatibility-tests") == "1B15FEB1F4C65A96", "Profile mutex identity is unchanged from .NET 10");
        check(Compatibility.CodePoints("A𠀀\uD800B\uDC00").SequenceEqual(new[] { 65, 0x20000, 0xFFFD, 66, 0xFFFD }), "UTF-16 compatibility preserves supplementary and malformed-character counting");

        string destination = Path.Combine(folder, "atomic.txt"), temporary = destination + ".tmp";
        File.WriteAllText(temporary, "first"); Compatibility.ReplaceFile(temporary, destination);
        check(File.ReadAllText(destination) == "first", "Atomic save creates a new destination");
        File.WriteAllText(temporary, "second"); Compatibility.ReplaceFile(temporary, destination);
        check(File.ReadAllText(destination) == "second" && !File.Exists(temporary), "Atomic save replaces an existing destination");
        File.WriteAllText(temporary, "third");
        bool failed = false;
        using (File.Open(destination, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            try { Compatibility.ReplaceFile(temporary, destination); }
            catch (IOException) { failed = true; }
        }
        check(failed && File.ReadAllText(destination) == "second", "Failed replacement preserves the previously saved draft");

        // Same on-disk schema as the current .NET 10 WPF release. No migration
        // or deletion of existing notes/settings/downloads should be necessary.
        var profile = Path.Combine(folder, "previous-wpf"); Directory.CreateDirectory(profile);
        File.WriteAllText(Path.Combine(profile, "state.json"), """
        {"Settings":{"Shortcut":"Control+K","NewShortcut":"Control+T","MarkdownShortcut":"Control+Shift+V","CopyShortcut":"Control+Shift+C","ShowWordCount":false,"AlwaysOnTop":true,"IndentType":"tab","IndentSize":4,"ShowWhitespace":true,"HistoryLimit":25,"LightTheme":false,"OcrLanguages":["eng","jpn"]},"History":[{"Id":"existing-id","Text":"旧笔记 日本語 العربية","CreatedAt":"2026-09-25T12:00:00+04:00"}],"Draft":"Existing draft 中文"}
        """);
        var state = new StateStore(profile);
        check(state.State.Draft == "Existing draft 中文" && state.State.History[0].Id == "existing-id" && state.State.History[0].Text == "旧笔记 日本語 العربية", ".NET 10 draft and history load without data migration");
        state.Save(); var reloaded = new StateStore(profile);
        check(reloaded.State.Settings.Shortcut == "Control+K" && reloaded.State.Settings.AlwaysOnTop && !reloaded.State.Settings.ShowWordCount && !reloaded.State.Settings.LightTheme && reloaded.State.Settings.HistoryLimit == 25 && reloaded.State.Settings.OcrLanguages.SequenceEqual(new[] { "eng", "jpn" }) && reloaded.State.History[0].CreatedAt.Offset == TimeSpan.FromHours(4), "Existing preferences and history timestamps survive a Framework save/reload");
    }
}
