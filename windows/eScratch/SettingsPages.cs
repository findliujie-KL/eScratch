using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace eScratch;

public partial class SettingsWindow
{
    public event Action<bool>? FullPageChanged;
    private readonly Dictionary<FrameworkElement, FrameworkElement> settingRows = new();
    private string currentPage = "quick";
    private string helpReturnPage = "quick";
    private bool pagesReady;
    private void InitializePages()
    {
        foreach (var control in new FrameworkElement[] { AlignmentBox, PaperThemeBox, TextureSlider, FontPicker, LoginBox, OpenNearPointerBox, ReopenLastScratchBox, CopyOnHideBox,
            WordCountBox, TopmostBox, LightBox, WhitespaceBox, HistoryLimitBox, IndentBox, LanguagePicker, JoinOcrLinesBox,
            ToggleBox, NewBox, MarkdownBox, RestoreDefaultsButton, VersionText })
        {
            var row = control;
            while (row.Parent is FrameworkElement parent && parent != SettingsRows) row = parent;
            settingRows[control] = row;
        }
        foreach (var box in new[] { LoginBox, OpenNearPointerBox, ReopenLastScratchBox, CopyOnHideBox, WordCountBox, TopmostBox, LightBox, WhitespaceBox, JoinOcrLinesBox })
            box.Click += SaveClick;
        foreach (var box in new[] { FontSizeBox, HistoryLimitBox })
        {
            box.LostKeyboardFocus += SaveClick;
            box.PreviewKeyDown += (_, e) => { if (e.Key == Key.Enter) { SaveClick(box, e); e.Handled = true; } };
        }
        IndentBox.SelectionChanged += SaveClick; IndentSizeBox.SelectionChanged += SaveClick;
        PaperThemeBox.SelectionChanged += SaveClick;
        AlignmentBox.SelectionChanged += SaveClick;
        TextureSlider.ValueChanged += SaveClick;
        pagesReady = true;
        ShowPage("quick");
    }
    private void AddRow(FrameworkElement control)
    {
        var row = settingRows[control];
        if (row is Panel panel)
            foreach (var hint in panel.Children.OfType<TextBlock>())
                if (hint.Style == TryFindResource("Hint")) hint.Visibility = currentPage == "quick" ? Visibility.Collapsed : Visibility.Visible;
        row.Margin = new Thickness(0, currentPage == "quick" ? 3 : 12, 0, currentPage == "quick" ? 3 : 12);
        if (control is CheckBox checkbox) checkbox.Margin = new Thickness(0, currentPage == "quick" ? 3 : 6, 0, currentPage == "quick" ? 3 : 6);
        SettingsRows.Children.Add(row);
    }
    private void Heading(string title)
    {
        var text = new TextBlock { Text = title, FontSize = 17, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 18, 0, 6) };
        text.SetResourceReference(TextBlock.ForegroundProperty, "Ink"); SettingsRows.Children.Add(text);
    }
    private void NavigateButton(string title, string page)
    {
        var button = new Button { Content = title, HorizontalAlignment = HorizontalAlignment.Stretch, Margin = new Thickness(0, 6, 0, 0) };
        button.Click += (_, _) => ShowPage(page); SettingsRows.Children.Add(button);
    }
    public void ShowPage(string page)
    {
        if (page == "help" && currentPage != "help") helpReturnPage = currentPage;
        currentPage = page;
        SettingsRows.Children.Clear();
        SettingsScroll.ScrollToTop();
        PageTitle.Text = page == "quick" ? "Quick settings" : page == "all" ? "All settings" : "Help & shortcuts";
        PageBackButton.Content = page == "quick" ? "Close" : "Back";
        QuickNavigation.Visibility = page == "quick" ? Visibility.Visible : Visibility.Collapsed;
        AutoSaveHint.Visibility = page == "help" ? Visibility.Collapsed : Visibility.Visible;
        FullPageChanged?.Invoke(page != "quick");
        if (page == "quick")
        {
            foreach (var control in new FrameworkElement[] { ToggleBox, ReopenLastScratchBox, OpenNearPointerBox, WordCountBox, CopyOnHideBox }) AddRow(control);
        }
        else if (page == "all")
        {
            void Section(string title, params FrameworkElement[] controls) { Heading(title); foreach (var control in controls) AddRow(control); }
            Section("Appearance", PaperThemeBox, TextureSlider, FontPicker, AlignmentBox, LightBox, WordCountBox, WhitespaceBox);
            Section("Window & startup", LoginBox, ReopenLastScratchBox, OpenNearPointerBox, TopmostBox, CopyOnHideBox);
            Section("Keyboard shortcuts", ToggleBox, NewBox, MarkdownBox);
            Section("OCR & pasting", LanguagePicker, JoinOcrLinesBox);
            Section("History & editing", HistoryLimitBox, IndentBox);
            Section("About", VersionText);
            HelpRow("License", "MIT · Includes third-party components; see THIRD-PARTY-NOTICES.txt in the application folder.");
            AddRow(RestoreDefaultsButton); NavigateButton("Help & shortcuts", "help");
        }
        else
        {
            var s = store.State.Settings;
            string Keys(string text) => text.Replace("Control", "Ctrl");
            Heading("Open & navigate");
            HelpRow("Show / hide window (system-wide)", Keys(s.Shortcut));
            HelpRow("New scratch", Keys(s.NewShortcut)); HelpRow("History", "Ctrl+H"); HelpRow("Help & shortcuts", "F1");
            HelpRow("Save copy as pinned note", "F12 · Keeps the current scratch unchanged");
            HelpRow("Close panel / hide window", "Esc");
            Heading("Paste"); HelpRow("Paste text or recognize a screenshot", "Ctrl+V"); HelpRow("Paste as Markdown", Keys(s.MarkdownShortcut));
            Heading("Format"); HelpRow("Toggle bold", "Ctrl+B"); HelpRow("Toggle italic", "Ctrl+I"); HelpRow("Toggle strikethrough", "Ctrl+Shift+X");
            HelpRow("Formatted / Markdown source view", "Use the Abc / */~ button at the top left. The saved note remains plain Markdown.");
            Heading("Calculate"); HelpRow("Automatic result", "Type an expression on a line. Its result appears on the right, outside your note.");
            HelpRow("Copy a result", "Click the result; right-click for other number formats.");
            HelpRow("Insert result / reveal more decimals", "Ctrl+Enter at the end of the expression"); HelpRow("Cycle displayed units", "Ctrl+Shift+Enter");
            Heading("View"); HelpRow("Zoom in / out", "Ctrl+scroll up / down (75–250%)"); HelpRow("Reset zoom", "Ctrl+0");
            Heading("History"); HelpRow("Find a saved note", "Search on the History board. Click a note to reopen it."); HelpRow("Keep a note", "Pin it. Clear unpinned keeps pinned notes and your current draft.");
            Heading("Shortcut scope"); HelpRow("Inside eScratch", "All shortcuts above work inside the app unless marked system-wide. Editing shortcuts require focus in the scratch.");
        }
    }
    private void HelpRow(string label, string value)
    {
        var grid = new Grid { Margin = new Thickness(0, 7, 0, 7) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(3, GridUnitType.Star) });
        var name = new TextBlock { Text = label, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 16, 0) };
        var detail = new TextBlock { Text = value, TextWrapping = TextWrapping.Wrap };
        detail.SetResourceReference(TextBlock.ForegroundProperty, "Secondary");
        Grid.SetColumn(detail, 1); grid.Children.Add(name); grid.Children.Add(detail); SettingsRows.Children.Add(grid);
    }
    private void PageBackClick(object sender, RoutedEventArgs e) { if (currentPage == "quick") Close(); else ShowPage(currentPage == "help" ? helpReturnPage : "quick"); }
    private void AllSettingsClick(object sender, RoutedEventArgs e) => ShowPage("all");
    private void HelpClick(object sender, RoutedEventArgs e) => ShowPage("help");
}
