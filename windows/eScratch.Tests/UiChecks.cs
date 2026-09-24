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
        var store = new StateStore(Path.Combine(directory, "ui"));
        IDataObject clipboardData = new DataObject(DataFormats.Bitmap, image);
        var window = new MainWindow(store, () => clipboardData);
        try
        {
            check(store.State.Settings.LightTheme && window.Background is SolidColorBrush brush && brush.Color.R == 0xef, "WPF defaults to the original light palette");
            ((Button)window.FindName("ThemeButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            check(!new StateStore(store.DirectoryPath).State.Settings.LightTheme && ((SolidColorBrush)window.Background).Color.R == 0x1e, "Toolbar theme toggle applies and persists dark mode");
            ((Button)window.FindName("ThemeButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            ((Button)window.FindName("HistoryButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            check(((Border)window.FindName("HistoryPanel")).Visibility == Visibility.Visible, "Toolbar opens history inside the editor");
            ((Button)window.FindName("SettingsButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            check(((Border)window.FindName("HistoryPanel")).Visibility == Visibility.Collapsed && ((ContentControl)window.FindName("SettingsContent")).Content is SettingsWindow, "Settings replaces history with an embedded panel");
            ((Button)window.FindName("SettingsButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            check(((Border)window.FindName("PanelBackdrop")).Visibility == Visibility.Collapsed, "Clicking Settings again dismisses the panel");
            var editor = (TextEditor)window.FindName("Editor");
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
            var settings = new SettingsWindow(store, new OcrService(store.DirectoryPath), _ => true, () => { });
            settings.Measure(new Size(530, 700)); settings.Arrange(new Rect(0, 0, 530, 700));
            var picker = (ComboBox)settings.FindName("LanguagePicker"); picker.ApplyTemplate();
            var textBox = (TextBox)picker.Template.FindName("PART_EditableTextBox", picker);
            picker.SelectedItem = ((System.Collections.Generic.IEnumerable<OcrLanguage>)picker.ItemsSource).First(l => l.Code == "spa");
            textBox.Text = "Japanese";
            check(picker.SelectedItem == null && picker.Items.Cast<OcrLanguage>().All(l => l.Name.Contains("Japanese")) && picker.Items.Count > 0, "Editing a selected OCR language refreshes the search and clears stale selection");
            settings.Close();
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
