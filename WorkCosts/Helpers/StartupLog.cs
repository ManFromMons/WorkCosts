using System.Diagnostics;
using System.Text;

namespace WorkCosts.Helpers;

/// <summary>
/// Writes startup diagnostics to a file so a failed launch can be diagnosed without a debugger.
/// </summary>
internal static class StartupLog
{
    private static readonly object Gate = new();
    private static readonly string LogPath = ResolveLogPath();

    public static string Path => LogPath;

    public static void Write(string message, Exception? exception = null)
    {
        var builder = new StringBuilder()
            .Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"))
            .Append("  ")
            .Append(message);

        if (exception is not null)
        {
            builder.AppendLine().Append(Format(exception));
        }

        var line = builder.ToString();
        Console.WriteLine(line);
        Debug.WriteLine(line);

        try
        {
            lock (Gate)
            {
                var directory = System.IO.Path.GetDirectoryName(LogPath);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                File.AppendAllText(LogPath, line + Environment.NewLine + Environment.NewLine);
            }
        }
        catch (Exception writeEx)
        {
            Console.WriteLine($"StartupLog write failed: {writeEx}");
            Debug.WriteLine($"StartupLog write failed: {writeEx}");
        }
    }

    public static string Format(Exception exception)
    {
        var parts = new List<string> { $"{exception.GetType().FullName}: {exception.Message}" };
        for (var inner = exception.InnerException; inner is not null; inner = inner.InnerException)
        {
            parts.Add($"Inner {inner.GetType().FullName}: {inner.Message}");
        }

        if (!string.IsNullOrWhiteSpace(exception.StackTrace))
        {
            parts.Add(exception.StackTrace);
        }

        return string.Join(Environment.NewLine + Environment.NewLine, parts);
    }

    private static string ResolveLogPath()
    {
        try
        {
            var folder = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Will I DIY");
            return System.IO.Path.Combine(folder, "startup.log");
        }
        catch
        {
            return System.IO.Path.Combine(AppContext.BaseDirectory, "startup.log");
        }
    }
}
