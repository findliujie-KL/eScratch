using System;
using System.ComponentModel;
using System.Collections.Generic;
using System.Windows.Controls;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Forms = System.Windows.Forms;

namespace eScratch;
public partial class MainWindow : Window
{
    private readonly StateStore store;
    private readonly OcrService ocr;
    private readonly Func<IDataObject?> readClipboard;
    private readonly Forms.NotifyIcon tray;
    private readonly DispatcherTimer saveTimer = new() { Interval = TimeSpan.FromMilliseconds(500) };
    private readonly DispatcherTimer copyFeedbackTimer = new() { Interval = TimeSpan.FromMilliseconds(1500) };
    private Hotkey? hotkey;
    private bool quitting, reading;
    private long revision;
    private SettingsWindow? settingsWindow;
    public MainWindow(StateStore? stateStore = null, Func<IDataObject?>? clipboardReader = null)
    {
        readClipboard = clipboardReader ?? Clipboard.GetDataObject;
        store = stateStore ?? new StateStore();
        ocr = new OcrService(store.DirectoryPath);
        InitializeComponent();
        Icon = BitmapFrame.Create(new Uri(Path.Combine(AppContext.BaseDirectory, "Assets", "icon.ico")));
        Editor.Text = store.State.Draft;
        ApplySettings();
        Status.Text = store.Warning ?? "";
        Placeholder.Visibility = Editor.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
        saveTimer.Tick += (_, _) => { saveTimer.Stop(); SaveState(); };
        copyFeedbackTimer.Tick += (_, _) => { copyFeedbackTimer.Stop(); CopyButton.ClearValue(Button.ForegroundProperty); if (Status.Text == "Copied") Status.Text = ""; };
        DataObject.AddPastingHandler(Editor, OnPaste);
        var menu = new ContextMenu();
        var paste = new MenuItem { Header = "Paste", Command = ApplicationCommands.Paste, CommandTarget = Editor.TextArea, InputGestureText = "Ctrl+V" };
        var markdown = new MenuItem { Header = "Paste as Markdown" };
        markdown.Click += (_, _) => PasteMarkdown();
        menu.Items.Add(paste); menu.Items.Add(markdown);
        menu.Opened += (_, _) => {
            markdown.InputGestureText = store.State.Settings.MarkdownShortcut.Replace("Control", "Ctrl");
            try { var data = readClipboard(); markdown.IsEnabled = !Editor.IsReadOnly && data != null && (data.GetDataPresent(DataFormats.UnicodeText) || data.GetDataPresent(DataFormats.Html)); }
            catch { markdown.IsEnabled = false; }
        };
        Editor.ContextMenu = menu;
        // AvalonEdit's default CanPaste only accepts text. Intercept the routed
        // command before that check so image-only Ctrl+V and Shift+Insert work.
        CommandManager.AddPreviewCanExecuteHandler(Editor.TextArea, CanPasteImage);
        CommandManager.AddPreviewExecutedHandler(Editor.TextArea, PasteImageCommand);
        tray = new Forms.NotifyIcon { Icon = new System.Drawing.Icon(Path.Combine(AppContext.BaseDirectory, "Assets", "icon.ico")), Text = "eScratch (WPF)", Visible = true };
        tray.DoubleClick += (_, _) => Dispatcher.Invoke(RestoreWindow);
        tray.ContextMenuStrip = new Forms.ContextMenuStrip();
        tray.ContextMenuStrip.Items.Add("Show / Hide eScratch", null, (_, _) => Dispatcher.Invoke(Toggle));
        tray.ContextMenuStrip.Items.Add("Quit eScratch", null, (_, _) => Dispatcher.Invoke(Quit));
        SourceInitialized += (_, _) =>
        {
            hotkey = new Hotkey(new WindowInteropHelper(this).Handle, Toggle);
            if (!hotkey.Register(store.State.Settings.Shortcut)) Status.Text = "Shortcut is in use by another app. Choose another shortcut in Settings.";
        };
        Closing += OnClosing;
        Loaded += (_, _) => Editor.Focus();
        Application.Current.SessionEnding += (_, _) => { quitting = true; SaveState(); };
    }
    private void SaveState()
    {
        store.State.Draft = Editor.Text;
        try { store.Save(); } catch (Exception ex) { Status.Text = "Could not save: " + ex.Message; }
    }
    private void EditorChanged(object? sender, EventArgs e)
    {
        revision++;
        if (Count == null) return;
        Count.Text = $"{Editor.Text.Length:N0} characters";
        Placeholder.Visibility = Editor.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
        saveTimer.Stop(); saveTimer.Start();
    }
    private void ApplySettings()
    {
        var s = store.State.Settings;
        Topmost = s.AlwaysOnTop;
        Editor.Options.ConvertTabsToSpaces = s.IndentType != "tab";
        Editor.Options.IndentationSize = s.IndentSize;
        Editor.Options.ShowSpaces = s.ShowWhitespace;
        Editor.Options.ShowTabs = s.ShowWhitespace;
        Editor.Options.ShowEndOfLine = s.ShowWhitespace;
        var colors = new Dictionary<string, (string Light, string Dark)> {
            ["Surface"] = ("#eff1f5", "#1e1e2e"), ["Panel"] = ("#e6e9ef", "#181825"),
            ["Hover"] = ("#ccd0da", "#313244"), ["Active"] = ("#bcc0cc", "#45475a"),
            ["Line"] = ("#ccd0da", "#313244"), ["Ink"] = ("#4c4f69", "#cdd6f4"),
            ["Secondary"] = ("#5c5f77", "#a6adc8"), ["Muted"] = ("#9ca0b0", "#585b70"),
            ["Accent"] = ("#6478b4", "#89b4fa"), ["Danger"] = ("#d20f39", "#f38ba8"), ["Success"] = ("#40a02b", "#a6e3a1")
        };
        foreach (var color in colors) Application.Current.Resources[color.Key] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(s.LightTheme ? color.Value.Light : color.Value.Dark));
        NewButton.ToolTip = "New (" + s.NewShortcut.Replace("Control", "Ctrl") + ")";
        CopyButton.ToolTip = "Copy (" + s.CopyShortcut.Replace("Control", "Ctrl") + ")";
        ThemeButton.ToolTip = s.LightTheme ? "Switch to dark theme" : "Switch to light theme";
        ThemeIcon.Data = Geometry.Parse(s.LightTheme ? "M21,12.79 A9,9 0 1 1 11.21,3 A7,7 0 0 0 21,12.79" : "M17,12 A5,5 0 1 1 7,12 A5,5 0 1 1 17,12 M12,1 V3 M12,21 V23 M1,12 H3 M21,12 H23 M4,4 L6,6 M18,18 L20,20 M4,20 L6,18 M18,6 L20,4");
    }
    private bool CopyDraft()
    {
        if (string.IsNullOrWhiteSpace(Editor.Text)) return true;
        try { Clipboard.SetDataObject(Editor.Text, true); Status.Text = "Copied"; CopyButton.SetResourceReference(Button.ForegroundProperty, "Success"); copyFeedbackTimer.Stop(); copyFeedbackTimer.Start(); return true; }
        catch (Exception ex) { Status.Text = "Clipboard is busy. Please try again. " + ex.Message; return false; }
    }
    private void Toggle() { if (IsVisible && WindowState != WindowState.Minimized) HideToTray(); else RestoreWindow(); }
    private void HideToTray() { if (!CopyDraft()) return; SaveState(); settingsWindow?.Close(); Hide(); }
    private void RestoreWindow() { Show(); WindowState = WindowState.Normal; Activate(); Editor.Focus(); }
    private void OnClosing(object? sender, CancelEventArgs e) { if (!quitting) { e.Cancel = true; HideToTray(); } }
    private void Quit()
    {
        SaveState(); quitting = true; saveTimer.Stop(); copyFeedbackTimer.Stop(); hotkey?.Dispose();
        tray.Visible = false; tray.Icon?.Dispose(); tray.Dispose();
        settingsWindow?.Close(); Application.Current.Shutdown();
    }
    private void NewClick(object sender, RoutedEventArgs e)
    {
        try { store.Remember(Editor.Text); Editor.Clear(); SaveState(); BackClick(sender, e); }
        catch (Exception ex) { Status.Text = "Could not save history: " + ex.Message; }
    }
    private void CopyClick(object sender, RoutedEventArgs e) => CopyDraft();
    private void BackClick(object sender, RoutedEventArgs e)
    {
        HistoryPanel.Visibility = SettingsPanel.Visibility = PanelBackdrop.Visibility = Visibility.Collapsed;
        settingsWindow?.Close();
        HistoryButton.ClearValue(Button.BackgroundProperty); SettingsButton.ClearValue(Button.BackgroundProperty);
        Editor.Focus();
    }
    private void BackdropClick(object sender, MouseButtonEventArgs e) => BackClick(sender, e);
    private void CloseClick(object sender, RoutedEventArgs e) => Close();
    private void ThemeClick(object sender, RoutedEventArgs e)
    {
        store.State.Settings.LightTheme = !store.State.Settings.LightTheme;
        ApplySettings(); SaveState();
        if (settingsWindow != null) ((CheckBox)settingsWindow.FindName("LightBox")).IsChecked = store.State.Settings.LightTheme;
    }
    private void RefreshHistory()
    {
        HistoryList.ItemsSource = store.State.History.ToList();
        EmptyHistory.Visibility = store.State.History.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }
    private void HistoryClick(object sender, RoutedEventArgs e)
    {
        bool wasOpen = HistoryPanel.Visibility == Visibility.Visible;
        BackClick(sender, e);
        if (wasOpen) return;
        RefreshHistory(); HistoryPanel.Visibility = PanelBackdrop.Visibility = Visibility.Visible;
        HistoryButton.SetResourceReference(Button.BackgroundProperty, "Active");
    }
    private void RestoreEntryClick(object sender, RoutedEventArgs e)
    {
        HistoryList.SelectedItem = ((Button)sender).Tag;
        RestoreClick(sender, e);
    }
    private void HistoryKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) { RestoreClick(sender, e); e.Handled = true; }
        else if (e.Key == Key.Delete) { DeleteClick(sender, e); e.Handled = true; }
    }
    private void RestoreClick(object sender, RoutedEventArgs e)
    {
        if (HistoryList.SelectedItem is not HistoryEntry entry) return;
        try { store.Remember(Editor.Text); Editor.Text = entry.Text; SaveState(); BackClick(sender, e); }
        catch (Exception ex) { Status.Text = ex.Message; }
    }
    private void DeleteClick(object sender, RoutedEventArgs e)
    {
        var entry = (sender as Button)?.Tag as HistoryEntry ?? HistoryList.SelectedItem as HistoryEntry;
        if (entry != null) { store.State.History.RemoveAll(h => h.Id == entry.Id); SaveState(); RefreshHistory(); }
    }
    private void ClearClick(object sender, RoutedEventArgs e)
    {
        if (store.State.History.Count == 0 || MessageBox.Show(this, "Delete all saved history? Your current draft will be kept.", "Clear history", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        store.State.History.Clear(); SaveState(); RefreshHistory();
    }
    private void SettingsClick(object sender, RoutedEventArgs e)
    {
        bool wasOpen = settingsWindow != null;
        BackClick(sender, e);
        if (wasOpen) return;
        settingsWindow = new SettingsWindow(store, ocr, shortcut => hotkey?.Register(shortcut) == true, () => { ApplySettings(); SaveState(); RefreshHistory(); });
        settingsWindow.Closed += (_, _) => { SettingsContent.Content = null; settingsWindow = null; SettingsPanel.Visibility = PanelBackdrop.Visibility = Visibility.Collapsed; SettingsButton.ClearValue(Button.BackgroundProperty); };
        SettingsContent.Content = settingsWindow;
        SettingsPanel.Visibility = PanelBackdrop.Visibility = Visibility.Visible;
        SettingsButton.SetResourceReference(Button.BackgroundProperty, "Active");
    }
    private void WindowKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape) { if (PanelBackdrop.IsVisible) BackClick(sender, e); else HideToTray(); e.Handled = true; return; }
        if (settingsWindow != null) return;
        try
        {
            if (Editor.IsKeyboardFocusWithin && Hotkey.Parse(store.State.Settings.MarkdownShortcut).Matches(this, e)) { PasteMarkdown(); e.Handled = true; }
            else if (Hotkey.Parse(store.State.Settings.NewShortcut).Matches(this, e)) { NewClick(sender, e); e.Handled = true; }
            else if (Hotkey.Parse(store.State.Settings.CopyShortcut).Matches(this, e)) { CopyDraft(); e.Handled = true; }
        }
        catch (NotSupportedException) { }
    }
    private void PasteMarkdown()
    {
        if (Editor.IsReadOnly) return;
        try
        {
            var data = readClipboard();
            if (data == null) return;
            var plain = data.GetData(DataFormats.UnicodeText) as string ?? "";
            var html = data.GetData(DataFormats.Html) as string ?? "";
            if (plain.Length == 0 && html.Length == 0) return;
            var text = MarkdownPaste.Convert(plain, html, data.GetDataPresent(DataFormats.Rtf, false));
            var start = Editor.SelectionStart;
            Editor.Document.Replace(start, Editor.SelectionLength, text);
            Editor.CaretOffset = start + text.Length; Editor.Focus();
            Status.Text = "Pasted as Markdown";
        }
        catch (Exception ex) { Status.Text = "Could not paste as Markdown: " + ex.Message; }
    }
    private void CanPasteImage(object sender, CanExecuteRoutedEventArgs e)
    {
        if (e.Command != ApplicationCommands.Paste) return;
        try
        {
            if (!ClipboardImage.IsAvailable(readClipboard())) return;
            e.CanExecute = !Editor.IsReadOnly && !reading;
            e.Handled = true;
        }
        catch (System.Runtime.InteropServices.ExternalException) { }
    }
    private async void PasteImageCommand(object sender, ExecutedRoutedEventArgs e)
    {
        if (e.Command != ApplicationCommands.Paste) return;
        try
        {
            var data = readClipboard();
            if (!ClipboardImage.IsAvailable(data)) return;
            e.Handled = true;
            if (!Editor.IsReadOnly) await PasteImageAsync(data!);
        }
        catch (Exception ex) { e.Handled = true; Status.Text = "Could not read the clipboard: " + ex.Message; }
    }
    private async void OnPaste(object sender, DataObjectPastingEventArgs e)
    {
        if (!ClipboardImage.IsAvailable(e.DataObject)) return;
        e.CancelCommand();
        if (!Editor.IsReadOnly) await PasteImageAsync(e.DataObject);
    }
    private async System.Threading.Tasks.Task PasteImageAsync(IDataObject data)
    {
        if (reading) { Status.Text = "OCR is already reading an image."; return; }
        var expectedRevision = revision;
        var start = Editor.SelectionStart; var length = Editor.SelectionLength;
        reading = true; Status.Text = "Reading screenshot…";
        try
        {
            var candidates = ClipboardImage.ReadCandidates(data);
            var languages = store.State.Settings.OcrLanguages.ToArray();
            string text = "";
            Exception? lastError = null;
            bool recognized = false;
            foreach (var bitmap in candidates)
            {
                try
                {
                    var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
                    using var bytes = new MemoryStream(); encoder.Save(bytes);
                    text = await ocr.RecognizeAsync(bytes.ToArray(), languages);
                    recognized = true;
                    if (!string.IsNullOrWhiteSpace(text)) break;
                }
                catch (Exception ex) { lastError = ex; }
                if (quitting) return;
            }
            if (!recognized && lastError != null) throw lastError;
            if (quitting) return;
            if (string.IsNullOrWhiteSpace(text)) { Status.Text = "No text found. Try a clearer screenshot or another OCR language."; return; }
            if (revision != expectedRevision)
            {
                // Never replace a newer draft or selection while asynchronous OCR is running.
                store.Remember(text); Status.Text = "OCR text saved to History because the draft changed while reading.";
                if (HistoryPanel.IsVisible) RefreshHistory();
                return;
            }
            Editor.Document.Replace(start, length, text);
            Editor.CaretOffset = start + text.Length; Editor.Focus(); Status.Text = "Screenshot text inserted";
        }
        catch (Exception ex) { Status.Text = "OCR failed: " + ex.Message; }
        finally { reading = false; }
    }
}

