namespace SMEH.Helpers;

/// <summary>Renders a simple progress bar to the console for download progress. Uses System.Console for \r overwrite so it works alongside Spectre.Console.</summary>
public static class ConsoleProgressBar
{
    private const int BarWidth = 30;
    private static readonly object Lock = new();

    /// <summary>
    /// Creates a progress reporter that draws on the reporting thread. Unlike <see cref="Progress{T}"/>, which queues
    /// callbacks to the thread pool in a console app, no updates can land after <see cref="Clear"/>.
    /// </summary>
    /// <param name="label">Optional prefix.</param>
    public static IProgress<DownloadProgress> Create(string? label = null) => new SynchronousProgress(label);

    /// <summary>Draws one line: a progress bar and percentage/size.</summary>
    /// <param name="p">Current download progress.</param>
    /// <param name="label">Optional prefix.</param>
    public static void Report(DownloadProgress p, string? label = null)
    {
        lock (Lock)
        {
            var prefix = string.IsNullOrEmpty(label) ? "  " : $"  {label} ";
            if (p.TotalBytes.HasValue && p.TotalBytes.Value > 0)
            {
                var pct = (int)Math.Min(100, (100 * p.BytesRead) / p.TotalBytes.Value);
                var filled = (int)Math.Round((BarWidth * p.BytesRead) / (double)p.TotalBytes.Value);
                filled = Math.Clamp(filled, 0, BarWidth);
                var bar = new string('=', filled) + new string(' ', BarWidth - filled);
                var readMb = p.BytesRead / 1_000_000.0;
                var totalMb = p.TotalBytes.Value / 1_000_000.0;
                Console.Write($"\r{prefix}[{bar}] {pct,3}% ({readMb:F1} MB / {totalMb:F1} MB)   ");
            }
            else
            {
                var readMb = p.BytesRead / 1_000_000.0;
                Console.Write($"\r{prefix}Downloaded: {readMb:F1} MB   ");
            }
        }
    }

    /// <summary>Clears the current progress line so the next output starts clean.</summary>
    public static void Clear()
    {
        lock (Lock)
        {
            try
            {
                var w = Math.Max(1, Console.WindowWidth);
                Console.Write("\r" + new string(' ', w - 1) + "\r");
            }
            catch
            {
                Console.WriteLine();
            }
        }
    }

    private sealed class SynchronousProgress : IProgress<DownloadProgress>
    {
        // Reports arrive on every read; redrawing each time would slow the download itself
        private static readonly TimeSpan RedrawInterval = TimeSpan.FromMilliseconds(100);

        private readonly string? _label;
        private readonly System.Diagnostics.Stopwatch _sinceLastDraw = new();

        public SynchronousProgress(string? label)
        {
            _label = label;
        }

        public void Report(DownloadProgress value)
        {
            var isComplete = value.TotalBytes.HasValue && value.BytesRead >= value.TotalBytes.Value;
            if (_sinceLastDraw.IsRunning && _sinceLastDraw.Elapsed < RedrawInterval && !isComplete)
            {
                return;
            }

            ConsoleProgressBar.Report(value, _label);
            _sinceLastDraw.Restart();
        }
    }
}
