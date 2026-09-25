using System;
using System.Windows.Controls;
using System.Windows.Threading;

namespace eScratch;

// One timer per message area; a new message cancels the previous expiry.
public sealed class TransientNotice : IDisposable
{
    private readonly TextBlock target;
    private bool disposed;
    private readonly DispatcherTimer timer = new();
    public TransientNotice(TextBlock target)
    {
        this.target = target;
        timer.Tick += (_, _) => { timer.Stop(); target.Text = ""; };
    }
    public void Show(string text, double seconds = 8, bool persistent = false)
    {
        if (disposed) return;
        timer.Stop();
        target.Text = text;
        if (text.Length == 0 || persistent) return;
        timer.Interval = TimeSpan.FromSeconds(seconds);
        timer.Start();
    }
    public void Dispose() { disposed = true; timer.Stop(); }
}
