using System;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace eScratch;
public partial class SettingsWindow : UserControl
{
    public event EventHandler? Closed;
    public void Close() { feedbackNotice.Dispose(); downloadNotice.Dispose(); download?.Cancel(); Closed?.Invoke(this, EventArgs.Empty); }
    private readonly StateStore store;
    private readonly OcrService ocr;
    private readonly Func<string, bool> registerShortcut;
    private readonly Action changed;
    private CancellationTokenSource? download;
    private bool filtering;
    private readonly TransientNotice feedbackNotice;
    private readonly TransientNotice downloadNotice;
    public SettingsWindow(StateStore store, OcrService ocr, Func<string, bool> registerShortcut, Action changed)
    {
        this.store = store; this.ocr = ocr; this.registerShortcut = registerShortcut; this.changed = changed;
        InitializeComponent();
        feedbackNotice = new TransientNotice(Feedback);
        downloadNotice = new TransientNotice(DownloadStatus);
        var s = store.State.Settings;
        try { LoginBox.IsChecked = LoginStartup.Enabled; } catch (Exception ex) { LoginBox.IsEnabled = false; feedbackNotice.Show("Could not read startup setting: " + ex.Message); }
        WordCountBox.IsChecked = s.ShowWordCount; TopmostBox.IsChecked = s.AlwaysOnTop; LightBox.IsChecked = s.LightTheme; WhitespaceBox.IsChecked = s.ShowWhitespace;
        HistoryLimitBox.Text = s.HistoryLimit.ToString();
        IndentBox.SelectedIndex = s.IndentType == "tab" ? 1 : 0; IndentSizeBox.SelectedIndex = s.IndentSize / 2 - 1;
        ToggleBox.Text = s.Shortcut; NewBox.Text = s.NewShortcut; CopyBox.Text = s.CopyShortcut; MarkdownBox.Text = s.MarkdownShortcut;
        LanguagePicker.ItemsSource = ocr.Catalog;
        LanguagePicker.AddHandler(TextBoxBase.TextChangedEvent, new TextChangedEventHandler(SearchLanguages));
        RefreshInstalled();
        Closed += (_, _) => download?.Cancel();
    }
    private void RecordShortcut(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Tab) return;
        e.Handled = true;
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt or Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin) return;
        if (Keyboard.Modifiers == ModifierKeys.None) { feedbackNotice.Show("Include Ctrl, Alt, Shift or Windows in a shortcut."); return; }
        try { ((TextBox)sender).Text = new KeyGestureConverter().ConvertToInvariantString(new KeyGesture(key, Keyboard.Modifiers))!; feedbackNotice.Show(""); }
        catch (Exception) { feedbackNotice.Show("That key combination cannot be used."); }
    }
    private void RestoreDefaultsClick(object sender, RoutedEventArgs e)
    {
        if (MessageBox.Show(Window.GetWindow(this),
            "Restore all settings to their defaults? This turns off Start with Windows and resets shortcuts, the light theme, indentation, OCR selection to English, and the history limit to 10. Only the newest 10 history entries will be kept. Your current draft and downloaded OCR languages will be preserved.",
            "Restore defaults", MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No) != MessageBoxResult.Yes) return;
        try
        {
            if (!store.RestoreDefaults(registerShortcut)) { feedbackNotice.Show("The default shortcut is in use by another app. Close that app and retry. No settings were changed."); return; }
            LoginStartup.SetEnabled(false);
            changed(); Close();
        }
        catch (Exception ex) { feedbackNotice.Show("Could not restore defaults: " + ex.Message); }
    }
    private void SaveClick(object sender, RoutedEventArgs e)
    {
        if (!int.TryParse(HistoryLimitBox.Text, out var limit) || limit < 1 || limit > 1000) { feedbackNotice.Show("Enter a history limit between 1 and 1000."); return; }
        try
        {
            var shortcuts = new[] { ToggleBox.Text, NewBox.Text, CopyBox.Text, MarkdownBox.Text }.Select(Hotkey.Parse).ToArray();
            if (shortcuts[3].Key == Key.V && shortcuts[3].Modifiers == ModifierKeys.Control) { feedbackNotice.Show("Ctrl+V is reserved for normal Paste."); return; }
            if (shortcuts.Select(k => (k.Key, k.Modifiers)).Distinct().Count() != 4) { feedbackNotice.Show("Choose different shortcuts for each action."); return; }
        }
        catch { feedbackNotice.Show("Record a valid shortcut in each field."); return; }
        if (limit < store.State.History.Count && MessageBox.Show(Window.GetWindow(this), $"Keep only the newest {limit} history entries? Older entries will be removed.", "Reduce history", MessageBoxButton.YesNo) != MessageBoxResult.Yes) return;
        if (!registerShortcut(ToggleBox.Text)) { feedbackNotice.Show("That global shortcut is already in use. Please choose another."); return; }
        var s = store.State.Settings;
        s.Shortcut = ToggleBox.Text; s.NewShortcut = NewBox.Text; s.CopyShortcut = CopyBox.Text; s.MarkdownShortcut = MarkdownBox.Text;
        s.ShowWordCount = WordCountBox.IsChecked == true; s.AlwaysOnTop = TopmostBox.IsChecked == true; s.LightTheme = LightBox.IsChecked == true; s.ShowWhitespace = WhitespaceBox.IsChecked == true;
        s.HistoryLimit = limit; s.IndentType = IndentBox.SelectedIndex == 1 ? "tab" : "space"; s.IndentSize = (IndentSizeBox.SelectedIndex + 1) * 2;
        try { if (LoginBox.IsEnabled && (LoginBox.IsChecked == true) != LoginStartup.Enabled) LoginStartup.SetEnabled(LoginBox.IsChecked == true); store.Save(); changed(); Close(); } catch (Exception ex) { feedbackNotice.Show("Could not save: " + ex.Message); }
    }
    private void SearchLanguages(object sender, TextChangedEventArgs e)
    {
        if (filtering || e.OriginalSource is not TextBox textBox) return;
        if (LanguagePicker.SelectedItem is OcrLanguage selected && textBox.Text == selected.ToString()) return;
        filtering = true;
        var text = textBox.Text; var position = textBox.CaretIndex;
        LanguagePicker.SelectedItem = null;
        LanguagePicker.ItemsSource = ocr.Catalog.Where(l => l.ToString().IndexOf(text, StringComparison.OrdinalIgnoreCase) >= 0).ToList();
        LanguagePicker.IsDropDownOpen = true;
        textBox.Text = text; textBox.CaretIndex = position;
        filtering = false;
    }
    private void RefreshInstalled()
    {
        InstalledLanguages.Children.Clear();
        store.State.Settings.OcrLanguages = store.State.Settings.OcrLanguages.Where(c => ocr.Catalog.Any(l => l.Code == c) && ocr.Installed(c)).ToList();
        if (store.State.Settings.OcrLanguages.Count == 0) store.State.Settings.OcrLanguages.Add("eng");
        foreach (var language in ocr.Catalog.Where(l => ocr.Installed(l.Code)))
        {
            var row = new DockPanel();
            if (language.Code != "eng")
            {
                var remove = new Button { Content = "Remove", FontSize = 12 };
                DockPanel.SetDock(remove, Dock.Right);
                remove.Click += (_, _) =>
                {
                    try { ocr.Remove(language.Code); store.State.Settings.OcrLanguages.Remove(language.Code); RefreshInstalled(); store.Save(); }
                    catch (Exception ex) { downloadNotice.Show(ex.Message); }
                };
                row.Children.Add(remove);
            }
            var check = new CheckBox { Content = language.Name, IsChecked = store.State.Settings.OcrLanguages.Contains(language.Code), VerticalAlignment = VerticalAlignment.Center };
            check.Click += (_, _) =>
            {
                var selected = store.State.Settings.OcrLanguages;
                if (check.IsChecked == true) { if (!selected.Contains(language.Code)) selected.Add(language.Code); }
                else if (selected.Count == 1) { check.IsChecked = true; downloadNotice.Show("Keep at least one OCR language selected."); return; }
                else selected.Remove(language.Code);
                try { store.Save(); } catch (Exception ex) { downloadNotice.Show(ex.Message); }
            };
            row.Children.Add(check); InstalledLanguages.Children.Add(row);
        }
    }
    private async void DownloadClick(object sender, RoutedEventArgs e)
    {
        if (LanguagePicker.SelectedItem is not OcrLanguage language) { downloadNotice.Show("Search and select a language from the list first."); return; }
        if (ocr.Installed(language.Code)) { downloadNotice.Show("That language is already installed."); return; }
        download = new CancellationTokenSource();
        DownloadButton.IsEnabled = false; LanguagePicker.IsEnabled = false; CancelDownloadButton.IsEnabled = true;
        try
        {
            await ocr.DownloadAsync(language.Code, new Progress<string>(s => downloadNotice.Show(s, persistent: true)), download.Token);
            RefreshInstalled(); downloadNotice.Show($"{language.Name} installed. Select its checkbox to use it.");
        }
        catch (OperationCanceledException) { downloadNotice.Show("Download canceled."); }
        catch (Exception ex) { downloadNotice.Show("Download failed. You can retry. " + ex.Message); }
        finally { download.Dispose(); download = null; DownloadButton.IsEnabled = true; LanguagePicker.IsEnabled = true; CancelDownloadButton.IsEnabled = false; }
    }
    private void CancelDownloadClick(object sender, RoutedEventArgs e) => download?.Cancel();
}

