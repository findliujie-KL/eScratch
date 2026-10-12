using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using ICSharpCode.AvalonEdit;
using eScratch;

internal static class UiChecks
{
    public static void Run(string directory, BitmapSource image, Action<bool, string> check)
    {
        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        app.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("pack://application:,,,/eScratch;component/Theme.xaml") });
        var noticeTarget = new TextBlock();
        using (var notice = new TransientNotice(noticeTarget))
        {
            var defaultNoticeClock = Stopwatch.StartNew();
            notice.Show("Temporary");
            PumpUntil(() => noticeTarget.Text == "");
            check(defaultNoticeClock.Elapsed >= TimeSpan.FromMilliseconds(800) && defaultNoticeClock.Elapsed < TimeSpan.FromSeconds(2), "Temporary notices expire after one second by default");
            notice.Show("Done", 0.03);
            PumpUntil(() => noticeTarget.Text == "");
            check(noticeTarget.Text == "", "Completed notices expire");
            notice.Show("Old", 0.03);
            notice.Show("Working", persistent: true);
            var until = DateTime.UtcNow.AddMilliseconds(80);
            PumpUntil(() => DateTime.UtcNow >= until);
            check(noticeTarget.Text == "Working", "Old expiry cannot clear new progress");
            notice.Show("Failed", 0.03);
            PumpUntil(() => noticeTarget.Text == "");
            check(noticeTarget.Text == "", "Completed error replaces progress and expires");
        }
        var store = new StateStore(Path.Combine(directory, "ui"));
        IDataObject clipboardData = new DataObject(DataFormats.Bitmap, image);
        bool clipboardBusy = false;
        var window = new MainWindow(store, () => clipboardBusy ? throw new System.Runtime.InteropServices.COMException("Clipboard busy", unchecked((int)0x800401D0)) : clipboardData);
        try
        {
            check(!window.ShowInTaskbar, "Editor has no taskbar button");
            check(store.State.Settings.LightTheme && store.State.Settings.PaperTheme == "Book paper" && window.Background is SolidColorBrush brush && brush.Color.R == 0xf6, "WPF defaults to Book paper light");
            ((Button)window.FindName("ThemeButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            check(!new StateStore(store.DirectoryPath).State.Settings.LightTheme && ((SolidColorBrush)window.Background).Color.R == 0x28, "Toolbar theme toggle applies and persists dark mode");
            ((Button)window.FindName("ThemeButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            ((Button)window.FindName("HistoryButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            check(((Border)window.FindName("HistoryPanel")).Visibility == Visibility.Visible, "Toolbar opens history inside the editor");
            ((Button)window.FindName("SettingsButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            check(((Border)window.FindName("HistoryPanel")).Visibility == Visibility.Collapsed && ((ContentControl)window.FindName("SettingsContent")).Content is SettingsWindow, "Settings replaces history with an embedded panel");
            var settingsView = (SettingsWindow)((ContentControl)window.FindName("SettingsContent")).Content;
            check(!((ComboBox)settingsView.FindName("FontPicker")).IsEnabled && ((TextBlock)settingsView.FindName("FontLoadingText")).Visibility == Visibility.Visible,
                "Settings opens before font scanning completes");
            check(((TextBlock)settingsView.FindName("VersionText")).Text.Contains("eScratch version " + typeof(SettingsWindow).Assembly.GetName().Version?.ToString(3)), "Settings shows the software version");
            check(settingsView.FindName("CopyBox") == null, "Settings no longer offers a manual copy shortcut");
            ((Button)window.FindName("SettingsButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            check(((Border)window.FindName("PanelBackdrop")).Visibility == Visibility.Collapsed, "Clicking Settings again dismisses the panel");
            var editor = (TextEditor)window.FindName("Editor");
            var setZoom = typeof(MainWindow).GetMethod("SetZoom", BindingFlags.Instance | BindingFlags.NonPublic)!;
            setZoom.Invoke(window, new object[] { 150 });
            check(editor.FontSize == 21 && ((TextBlock)((StackPanel)window.FindName("Placeholder")).Children[0]).FontSize == 21, "Zoom scales editor text and empty-state hint together");
            setZoom.Invoke(window, new object[] { 20 }); check(editor.FontSize == 10.5, "Zoom stops at 75 percent");
            setZoom.Invoke(window, new object[] { 300 }); check(editor.FontSize == 35, "Zoom stops at 250 percent");
            setZoom.Invoke(window, new object[] { 100 }); check(editor.FontSize == 14, "Reset zoom restores 100 percent");
            var inlineEditor = (MarkdownEditor)typeof(MainWindow).GetField("markdownEditor", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(window)!;
            editor.Text = "**hello**";
            ((Button)window.FindName("FormatViewButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            check(inlineEditor.Enabled && editor.Text == "**hello**", "Formatted view preserves the exact Markdown source");
            editor.Select(4, 0); editor.TextArea.PerformTextInput("NEW");
            check(editor.Text == "**heNEWllo**", "Actual editor typing within hidden bold markers preserves bold");
            editor.Undo(); check(editor.Text == "**hello**", "Formatted typing is undoable");
            editor.Select(0, editor.Text.Length); editor.TextArea.PerformTextInput("world");
            check(editor.Text == "**world**", "Typing over a selected bold span preserves its style");
            editor.Select(editor.Text.Length, 0); editor.TextArea.PerformTextInput(" plain");
            check(editor.Text == "**world plain**", "Typing after a bold span continues bold");
            editor.Select(0, 0); inlineEditor.Delete(false);
            check(editor.Text == "**orld plain**", "Forward delete removes a visible character rather than a hidden marker");
            editor.Text = "**hello**"; editor.Select(editor.Text.Length, 0); editor.CaretOffset = editor.Text.Length;
            editor.TextArea.PerformTextInput(" "); editor.TextArea.PerformTextInput("again");
            check(editor.Text == "**hello again**", "Formatting continues when a space and following word are typed separately");
            editor.Text = "*hello*"; editor.Select(0, 0); editor.CaretOffset = 0; editor.TextArea.PerformTextInput("before");
            check(editor.Text == "before*hello*", "Typing before a formatted phrase stays plain");
            editor.Text = "hello"; editor.Select(0, 5);
            inlineEditor.Toggle(InlineStyle.Bold);
            check(editor.Text == "**hello**", "Bold shortcut action writes Markdown delimiters");
            inlineEditor.Toggle(InlineStyle.Bold);
            check(editor.Text == "hello", "Repeated formatting action toggles it off");
            editor.Text = "**hello** "; editor.Select(0, editor.Text.Length);
            inlineEditor.Toggle(InlineStyle.Bold);
            check(editor.Text == "hello ", "Bold toggles off even when the selection includes trailing plain whitespace");
            editor.Text = "***hello***"; editor.Select(5, 0); editor.CaretOffset = 5;
            inlineEditor.Toggle(InlineStyle.Bold);
            check(editor.Text == "*hello*" && editor.SelectionLength == 0 && inlineEditor.Model.VisibleOffset(editor.CaretOffset) == 2,
                "Caret inside bold removes bold from the phrase, retaining italic and caret position");
            editor.Undo();
            check(editor.Text == "***hello***", "Removing bold at the caret is undoable");
            editor.Text = "**35+456**"; editor.Select(editor.Text.Length, 0); editor.CaretOffset = editor.Text.Length;
            typeof(MainWindow).GetMethod("CompleteCalculation", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(window, null);
            check(editor.Text == "**35+456 = 491**", "Calculator works on a formatted expression");
            editor.Text = "**old** tail"; editor.Select(0, 7);
            clipboardData = new DataObject(DataFormats.UnicodeText, "new");
            ApplicationCommands.Paste.Execute(null, editor.TextArea);
            check(editor.Text == "**new** tail", "Normal Paste replaces selected formatted content and retains formatting");
            clipboardData = new DataObject(DataFormats.Bitmap, image);
            editor.Text = "**Bold** and *italic* and ~~deleted~~\n# Heading stays plain\n- List markers stay visible";
            editor.Measure(new Size(640, 240)); editor.Arrange(new Rect(0, 0, 640, 240)); editor.UpdateLayout();
            editor.TextArea.TextView.EnsureVisualLines();
            check(editor.TextArea.TextView.VisualLines.SelectMany(l => l.Elements).Any(el => el.TextRunProperties.Typeface.Weight == FontWeights.Bold),
                "Formatted editor renders bold text through the existing text view");
            check(editor.TextArea.TextView.VisualLines.SelectMany(l => l.Elements).Any(el => el.TextRunProperties.TextDecorations?.Contains(TextDecorations.Strikethrough[0]) == true),
                "Formatted editor renders strikethrough");
            var formattedPreview = Environment.GetEnvironmentVariable("ESCRATCH_MARKDOWN_PREVIEW");
            if (!string.IsNullOrEmpty(formattedPreview))
            {
                var view = (FrameworkElement)window.Content;
                view.Measure(new Size(700, 420)); view.Arrange(new Rect(0, 0, 700, 420)); view.UpdateLayout();
                var bitmap = new RenderTargetBitmap(700, 420, 96, 96, PixelFormats.Pbgra32); bitmap.Render(view);
                var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
                using var output = File.Create(formattedPreview); encoder.Save(output);
            }
            ((Button)window.FindName("FormatViewButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            check(!inlineEditor.Enabled && editor.Text.StartsWith("**Bold**"), "Source toggle reveals original Markdown without rewriting it");
            editor.Text = "Keep this line\n35+456\nAnd this one";
            editor.CaretOffset = editor.Text.IndexOf("35+456", StringComparison.Ordinal) + "35+456".Length;
            typeof(MainWindow).GetMethod("CompleteCalculation", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(window, null);
            check(editor.Text == "Keep this line\n35+456 = 491\nAnd this one", "Calculator completes only the current line");
            editor.Text = "ordinary note"; editor.CaretOffset = editor.Text.Length;
            var noticeClock = Stopwatch.StartNew();
            typeof(MainWindow).GetMethod("CompleteCalculation", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(window, null);
            var calculatorNotice = (TextBlock)window.FindName("Status");
            check(calculatorNotice.Text == "Enter an arithmetic expression to calculate.", "Invalid calculation shows a helpful notice");
            PumpUntil(() => calculatorNotice.Text == "");
            check(noticeClock.Elapsed < TimeSpan.FromSeconds(2), "Calculator error notice expires after about one second");
            editor.Text = "Hello, world! Don't stop. 123";
            check(((TextBlock)window.FindName("Count")).Text == "5 words", "Live word count handles punctuation and contractions");
            editor.Text = "你好世界"; check(((TextBlock)window.FindName("Count")).Text == "4 words", "Live word count includes Chinese characters");
            editor.Clear(); check(((TextBlock)window.FindName("Count")).Text == "0 words", "Empty editor has zero words");
            ((Button)window.FindName("PinButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            check(window.Topmost && new StateStore(store.DirectoryPath).State.Settings.AlwaysOnTop, "Toolbar pin applies and persists Always on Top");
            ((Button)window.FindName("PinButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            check(!window.Topmost, "Toolbar pin toggles off");
            store.State.Settings.ShowWordCount = false; store.Save();
            typeof(MainWindow).GetMethod("ApplySettings", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(window, null);
            check(((TextBlock)window.FindName("Count")).Visibility == Visibility.Collapsed && !new StateStore(store.DirectoryPath).State.Settings.ShowWordCount, "Word count can be hidden and preference persists");
            store.State.Settings.ShowWordCount = true;
            typeof(MainWindow).GetMethod("ApplySettings", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(window, null);
            editor.Text = "Previous draft";
            check(!store.State.Settings.ReopenLastScratch && !store.State.Settings.OpenNearPointer, "Resume options default off");
            store.State.Settings.ReopenLastScratch = true;
            var beforeResumeCount = store.State.History.Count;
            typeof(MainWindow).GetMethod("StartResumedEntry", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(window, null);
            check(editor.Text == "Previous draft" && store.State.History.Count == beforeResumeCount, "Reopen preserves draft without adding history");
            store.State.Settings.ReopenLastScratch = false;
            typeof(MainWindow).GetMethod("StartResumedEntry", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(window, null);
            check(editor.Text == "" && new StateStore(store.DirectoryPath).State.History[0].Text == "Previous draft", "Resume archives draft before starting blank entry");
            var historyCount = store.State.History.Count;
            typeof(MainWindow).GetMethod("StartResumedEntry", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(window, null);
            check(store.State.History.Count == historyCount, "Resuming blank entry does not add history");
            var historyPinButton = new Button { Tag = store.State.History[0] };
            typeof(MainWindow).GetMethod("PinHistoryClick", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(window, new object[] { historyPinButton, new RoutedEventArgs() });
            check(store.State.History[0].Pinned && new StateStore(store.DirectoryPath).State.History[0].Pinned, "History pin action persists the selected item");
            historyPinButton.Tag = store.State.History[0];
            typeof(MainWindow).GetMethod("PinHistoryClick", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(window, new object[] { historyPinButton, new RoutedEventArgs() });
            check(!store.State.History[0].Pinned, "History pin action can unpin an item");
            clipboardBusy = true;
            check(!ApplicationCommands.Paste.CanExecute(null, editor.TextArea), "Busy clipboard disables Paste without crashing");
            clipboardBusy = false;
            clipboardData = new DataObject(DataFormats.UnicodeText, "text");
            check(ApplicationCommands.Paste.CanExecute(null, editor.TextArea), "Text Paste recovers after clipboard contention");
            var spreadsheetData = new DataObject();
            spreadsheetData.SetData(DataFormats.Bitmap, image);
            spreadsheetData.SetData(DataFormats.UnicodeText, "A\tB\n1\t2");
            clipboardData = spreadsheetData;
            editor.Text = "Before OLD After"; editor.Select(7, 3);
            ApplicationCommands.Paste.Execute(null, editor.TextArea);
            check(editor.Text == "Before A\tB\n1\t2 After", "Spreadsheet text takes priority over its image preview");
            editor.Undo();
            check(editor.Text == "Before OLD After", "Mixed-format text paste is undoable");
            clipboardData = new DataObject();
            clipboardData.SetData(DataFormats.UnicodeText, "pigdog");
            clipboardData.SetData(DataFormats.Html, "<p>dog</p>");
            clipboardData.SetData(DataFormats.Rtf, "{\\rtf1 dog}");
            editor.Text = "Before OLD After"; editor.Select(7, 3);
            ((MenuItem)editor.ContextMenu.Items[1]).RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
            check(editor.Text == "Before ~~pig~~dog After", "Markdown context menu replaces selection and recovers deletion");
            PumpUntil(() => ((TextBlock)window.FindName("Status")).Text != "Pasted as Markdown");
            check(((TextBlock)window.FindName("Status")).Text == "", "Markdown success message clears automatically");
            editor.Undo(); check(editor.Text == "Before OLD After", "Markdown paste is one undoable edit");
            editor.Text = "Value 1234567"; editor.Select(6, 7);
            ((MenuItem)editor.ContextMenu.Items[3]).RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
            check(editor.Text == "Value 1,234,567", "Numeric context menu adds thousand separators");
            editor.Undo(); check(editor.Text == "Value 1234567", "Numeric formatting is one undoable edit");
            editor.Select(6, 7);
            ((MenuItem)editor.ContextMenu.Items[4]).RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
            check(editor.Text == "Value one million two hundred thirty-four thousand five hundred sixty-seven", "Numeric context menu converts selection to words");
            clipboardData = new DataObject(DataFormats.Bitmap, image);
            var status = (TextBlock)window.FindName("Status");
            editor.Text = "Before OLD After"; editor.Select(7, 3);
            var data = new DataObject(DataFormats.Bitmap, image);
            var paste = new DataObjectPastingEventArgs(data, false, DataFormats.Bitmap);
            editor.RaiseEvent(paste);
            PumpUntil(() => status.Text != "Reading screenshot…");
            check(paste.CommandCancelled && editor.Text.StartsWith("Before Hello") && editor.Text.EndsWith(" After"), "Image paste replaces the selection through the WPF paste handler");
            editor.Undo(); check(editor.Text == "Before OLD After", "OCR insertion is undoable");
            editor.RaiseEvent(new DataObjectPastingEventArgs(data, false, DataFormats.Bitmap));
            editor.Text = "A newer draft";
            PumpUntil(() => status.Text != "Reading screenshot…");
            check(editor.Text == "A newer draft" && store.State.History.Any(h => h.Text.Contains("Hello")), "Typing during OCR preserves the new draft and archives the OCR result");
            editor.Text = "Before OLD After"; editor.Select(7, 3);
            check(ApplicationCommands.Paste.CanExecute(null, editor.TextArea), "Actual Paste command is enabled for an image-only clipboard");
            ApplicationCommands.Paste.Execute(null, editor.TextArea);
            PumpUntil(() => status.Text != "Reading screenshot…");
            check(editor.Text.StartsWith("Before Hello") && editor.Text.EndsWith(" After"), "Actual routed Paste command performs OCR and replaces selection");
            editor.IsReadOnly = true;
            check(!ApplicationCommands.Paste.CanExecute(null, editor.TextArea), "Image paste respects read-only editors");
            editor.IsReadOnly = false;
            using var pngBytes = new MemoryStream();
            var pngEncoder = new PngBitmapEncoder(); pngEncoder.Frames.Add(BitmapFrame.Create(image)); pngEncoder.Save(pngBytes);
            clipboardData = new DataObject("PNG", pngBytes.ToArray());
            editor.Clear();
            check(ApplicationCommands.Paste.CanExecute(null, editor.TextArea), "PNG-only clipboard enables Paste");
            ApplicationCommands.Paste.Execute(null, editor.TextArea);
            PumpUntil(() => status.Text != "Reading screenshot…");
            check(editor.Text.Contains("Hello"), "PNG-only clipboard is recognized through the Paste command");
            var blank = BitmapSource.Create(1, 1, 96, 96, PixelFormats.Bgra32, null, new byte[] {255,255,255,255}, 4);
            var blankPng = new PngBitmapEncoder(); blankPng.Frames.Add(BitmapFrame.Create(blank));
            using var blankBytes = new MemoryStream(); blankPng.Save(blankBytes);
            clipboardData = new DataObject(DataFormats.Bitmap, image);
            clipboardData.SetData("PNG", blankBytes.ToArray());
            editor.Text = "Before OLD After"; editor.Select(7, 3);
            ApplicationCommands.Paste.Execute(null, editor.TextArea);
            PumpUntil(() => status.Text != "Reading screenshot…");
            check(editor.Text.StartsWith("Before Hello") && editor.Text.EndsWith(" After"), "Empty OCR from PNG retries Bitmap and replaces the selection");
            File.WriteAllBytes(Path.Combine(store.DirectoryPath, "tessdata", "chi_sim.traineddata"), new byte[] { 1 });
            store.State.Settings.OcrLanguages = new System.Collections.Generic.List<string> { "eng", "chi_sim" };
            store.Save();
            var settings = new SettingsWindow(store, new OcrService(store.DirectoryPath), _ => true, () => { });
            settings.Measure(new Size(530, 700)); settings.Arrange(new Rect(0, 0, 530, 700));
            var picker = (ComboBox)settings.FindName("LanguagePicker"); picker.ApplyTemplate();
            var textBox = (TextBox)picker.Template.FindName("PART_EditableTextBox", picker);
            picker.SelectedItem = ((System.Collections.Generic.IEnumerable<OcrLanguage>)picker.ItemsSource).First(l => l.Code == "spa");
            textBox.Text = "Japanese";
            check(picker.SelectedItem == null && picker.Items.Cast<OcrLanguage>().All(l => l.Name.Contains("Japanese")) && picker.Items.Count > 0, "Editing a selected OCR language refreshes the search and clears stale selection");
            var priorityRows = (StackPanel)settings.FindName("InstalledLanguages");
            var secondRow = (DockPanel)priorityRows.Children[1];
            var moveUp = secondRow.Children.OfType<Button>().First(b => Equals(b.ToolTip, "Higher OCR priority"));
            moveUp.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            check(store.State.Settings.OcrLanguages.SequenceEqual(new[] { "chi_sim", "eng" }) &&
                new StateStore(store.DirectoryPath).State.Settings.OcrLanguages.SequenceEqual(new[] { "chi_sim", "eng" }),
                "OCR priority controls reorder and persist selected languages");
            settings.Close();
            var fontSettings = new SettingsWindow(store, new OcrService(store.DirectoryPath), _ => true, () => { });
            var pageRows = (StackPanel)fontSettings.FindName("SettingsRows");
            check(store.State.Settings.ScratchAlignment == "Justify", "Scratch alignment defaults to Justify");
            ((ComboBox)fontSettings.FindName("AlignmentBox")).SelectedItem = "Center";
            check(new StateStore(store.DirectoryPath).State.Settings.ScratchAlignment == "Center", "Alignment selection saves immediately");
            ((ComboBox)fontSettings.FindName("AlignmentBox")).SelectedItem = "Justify";
            ((ComboBox)fontSettings.FindName("PaperThemeBox")).SelectedItem = "Sage notebook";
            ((Slider)fontSettings.FindName("TextureSlider")).Value = 45;
            check(new StateStore(store.DirectoryPath).State.Settings.PaperTheme == "Sage notebook" && new StateStore(store.DirectoryPath).State.Settings.TextureIntensity == 45, "Theme and texture intensity save immediately");
            ((ComboBox)fontSettings.FindName("PaperThemeBox")).SelectedItem = "Porcelain";
            check(pageRows.Children.Count == 5 && ((TextBlock)fontSettings.FindName("PageTitle")).Text == "Quick settings", "Quick settings contains five controls");
            var reopenSwitch = (CheckBox)fontSettings.FindName("ReopenLastScratchBox");
            reopenSwitch.IsChecked = true; reopenSwitch.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            check(new StateStore(store.DirectoryPath).State.Settings.ReopenLastScratch, "Quick switch persists immediately without closing settings");
            reopenSwitch.IsChecked = false; reopenSwitch.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            bool fullSettingsPage = false;
            fontSettings.FullPageChanged += full => fullSettingsPage = full;
            fontSettings.ShowPage("all");
            check(fullSettingsPage && pageRows.Children.OfType<TextBlock>().Any(t => t.Text == "OCR & pasting"), "All settings opens full-width with grouped controls");
            fontSettings.ShowPage("help");
            check(((TextBlock)fontSettings.FindName("PageTitle")).Text == "Help & shortcuts" && pageRows.Children.OfType<Grid>().SelectMany(g => g.Children.OfType<TextBlock>()).Any(t => t.Text == store.State.Settings.Shortcut.Replace("Control", "Ctrl")), "Help displays the configured toggle shortcut");
            ((Button)fontSettings.FindName("PageBackButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            check(((TextBlock)fontSettings.FindName("PageTitle")).Text == "All settings", "Help returns to the settings page that opened it");
            var settingsPreview = Environment.GetEnvironmentVariable("ESCRATCH_SETTINGS_PREVIEW");
            if (!string.IsNullOrEmpty(settingsPreview))
            {
                foreach (var page in new[] { "quick", "all", "help" })
                {
                    fontSettings.ShowPage(page);
                    var width = page == "quick" ? 320 : 700;
                    fontSettings.Measure(new Size(width, 430)); fontSettings.Arrange(new Rect(0, 0, width, 430)); fontSettings.UpdateLayout();
                    var preview = new RenderTargetBitmap(width, 430, 96, 96, PixelFormats.Pbgra32); preview.Render(fontSettings);
                    var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(preview));
                    using var stream = File.Create(settingsPreview + "-" + page + ".png"); encoder.Save(stream);
                }
                fontSettings.ShowPage("all");
            }
            var fontPicker = (ComboBox)fontSettings.FindName("FontPicker");
            PumpUntil(() => fontPicker.IsEnabled);
            check(fontPicker.Items.Cast<ScratchFont>().Any(f => f.Source == "Segoe UI") &&
                !fontPicker.Items.Cast<ScratchFont>().Any(f => f.Source == "Webdings"), "Background font scan populates the filtered picker");
            var previousFamily = store.State.Settings.ScratchFontFamily;
            fontPicker.Text = "Segoe UI";
            check(store.State.Settings.ScratchFontFamily == previousFamily, "Typing a font name leaves the saved scratch font unchanged");
            var boxTypeface = fontPicker.FontFamily.Source;
            check(boxTypeface != "Segoe UI", "Typing a name does not change the selected-box typeface");
            typeof(SettingsWindow).GetMethod("CommitFontPreview", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(fontSettings, null);
            check(fontPicker.FontFamily.Source == "Segoe UI", "Committed font is previewed in the selection box");
            var fontHost = new ContentPresenter { Content = new ScratchFont(new FontFamily("Arial")), ContentTemplate = fontPicker.ItemTemplate };
            fontHost.Measure(new Size(280, 40)); fontHost.Arrange(new Rect(0, 0, 280, 40)); fontHost.UpdateLayout();
            var fontRows = (StackPanel)VisualTreeHelper.GetChild(fontHost, 0);
            var fontRow = (TextBlock)fontRows.Children[0];
            check(fontRow.Text == "Arial" && fontRow.FontFamily.Source == "Arial", "Dropdown font names preview their own typeface");
            check(new ScratchFont(new FontFamily("Arial")).ChineseSample == "", "Latin font preview does not use fallback Chinese glyphs");
            var chineseFont = new ScratchFont(new FontFamily("Microsoft YaHei"));
            check(!ScratchFont.IsTextFont(new FontFamily("Webdings")) && !ScratchFont.IsTextFont(new FontFamily("Wingdings")), "Symbol fonts are excluded from scratch font choices");
            check(ScratchFont.IsTextFont(new FontFamily("Arial")) && ScratchFont.IsTextFont(new FontFamily("Microsoft YaHei")), "Latin and Chinese text fonts remain available");
            check(chineseFont.Name.Any(c => c >= '\u3400' && c <= '\u9fff') && chineseFont.ChineseSample == "", "Chinese font name serves as the preview without a redundant sample");
            check(fontPicker.ItemsPanel.LoadContent() is VirtualizingStackPanel && VirtualizingPanel.GetIsVirtualizing(fontPicker), "Font dropdown virtualizes its preview rows");
            ((TextBox)fontSettings.FindName("FontSizeBox")).Text = "18";
            typeof(SettingsWindow).GetMethod("SaveClick", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(fontSettings, new object[] { fontSettings, new RoutedEventArgs() });
            var savedFont = new StateStore(store.DirectoryPath).State.Settings;
            check(savedFont.ScratchFontFamily == "Segoe UI" && savedFont.ScratchFontSize == 18, "Scratch font and size persist through Settings");
            typeof(MainWindow).GetMethod("ApplySettings", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(window, null);
            setZoom.Invoke(window, new object[] { 150 });
            check(editor.FontFamily.Source == "Segoe UI" && editor.FontSize == 27, "Zoom uses the selected base font size");
            setZoom.Invoke(window, new object[] { 100 });
            store.State.History.Clear();
            var paperPreview = Environment.GetEnvironmentVariable("ESCRATCH_PAPER_PREVIEW");
            if (!string.IsNullOrEmpty(paperPreview))
            {
                editor.Text = "A little room to think.\n留一点空间，记下灵感。\n\n1.5M * 60%\n23 + 56\nA longer line that wraps naturally while keeping the notebook rules under each rendered row of text.";
                var paperHost = (FrameworkElement)window.Content;
                foreach (var theme in PaperThemes.Names)
                foreach (var light in new[] { true, false })
                {
                    store.State.Settings.PaperTheme = theme; store.State.Settings.LightTheme = light;
                    typeof(MainWindow).GetMethod("ApplySettings", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(window, null);
                    paperHost.Measure(new Size(700, 500)); paperHost.Arrange(new Rect(0, 0, 700, 500)); paperHost.UpdateLayout();
                    ((PaperSurface)window.FindName("PaperBackground")).InvalidateVisual(); paperHost.UpdateLayout();
                    var bitmap = new RenderTargetBitmap(700, 500, 96, 96, PixelFormats.Pbgra32); bitmap.Render(paperHost);
                    var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
                    using var stream = File.Create(paperPreview + "-" + theme.Replace(' ', '-') + (light ? "-light" : "-dark") + ".png"); encoder.Save(stream);
                }
                store.State.Settings.PaperTheme = "Porcelain"; store.State.Settings.LightTheme = true;
                typeof(MainWindow).GetMethod("ApplySettings", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(window, null);
            }
            var liveEditor = new ICSharpCode.AvalonEdit.TextEditor { Text = "23+56\n10/3\nPlain note", Padding = new Thickness(20), WordWrap = true };
            var liveCanvas = new Canvas();
            var liveHost = new Grid(); liveHost.Children.Add(liveEditor); liveHost.Children.Add(liveCanvas);
            string copiedLiveResult = "";
            var liveController = new LiveCalculations(liveEditor, liveCanvas, new MarkdownEditor(liveEditor), result => copiedLiveResult = result);
            liveHost.Measure(new Size(700, 300)); liveHost.Arrange(new Rect(0, 0, 700, 300)); liveHost.UpdateLayout();
            typeof(LiveCalculations).GetMethod("Refresh", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(liveController, null);
            check(liveCanvas.Children.Count == 2, "Live overlay displays results only on expression lines");
            liveEditor.SelectAll();
            check(liveEditor.SelectedText == "23+56\n10/3\nPlain note", "Selecting all excludes automatic calculation results");
            ((Button)liveCanvas.Children[0]).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            check(copiedLiveResult == "79" && liveEditor.Text == "23+56\n10/3\nPlain note", "Clicking result copies value without changing note");
            check(Canvas.GetTop(liveCanvas.Children[1]) > Canvas.GetTop(liveCanvas.Children[0]), "Live results follow expression line positions");
            check(editor.Padding.Right == 20, "Main editor keeps normal right margin for prose");
            liveEditor.Text = "123456789012345+987654321098765";
            liveHost.Measure(new Size(230, 180)); liveHost.Arrange(new Rect(0, 0, 230, 180)); liveHost.UpdateLayout();
            typeof(LiveCalculations).GetMethod("Refresh", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(liveController, null);
            var expressionLine = liveEditor.TextArea.TextView.VisualLines[0];
            check(expressionLine.Elements.Any(el => el.DocumentLength == 0) && expressionLine.TextLines.Count > 1 && liveCanvas.Children.Count == 1, "Long expressions reserve a wrapped result slot outside document text");
            liveEditor.Text = "Ordinary prose uses the available width without a calculation gutter.";
            liveHost.UpdateLayout();
            check(!liveEditor.TextArea.TextView.VisualLines[0].Elements.Any(el => el.DocumentLength == 0), "Prose does not reserve calculation space");
            var alignedView = liveEditor.TextArea.TextView;
            alignedView.SetValue(TextBlock.TextAlignmentProperty, TextAlignment.Center);
            liveEditor.Text = "Centered text"; alignedView.Redraw(); liveHost.UpdateLayout();
            check(alignedView.VisualLines[0].TextLines[0].Start > 10, "Center alignment moves rendered text inward");
            var centerLine = alignedView.VisualLines[0];
            var centerPoint = centerLine.GetVisualPosition(4, ICSharpCode.AvalonEdit.Rendering.VisualYPosition.LineMiddle);
            check(centerLine.GetVisualColumn(centerPoint, false) == 4, "Centered text hit testing maps back to the correct character");
            alignedView.SetValue(TextBlock.TextAlignmentProperty, TextAlignment.Justify);
            liveEditor.Text = "A paragraph with enough words to wrap across several rows and end with a short last row.";
            alignedView.Redraw(); liveHost.UpdateLayout();
            var justified = alignedView.VisualLines[0].TextLines;
            check(justified.Count > 1 && Math.Abs(justified[0].Width - alignedView.ActualWidth) < 2, "Justify fills the width of wrapped rows");
            check(justified[justified.Count - 1].Start == 0 && justified[justified.Count - 1].Width < alignedView.ActualWidth, "Final justified row remains naturally left aligned");
            alignedView.SetValue(TextBlock.TextAlignmentProperty, TextAlignment.Left); alignedView.Redraw(); liveHost.UpdateLayout();
            check(alignedView.VisualLines[0].TextLines[0].Start == 0, "Left alignment returns text to the normal margin");
            store.State.History.Add(new HistoryEntry("board-1", "Weekend ideas\n\nA walk by the sea.\nA book and a quiet coffee.", DateTimeOffset.Now) { Pinned = true });
            store.State.History.Add(new HistoryEntry("board-2", "Quick calculation\n\n1.5M * 60% = 0.9M", DateTimeOffset.Now.AddMinutes(-15)));
            store.State.History.Add(new HistoryEntry("board-3", "下次会议\n\n讨论新的设计。\nKeep it simple.", DateTimeOffset.Now.AddHours(-1)));
            store.State.History.Add(new HistoryEntry("board-4", "A thought to keep\n\nMake room for small ideas before they disappear.", DateTimeOffset.Now.AddDays(-1)));
            ((Button)window.FindName("HistoryButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            var search = (TextBox)window.FindName("HistorySearch");
            var board = (ListBox)window.FindName("HistoryList");
            search.Text = "COFFEE";
            check(board.Items.Count == 1 && ((HistoryEntry)board.Items[0]).Id == "board-1", "History search matches full note text case-insensitively");
            search.Text = "会议";
            check(board.Items.Count == 1 && ((HistoryEntry)board.Items[0]).Id == "board-3", "History search supports Chinese text");
            search.Text = "no such note";
            check(board.Items.Count == 0 && ((TextBlock)window.FindName("EmptyHistory")).Text.StartsWith("No matching"), "History search explains empty results");
            search.Clear();
            check(board.Items.Count == 4 && store.State.History.Count == 4, "Clearing search restores the board without changing saved notes");
            var content = (FrameworkElement)window.Content;
            content.Measure(new Size(700, 560)); content.Arrange(new Rect(0, 0, 700, 560)); content.UpdateLayout();
            var caretX = search.GetRectFromCharacterIndex(0).X;
            var hintX = ((TextBlock)window.FindName("HistorySearchHint")).TranslatePoint(new Point(), search).X;
            check(!double.IsNaN(caretX) && hintX >= caretX && hintX - caretX <= 3, "Empty history search hint begins just after the caret");
            check(((Border)window.FindName("HistoryPanel")).ActualWidth > 650 && board.ItemContainerGenerator.ContainerFromIndex(0) != null, "History board fills the window and lays out note cards");
            var previewPath = Environment.GetEnvironmentVariable("ESCRATCH_BOARD_PREVIEW");
            if (!string.IsNullOrEmpty(previewPath))
            {
                var bitmap = new RenderTargetBitmap(700, 560, 96, 96, PixelFormats.Pbgra32);
                bitmap.Render(content);
                var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
                using var output = File.Create(previewPath); encoder.Save(output);
            }
            content.Measure(new Size(520, 360)); content.Arrange(new Rect(0, 0, 520, 360)); content.UpdateLayout();
            var firstCard = (ListBoxItem)board.ItemContainerGenerator.ContainerFromIndex(0);
            var secondCard = (ListBoxItem)board.ItemContainerGenerator.ContainerFromIndex(1);
            check(firstCard.ActualWidth >= 200 && Math.Abs(firstCard.TranslatePoint(new Point(), board).Y - secondCard.TranslatePoint(new Point(), board).Y) < 1,
                "Minimum window width still fits two readable note columns");
            ((Button)window.FindName("ThemeButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            content.UpdateLayout();
            check(((SolidColorBrush)Application.Current.Resources["Note"]).Color.R == 0x38, "History note palette switches to dark mode");
            var draftBeforeClear = editor.Text;
            ((Button)window.FindName("ClearHistoryButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            ((Button)window.FindName("ConfirmActionButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            check(store.State.History.Count == 1 && store.State.History[0].Id == "board-1" && store.State.History[0].Pinned &&
                new StateStore(store.DirectoryPath).State.History.Count == 1 && editor.Text == draftBeforeClear,
                "Clearing history preserves pinned notes and the current draft");
            check(!((Button)window.FindName("ClearHistoryButton")).IsEnabled, "Clear history is disabled when only pinned notes remain");
            if (!string.IsNullOrEmpty(previewPath))
            {
                var bitmap = new RenderTargetBitmap(520, 360, 96, 96, PixelFormats.Pbgra32);
                bitmap.Render(content);
                var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
                using var output = File.Create(Path.ChangeExtension(previewPath, "dark.png")); encoder.Save(output);
            }
        }
        finally
        {
            // Release the native tray handle without ever opening a desktop window.
            typeof(MainWindow).GetMethod("Quit", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(window, null);
        }
    }
    private static void PumpUntil(Func<bool> condition)
    {
        var clock = Stopwatch.StartNew();
        while (!condition())
        {
            if (clock.Elapsed > TimeSpan.FromSeconds(30)) throw new TimeoutException("WPF OCR completion timed out.");
            var frame = new DispatcherFrame();
            var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(10) };
            timer.Tick += (_, _) => { timer.Stop(); frame.Continue = false; };
            timer.Start(); Dispatcher.PushFrame(frame);
        }
    }
}
