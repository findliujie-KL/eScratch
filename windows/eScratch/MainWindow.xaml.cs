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
    private readonly MarkdownEditor markdownEditor;
    private readonly Func<IDataObject?> readClipboard;
    private readonly Forms.NotifyIcon tray;
    private string? trayIconTheme;
    private readonly TrayFocusTracker trayFocus = new();
    private readonly DispatcherTimer saveTimer = new() { Interval = TimeSpan.FromMilliseconds(500) };
    private readonly TransientNotice statusNotice;
    private Hotkey? hotkey;
    private bool quitting, reading;
    private int zoomPercent = 100;
    private long revision;
    private SettingsWindow? settingsWindow;
    private Action? confirmationAction;
    private UIElement? confirmationReturnFocus;
    public MainWindow(StateStore? stateStore = null, Func<IDataObject?>? clipboardReader = null)
    {
        readClipboard = clipboardReader ?? Clipboard.GetDataObject;
        store = stateStore ?? new StateStore();
        ocr = new OcrService(store.DirectoryPath);
        InitializeComponent();
        PaperBackground.Attach(Editor, store.State.Settings);
        markdownEditor = new MarkdownEditor(Editor);
        statusNotice = new TransientNotice(Status);
        _ = new LiveCalculations(Editor, CalculationOverlay, markdownEditor, result =>
        {
            try { Clipboard.SetText(result); statusNotice.Show("Result copied"); }
            catch (Exception) { statusNotice.Show("Clipboard is busy. Please try again."); }
        });
        Icon = BitmapFrame.Create(new Uri(Path.Combine(AppContext.BaseDirectory, "Assets", "icon.ico")));
        Editor.Text = store.State.Draft;
        Editor.PreviewMouseWheel += ZoomWheel;
        ApplySettings();
        statusNotice.Show(store.Warning ?? "");
        Placeholder.Visibility = Editor.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
        saveTimer.Tick += (_, _) => { saveTimer.Stop(); SaveState(); };
        DataObject.AddPastingHandler(Editor, OnPaste);
        var menu = new ContextMenu();
        var paste = new MenuItem { Header = "Paste", Command = ApplicationCommands.Paste, CommandTarget = Editor.TextArea, InputGestureText = "Ctrl+V" };
        var markdown = new MenuItem { Header = "Paste as Markdown" };
        var numberDivider = new Separator();
        var separators = new MenuItem { Header = "Add thousand separators" };
        var words = new MenuItem { Header = "Convert to words" };
        markdown.Click += (_, _) => PasteMarkdown();
        separators.Click += (_, _) => { if (NumericText.TryAddSeparators(Editor.SelectedText, out var result)) ReplaceNumericSelection(result); };
        words.Click += (_, _) => { if (NumericText.TryToWords(Editor.SelectedText, out var result)) ReplaceNumericSelection(result); };
        menu.Items.Add(paste); menu.Items.Add(markdown); menu.Items.Add(numberDivider); menu.Items.Add(separators); menu.Items.Add(words);
        menu.Items.Add(new Separator());
        var savePinned = new MenuItem { Header = "Save copy as pinned note", InputGestureText = "F12" };
        savePinned.Click += (_, _) => SavePinnedCopy();
        menu.Opened += (_, _) => savePinned.IsEnabled = !string.IsNullOrWhiteSpace(Editor.Text);
        menu.Items.Add(savePinned);
        menu.Items.Add(new Separator());
        foreach (var format in new[] { ("Bold", InlineStyle.Bold, "Ctrl+B"), ("Italic", InlineStyle.Italic, "Ctrl+I"), ("Strikethrough", InlineStyle.Strike, "Ctrl+Shift+X") })
        {
            var item = new MenuItem { Header = format.Item1, InputGestureText = format.Item3 };
            item.Click += (_, _) => FormatSelection(format.Item2);
            menu.Opened += (_, _) => item.IsEnabled = !Editor.IsReadOnly && Editor.SelectionLength > 0;
            menu.Items.Add(item);
        }
        menu.Opened += (_, _) => {
            markdown.InputGestureText = store.State.Settings.MarkdownShortcut.Replace("Control", "Ctrl");
            try { var data = readClipboard(); markdown.IsEnabled = !Editor.IsReadOnly && data != null && (data.GetDataPresent(DataFormats.UnicodeText) || data.GetDataPresent(DataFormats.Html)); }
            catch { markdown.IsEnabled = false; }
            var canFormat = !Editor.IsReadOnly && NumericText.TryAddSeparators(Editor.SelectedText, out _);
            numberDivider.Visibility = separators.Visibility = words.Visibility = canFormat ? Visibility.Visible : Visibility.Collapsed;
            words.IsEnabled = canFormat && NumericText.TryToWords(Editor.SelectedText, out _);
        };
        Editor.ContextMenu = menu;
        // AvalonEdit's default CanPaste only accepts text. Intercept the routed
        // command before that check so image-only Ctrl+V and Shift+Insert work.
        CommandManager.AddPreviewCanExecuteHandler(Editor.TextArea, CanPasteImage);
        CommandManager.AddPreviewExecutedHandler(Editor.TextArea, PasteImageCommand);
        tray = new Forms.NotifyIcon { Icon = new System.Drawing.Icon(Path.Combine(AppContext.BaseDirectory, "Assets", "icon.ico")), Text = "eScratch (WPF)", Visible = true };
        tray.MouseDoubleClick += (_, e) =>
        {
            if (e.Button != Forms.MouseButtons.Left) return;
            Dispatcher.Invoke(() =>
            {
                if (IsVisible && WindowState != WindowState.Minimized &&
                    (IsActive || trayFocus.WasFocused(new WindowInteropHelper(this).Handle))) HideToTray();
                else RestoreWindow();
            });
        };
        Closed += (_, _) => trayFocus.Dispose();
        tray.ContextMenuStrip = new Forms.ContextMenuStrip();
        tray.ContextMenuStrip.Items.Add("Show scratch", null, (_, _) => Dispatcher.Invoke(RestoreWindow));
        tray.ContextMenuStrip.Items.Add("New scratch", null, (_, _) => Dispatcher.Invoke(() => { NewClick(this, new RoutedEventArgs()); ShowPreservingDraft(); }));
        tray.ContextMenuStrip.Items.Add("History", null, (_, _) => Dispatcher.Invoke(() => OpenTrayPage("history")));
        tray.ContextMenuStrip.Items.Add(new Forms.ToolStripSeparator());
        var topItem = new Forms.ToolStripMenuItem("Always on top", null, (_, _) => Dispatcher.Invoke(() => PinClick(this, new RoutedEventArgs())));
        tray.ContextMenuStrip.Items.Add(topItem);
        var appearanceItem = tray.ContextMenuStrip.Items.Add("Switch to dark appearance", null, (_, _) => Dispatcher.Invoke(() => ThemeClick(this, new RoutedEventArgs())));
        tray.ContextMenuStrip.Items.Add(new Forms.ToolStripSeparator());
        tray.ContextMenuStrip.Items.Add("Settings", null, (_, _) => Dispatcher.Invoke(() => OpenTrayPage("settings")));
        tray.ContextMenuStrip.Items.Add("Help & shortcuts", null, (_, _) => Dispatcher.Invoke(() => OpenTrayPage("help")));
        tray.ContextMenuStrip.Items.Add(new Forms.ToolStripSeparator());
        tray.ContextMenuStrip.Items.Add("Quit eScratch", null, (_, _) => Dispatcher.Invoke(Quit));
        tray.ContextMenuStrip.Opening += (_, _) =>
        {

            topItem.Checked = store.State.Settings.AlwaysOnTop;
            appearanceItem.Text = store.State.Settings.LightTheme ? "Switch to dark appearance" : "Switch to light appearance";
        };
        UpdateTrayTooltip();
        UpdateTrayIcon();
        SourceInitialized += (_, _) =>
        {
            hotkey = new Hotkey(new WindowInteropHelper(this).Handle, Toggle);
            if (!hotkey.Register(store.State.Settings.Shortcut)) statusNotice.Show("Shortcut is in use by another app. Choose another shortcut in Settings.");
        };
        StateChanged += (_, _) => { if (WindowState == WindowState.Minimized) HideToTray(); };
        Closing += OnClosing;
        Loaded += (_, _) => { Editor.Focus(); SettingsWindow.PreloadFonts(); };
        Application.Current.SessionEnding += (_, _) => { quitting = true; SaveState(); };
    }
    private void SaveState()
    {
        store.State.Draft = Editor.Text;
        try { store.Save(); } catch (Exception ex) { statusNotice.Show("Could not save: " + ex.Message); }
    }
    private void EditorChanged(object? sender, EventArgs e)
    {
        revision++;
        if (Count == null) return;
        UpdateWordCount();
        Placeholder.Visibility = Editor.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
        saveTimer.Stop(); saveTimer.Start();
    }
    private void ApplySettings()
    {
        var s = store.State.Settings;
        markdownEditor.SetEnabled(s.FormattedView);
        FormattedModeLabel.Visibility = s.FormattedView ? Visibility.Visible : Visibility.Collapsed;
        MarkdownModeLabel.Visibility = s.FormattedView ? Visibility.Collapsed : Visibility.Visible;
        FormatViewButton.ToolTip = s.FormattedView ? "Formatted view · Click for Markdown source" : "Markdown source · Click for formatted view";
        System.Windows.Automation.AutomationProperties.SetName(FormatViewButton, FormatViewButton.ToolTip.ToString());
        FormatViewButton.SetResourceReference(Button.BackgroundProperty, s.FormattedView ? "Active" : "Panel");
        Editor.FontFamily = new FontFamily(s.ScratchFontFamily);
        var alignment = (TextAlignment)Enum.Parse(typeof(TextAlignment), s.ScratchAlignment);
        if (!Equals(Editor.TextArea.TextView.GetValue(TextBlock.TextAlignmentProperty), alignment))
        {
            Editor.TextArea.TextView.SetValue(TextBlock.TextAlignmentProperty, alignment);
            Editor.TextArea.TextView.Redraw();
        }
        ((TextBlock)Placeholder.Children[0]).FontFamily = Editor.FontFamily;
        SetZoom(zoomPercent);
        Topmost = s.AlwaysOnTop;
        PinButton.SetResourceReference(Button.BackgroundProperty, Topmost ? "Active" : "Panel");
        PinButton.SetResourceReference(Button.ForegroundProperty, Topmost ? "Accent" : "Secondary");
        PinButton.ToolTip = Topmost ? "Always on top: on" : "Always on top: off";
        Count.Visibility = s.ShowWordCount ? Visibility.Visible : Visibility.Collapsed;
        UpdateWordCount();
        Editor.Options.ConvertTabsToSpaces = s.IndentType != "tab";
        Editor.Options.IndentationSize = s.IndentSize;
        Editor.Options.ShowSpaces = s.ShowWhitespace;
        Editor.Options.ShowTabs = s.ShowWhitespace;
        Editor.Options.ShowEndOfLine = s.ShowWhitespace;
        var colors = new Dictionary<string, (string Light, string Dark)> {
            ["Surface"] = ("#eff1f5", "#1e1e2e"), ["Panel"] = ("#e6e9ef", "#181825"),
            ["Note"] = ("#fff3cc", "#383328"), ["PinnedNote"] = ("#e8e3f6", "#302d43"),
            ["NoteEdge"] = ("#e6d6a8", "#5b503a"),
            ["Hover"] = ("#ccd0da", "#313244"), ["Active"] = ("#bcc0cc", "#45475a"),
            ["Line"] = ("#ccd0da", "#313244"), ["Ink"] = ("#4c4f69", "#cdd6f4"),
            ["Secondary"] = ("#5c5f77", "#a6adc8"), ["Muted"] = ("#9ca0b0", "#585b70"),
            ["Accent"] = ("#6478b4", "#89b4fa"), ["Danger"] = ("#d20f39", "#f38ba8"), ["Success"] = ("#40a02b", "#a6e3a1")
        };
        foreach (var color in colors) Application.Current.Resources[color.Key] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(s.LightTheme ? color.Value.Light : color.Value.Dark));
        foreach (var color in PaperThemes.Palette(s.PaperTheme, s.LightTheme)) Application.Current.Resources[color.Key] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color.Value));
        PaperBackground.Update(s);
        UpdateTrayIcon();
        NewButton.ToolTip = "New (" + s.NewShortcut.Replace("Control", "Ctrl") + ")";
        UpdateTrayTooltip();
        ShortcutHint.Text = s.Shortcut.Replace("Control", "Ctrl") + " to toggle window · " + s.NewShortcut.Replace("Control", "Ctrl") + " for new scratch · Ctrl+H for history";
        ThemeButton.ToolTip = s.LightTheme ? "Switch to dark theme" : "Switch to light theme";
        ThemeIcon.Data = Geometry.Parse(s.LightTheme ? "M21,12.79 A9,9 0 1 1 11.21,3 A7,7 0 0 0 21,12.79" : "M17,12 A5,5 0 1 1 7,12 A5,5 0 1 1 17,12 M12,1 V3 M12,21 V23 M1,12 H3 M21,12 H23 M4,4 L6,6 M18,18 L20,20 M4,20 L6,18 M18,6 L20,4");
    }
    private void UpdateWordCount()
    {
        var words = WordCounter.Count(Editor.Text);
        Count.Text = $"{words:N0} " + (words == 1 ? "word" : "words");
    }
    private void PinClick(object sender, RoutedEventArgs e)
    {
        store.State.Settings.AlwaysOnTop = !store.State.Settings.AlwaysOnTop;
        ApplySettings(); SaveState();
        if (settingsWindow != null) ((CheckBox)settingsWindow.FindName("TopmostBox")).IsChecked = Topmost;
    }
    private void FormatViewClick(object sender, RoutedEventArgs e)
    {
        store.State.Settings.FormattedView = !store.State.Settings.FormattedView;
        ApplySettings(); SaveState(); Editor.Focus();
    }
    private void FormatSelection(InlineStyle style)
    {
        if (!markdownEditor.Toggle(style)) statusNotice.Show("Select text to format.");
        Editor.Focus();
    }
    private bool TryCopyOnHide()
    {
        if (string.IsNullOrWhiteSpace(Editor.Text)) return true;
        try { Clipboard.SetDataObject(Editor.Text, true); statusNotice.Show("Copied"); return true; }
        catch (Exception ex) { statusNotice.Show("Clipboard is busy. Please try again. " + ex.Message); return false; }
    }
    private void Toggle() { if (IsVisible && WindowState != WindowState.Minimized && IsActive) HideToTray(); else RestoreWindow(); }
    private void UpdateTrayTooltip()
    {
        if (tray == null) return;
        var text = "eScratch\n" + store.State.Settings.Shortcut.Replace("Control", "Ctrl") + " to show / hide";
        tray.Text = text.Length <= 63 ? text : text.Substring(0, 63);
    }
    private void UpdateTrayIcon()
    {
        if (tray == null) return;
        var s = store.State.Settings;
        var key = s.PaperTheme + ":" + s.LightTheme;
        if (trayIconTheme == key) return;
        var next = ThemeTrayIcon.Create(s.PaperTheme, s.LightTheme);
        var previous = tray.Icon;
        tray.Icon = next; trayIconTheme = key;
        previous?.Dispose();
    }
    private void ShowPreservingDraft()
    {
        Show(); WindowState = WindowState.Normal;
        if (store.State.Settings.OpenNearPointer) MoveNearPointer();
        Activate(); Editor.Focus();
    }
    private void OpenTrayPage(string page)
    {
        ShowPreservingDraft(); BackClick(this, new RoutedEventArgs());
        if (page == "history") HistoryClick(this, new RoutedEventArgs());
        else
        {
            SettingsClick(this, new RoutedEventArgs());
            if (page == "help") settingsWindow?.ShowPage("help");
        }
    }
    private void HideToTray() { if (store.State.Settings.CopyOnHide && !TryCopyOnHide()) return; DismissConfirmation(); SaveState(); settingsWindow?.Close(); Hide(); }
    private void RestoreWindow()
    {
        if (!IsVisible || WindowState == WindowState.Minimized) StartResumedEntry();
        ShowPreservingDraft();
    }
    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool SetWindowPos(IntPtr window, IntPtr after, int x, int y, int width, int height, uint flags);
    private void MoveNearPointer()
    {
        var pointer = Forms.Cursor.Position;
        var area = Forms.Screen.FromPoint(pointer).WorkingArea;
        var source = PresentationSource.FromVisual(this);
        var scale = source?.CompositionTarget?.TransformToDevice ?? Matrix.Identity;
        var width = (int)Math.Ceiling(ActualWidth * scale.M11);
        var height = (int)Math.Ceiling(ActualHeight * scale.M22);
        var x = Math.Max(area.Left, Math.Min(pointer.X + 16, area.Right - width));
        var y = Math.Max(area.Top, Math.Min(pointer.Y + 16, area.Bottom - height));
        // Screen and cursor coordinates are pixels; avoid mixing them with WPF logical units.
        SetWindowPos(new WindowInteropHelper(this).Handle, IntPtr.Zero, x, y, 0, 0, 0x0015);
    }
    private void StartResumedEntry()
    {
        if (store.State.Settings.ReopenLastScratch) return;
        try
        {
            // Archive successfully before clearing, so failures never discard a draft.
            store.Remember(Editor.Text);
            revision++; Editor.Clear(); SaveState(); RefreshHistory();
            BackClick(this, new RoutedEventArgs());
        }
        catch (Exception ex) { statusNotice.Show("Could not start a new entry: " + ex.Message); }
    }
    private void OnClosing(object? sender, CancelEventArgs e) { if (!quitting) { e.Cancel = true; HideToTray(); } }
    private void Quit()
    {
        SaveState(); quitting = true; saveTimer.Stop(); statusNotice.Dispose(); hotkey?.Dispose();
        tray.Visible = false; tray.Icon?.Dispose(); tray.Dispose();
        settingsWindow?.Close(); Application.Current.Shutdown();
    }
    private void NewClick(object sender, RoutedEventArgs e)
    {
        try { store.Remember(Editor.Text); Editor.Clear(); SaveState(); BackClick(sender, e); }
        catch (Exception ex) { statusNotice.Show("Could not save history: " + ex.Message); }
    }
    private void SavePinnedCopy()
    {
        if (string.IsNullOrWhiteSpace(Editor.Text)) { statusNotice.Show("Nothing to save yet."); return; }
        try { store.SavePinnedCopy(Editor.Text); RefreshHistory(); statusNotice.Show("Saved a pinned copy"); }
        catch (Exception ex) { statusNotice.Show("Could not save pinned note: " + ex.Message); }
    }
    private void BackClick(object sender, RoutedEventArgs e)
    {
        DismissConfirmation();
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
        if (HistoryList == null || HistorySearch == null) return;
        var query = HistorySearch.Text.Trim();
        var entries = store.State.History.Where(entry => query.Length == 0 ||
            entry.Text.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0).ToList();
        HistoryList.ItemsSource = entries;
        EmptyHistory.Text = store.State.History.Count == 0 ? "Your saved scratches will appear here." : "No matching notes. Try another search.";
        EmptyHistory.Visibility = entries.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        HistorySummary.Text = query.Length == 0 ? $"{entries.Count} saved notes · pinned first" : $"{entries.Count} of {store.State.History.Count} notes";
        ClearHistoryButton.IsEnabled = store.State.History.Any(entry => !entry.Pinned);
    }
    private void SearchHistory(object sender, TextChangedEventArgs e) => RefreshHistory();
    private void ClearHistorySearch(object sender, RoutedEventArgs e) { HistorySearch.Clear(); HistorySearch.Focus(); }
    private void HistoryBoardSizeChanged(object sender, SizeChangedEventArgs e)
    {
        var width = Math.Max(200, HistoryList.ActualWidth - 40);
        var columns = Math.Max(1, (int)(width / 220));
        HistoryList.Tag = Math.Floor(width / columns);
    }
    private void HistoryClick(object sender, RoutedEventArgs e)
    {
        bool wasOpen = HistoryPanel.Visibility == Visibility.Visible;
        BackClick(sender, e);
        if (wasOpen) return;
        RefreshHistory(); HistoryPanel.Visibility = PanelBackdrop.Visibility = Visibility.Visible;
        HistoryButton.SetResourceReference(Button.BackgroundProperty, "Active");
        HistorySearch.Focus();
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
        catch (Exception ex) { statusNotice.Show(ex.Message); }
    }
    private void DeleteClick(object sender, RoutedEventArgs e)
    {
        var entry = (sender as Button)?.Tag as HistoryEntry ?? HistoryList.SelectedItem as HistoryEntry;
        if (entry != null) { store.State.History.RemoveAll(h => h.Id == entry.Id); SaveState(); RefreshHistory(); }
    }
    private void PinHistoryClick(object sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.Tag is not HistoryEntry entry) return;
        store.TogglePin(entry.Id);
        RefreshHistory();
    }
    private void ClearClick(object sender, RoutedEventArgs e)
    {
        if (!store.State.History.Any(entry => !entry.Pinned)) return;
        ShowConfirmation("Clear unpinned history?", "Delete all unpinned notes? Pinned notes and your current draft will be kept.", "Clear unpinned", () =>
        {
            store.State.History.RemoveAll(entry => !entry.Pinned); SaveState(); RefreshHistory();
        }, ClearHistoryButton);
    }
    internal void ShowConfirmation(string title, string message, string actionLabel, Action confirm, UIElement returnFocus)
    {
        if (ConfirmationOverlay.IsVisible) return;
        confirmationAction = confirm;
        confirmationReturnFocus = returnFocus;
        ConfirmationTitle.Text = title;
        ConfirmationMessage.Text = message;
        ConfirmActionButton.Content = actionLabel;
        System.Windows.Automation.AutomationProperties.SetName(ConfirmActionButton, actionLabel);
        ConfirmationOverlay.Visibility = Visibility.Visible;
        CancelConfirmationButton.Focus();
    }
    private void CancelConfirmationClick(object sender, RoutedEventArgs e) => DismissConfirmation();
    private void DismissConfirmation()
    {
        if (ConfirmationOverlay.Visibility != Visibility.Visible) return;
        var returnFocus = confirmationReturnFocus;
        confirmationAction = null;
        confirmationReturnFocus = null;
        ConfirmationOverlay.Visibility = Visibility.Collapsed;
        if (returnFocus?.IsVisible == true) returnFocus.Focus();
        else Editor.Focus();
    }
    private void ConfirmActionClick(object sender, RoutedEventArgs e)
    {
        var action = confirmationAction;
        DismissConfirmation();
        action?.Invoke();
    }
    private void SettingsClick(object sender, RoutedEventArgs e)
    {
        bool wasOpen = settingsWindow != null;
        BackClick(sender, e);
        if (wasOpen) return;
        settingsWindow = new SettingsWindow(store, ocr, shortcut => hotkey?.Register(shortcut) == true,
            () => { ApplySettings(); SaveState(); RefreshHistory(); });
        settingsWindow.Closed += (_, _) => { SettingsContent.Content = null; settingsWindow = null; SettingsPanel.Visibility = PanelBackdrop.Visibility = Visibility.Collapsed; SettingsButton.ClearValue(Button.BackgroundProperty); };
        SettingsContent.Content = settingsWindow;
        settingsWindow.FullPageChanged += full =>
        {
            SettingsPanel.Width = full ? double.NaN : 320;
            SettingsPanel.HorizontalAlignment = full ? HorizontalAlignment.Stretch : HorizontalAlignment.Right;
        };
        SettingsPanel.Width = 320;
        SettingsPanel.HorizontalAlignment = HorizontalAlignment.Right;
        SettingsPanel.Visibility = PanelBackdrop.Visibility = Visibility.Visible;
        SettingsButton.SetResourceReference(Button.BackgroundProperty, "Active");
    }
    private void WindowKeyDown(object sender, KeyEventArgs e)
    {
        if (ConfirmationOverlay.IsVisible)
        {
            if (e.Key == Key.Escape) { DismissConfirmation(); e.Handled = true; }
            return;
        }
        if (e.Key == Key.F1 && Keyboard.Modifiers == ModifierKeys.None)
        {
            if (settingsWindow == null) SettingsClick(sender, e);
            settingsWindow?.ShowPage("help"); e.Handled = true; return;
        }
        if (e.Key == Key.Escape) { if (PanelBackdrop.IsVisible) BackClick(sender, e); else HideToTray(); e.Handled = true; return; }
        if ((e.Key == Key.D0 || e.Key == Key.NumPad0) && Keyboard.Modifiers == ModifierKeys.Control)
        { SetZoom(100); e.Handled = true; return; }
        if (settingsWindow != null) return;
        if (e.Key == Key.F12 && Keyboard.Modifiers == ModifierKeys.None)
        {
            if (!e.IsRepeat) SavePinnedCopy();
            e.Handled = true; return;
        }
        if (Editor.IsKeyboardFocusWithin)
        {
            var modifiers = Keyboard.Modifiers;
            if (modifiers == ModifierKeys.Control && (e.Key == Key.B || e.Key == Key.I))
            { FormatSelection(e.Key == Key.B ? InlineStyle.Bold : InlineStyle.Italic); e.Handled = true; return; }
            if (modifiers == (ModifierKeys.Control | ModifierKeys.Shift) && e.Key == Key.X)
            { FormatSelection(InlineStyle.Strike); e.Handled = true; return; }
            if (markdownEditor.Enabled && modifiers == ModifierKeys.Control && e.Key == Key.X && Editor.SelectionLength > 0 && !Editor.IsReadOnly)
            {
                try { Clipboard.SetText(markdownEditor.SelectedVisibleText()); markdownEditor.ReplaceSelection(""); }
                catch (Exception ex) { statusNotice.Show("Could not cut: " + ex.Message); }
                e.Handled = true; return;
            }
        }
        if ((e.Key == Key.Return || e.Key == Key.Enter) &&
            (Keyboard.Modifiers == ModifierKeys.Control || Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift)) && Editor.IsKeyboardFocusWithin)
        {
            e.Handled = true;
            if (!e.IsRepeat) CalculateCurrentLine(Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift));
            return;
        }
        if (e.Key == Key.H && Keyboard.Modifiers == ModifierKeys.Control)
        { if (!e.IsRepeat) HistoryClick(sender, e); e.Handled = true; return; }
        try
        {
            if (Editor.IsKeyboardFocusWithin && Hotkey.Parse(store.State.Settings.MarkdownShortcut).Matches(this, e)) { PasteMarkdown(); e.Handled = true; }
            else if (Hotkey.Parse(store.State.Settings.NewShortcut).Matches(this, e)) { NewClick(sender, e); e.Handled = true; }
        }
        catch (NotSupportedException) { }
    }
    private void ZoomWheel(object sender, MouseWheelEventArgs e)
    {
        if (Keyboard.Modifiers != ModifierKeys.Control || e.Delta == 0) return;
        SetZoom(zoomPercent + (e.Delta > 0 ? 10 : -10));
        e.Handled = true;
    }
    private void SetZoom(int percent)
    {
        zoomPercent = Math.Max(75, Math.Min(250, percent));
        var scale = zoomPercent / 100.0;
        Editor.FontSize = store.State.Settings.ScratchFontSize * scale;
        if (Placeholder.Children[0] is TextBlock prompt) prompt.FontSize = Editor.FontSize;
        ShortcutHint.FontSize = 11 * scale;
    }
    private void CompleteCalculation() => CalculateCurrentLine(false);
    private void CalculateCurrentLine(bool cycleUnit)
    {
        if (Editor.IsReadOnly || Editor.SelectionLength != 0) return;
        var line = Editor.Document.GetLineByOffset(Editor.CaretOffset);
        var model = markdownEditor.Model;
        if (markdownEditor.Enabled ? model.VisibleOffset(Editor.CaretOffset) != model.VisibleOffset(line.EndOffset) : Editor.CaretOffset != line.EndOffset) { statusNotice.Show("Move the cursor to the end of the expression.", 1); return; }
        var visibleStart = model.VisibleOffset(line.Offset);
        var visibleLength = model.VisibleOffset(line.EndOffset) - visibleStart;
        var current = markdownEditor.Enabled ? model.Visible.Substring(visibleStart, visibleLength) : Editor.Document.GetText(line.Offset, line.Length);
        var success = cycleUnit ? Calculator.TryCycleDisplayUnit(current, out var completedUnit) : Calculator.TryCompleteLine(current, out completedUnit);
        var completed = completedUnit;
        if (!success) { statusNotice.Show("Enter an arithmetic expression to calculate.", 1); return; }
        if (completed == current) return;
        if (markdownEditor.Enabled) { markdownEditor.ReplaceVisible(visibleStart, visibleLength, completed); return; }
        Editor.Document.Replace(line.Offset, line.Length, completed);
        Editor.CaretOffset = line.Offset + completed.Length;
    }
    private void ReplaceNumericSelection(string replacement)
    {
        if (Editor.IsReadOnly || Editor.SelectionLength == 0) return;
        if (markdownEditor.Enabled) { markdownEditor.ReplaceSelection(replacement); Editor.Focus(); return; }
        var start = Editor.SelectionStart;
        Editor.Document.Replace(start, Editor.SelectionLength, replacement);
        Editor.CaretOffset = start + replacement.Length;
        Editor.Focus();
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
            PastePlainText(text);
            statusNotice.Show("Pasted as Markdown");
        }
        catch (Exception ex) { statusNotice.Show("Could not paste as Markdown: " + ex.Message); }
    }
    private void CanPasteImage(object sender, CanExecuteRoutedEventArgs e)
    {
        if (e.Command != ApplicationCommands.Paste) return;
        // Handle every Paste query, including text and clipboard contention, so
        // AvalonEdit's default query cannot throw while another app owns the clipboard.
        e.Handled = true;
        e.CanExecute = false;
        if (Editor.IsReadOnly || reading) return;
        try
        {
            var data = readClipboard();
            e.CanExecute = data != null && (ClipboardImage.IsAvailable(data) || ClipboardImage.TryReadPlainText(data, out _));
        }
        catch (System.Runtime.InteropServices.ExternalException) { }
    }
    private async void PasteImageCommand(object sender, ExecutedRoutedEventArgs e)
    {
        if (e.Command != ApplicationCommands.Paste) return;
        try
        {
            var data = readClipboard();
            if (markdownEditor.Enabled && ClipboardImage.TryReadPlainText(data, out var plain))
            { e.Handled = true; if (!Editor.IsReadOnly) PastePlainText(plain); return; }
            if (!ClipboardImage.IsAvailable(data)) return;
            e.Handled = true;
            if (Editor.IsReadOnly) return;
            if (ClipboardImage.TryReadPlainText(data, out var text)) PastePlainText(text);
            else await PasteImageAsync(data!);
        }
        catch (Exception ex) { e.Handled = true; statusNotice.Show("Could not read the clipboard: " + ex.Message); }
    }
    private async void OnPaste(object sender, DataObjectPastingEventArgs e)
    {
        if (markdownEditor.Enabled && ClipboardImage.TryReadPlainText(e.DataObject, out var plain))
        { e.CancelCommand(); if (!Editor.IsReadOnly) PastePlainText(plain); return; }
        if (!ClipboardImage.IsAvailable(e.DataObject)) return;
        e.CancelCommand();
        if (Editor.IsReadOnly) return;
        if (ClipboardImage.TryReadPlainText(e.DataObject, out var text)) PastePlainText(text);
        else await PasteImageAsync(e.DataObject);
    }
    private void PastePlainText(string text)
    {
        if (markdownEditor.Enabled) { markdownEditor.ReplaceSelection(text); Editor.Focus(); return; }
        var start = Editor.SelectionStart;
        Editor.Document.Replace(start, Editor.SelectionLength, text);
        Editor.CaretOffset = start + text.Length;
        Editor.Focus();
    }
    private async System.Threading.Tasks.Task PasteImageAsync(IDataObject data)
    {
        if (reading) { statusNotice.Show("Reading screenshot…", persistent: true); return; }
        var expectedRevision = revision;
        var start = Editor.SelectionStart; var length = Editor.SelectionLength;
        reading = true; statusNotice.Show("Reading screenshot…", persistent: true);
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
                    text = await ocr.RecognizeAsync(bytes.ToArray(), languages, store.State.Settings.JoinWrappedOcrLines);
                    recognized = true;
                    if (!string.IsNullOrWhiteSpace(text)) break;
                }
                catch (Exception ex) { lastError = ex; }
                if (quitting) return;
            }
            if (!recognized && lastError != null) throw lastError;
            if (quitting) return;
            if (string.IsNullOrWhiteSpace(text)) { statusNotice.Show("No text found. Try a clearer screenshot or another OCR language."); return; }
            if (revision != expectedRevision)
            {
                // Never replace a newer draft or selection while asynchronous OCR is running.
                store.Remember(text); statusNotice.Show("OCR text saved to History because the draft changed while reading.");
                if (HistoryPanel.IsVisible) RefreshHistory();
                return;
            }
            Editor.Select(start, length); PastePlainText(text); statusNotice.Show("Screenshot text inserted");
        }
        catch (Exception ex) { statusNotice.Show("OCR failed: " + ex.Message); }
        finally { reading = false; }
    }
}

