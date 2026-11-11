using Microsoft.Extensions.Logging;
using System.IO;
using System.Text;

namespace GreenLumaPresets.Logging;

/// <summary>
/// Minimal logger provider writing one file per day, keeping the most recent files only.
/// </summary>
public sealed class FileLoggerProvider : ILoggerProvider
{
    private const int RetainedFileCount = 7;

    private readonly string directory;
    private readonly object writeLock = new();

    public FileLoggerProvider(string directory)
    {
        this.directory = directory;
        try
        {
            Directory.CreateDirectory(directory);
            DeleteOldFiles();
        }
        catch
        {
            // Logging must never prevent the application from starting.
        }
    }

    public static string DefaultDirectory => AppPaths.LogsDirectory;

    public ILogger CreateLogger(string categoryName) => new FileLogger(this, categoryName);

    public void Dispose() { }

    private void Write(string line)
    {
        var path = Path.Combine(directory, $"app-{DateTime.Now:yyyyMMdd}.log");
        lock (writeLock)
        {
            try
            {
                File.AppendAllText(path, line, Encoding.UTF8);
            }
            catch
            {
                // Ignore I/O failures (disk full, file locked, ...).
            }
        }
    }

    private void DeleteOldFiles()
    {
        var oldFiles = new DirectoryInfo(directory)
            .GetFiles("app-*.log")
            .OrderByDescending(f => f.Name)
            .Skip(RetainedFileCount);

        foreach (var file in oldFiles)
        {
            file.Delete();
        }
    }

    private sealed class FileLogger(FileLoggerProvider provider, string categoryName) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel)) return;

            var builder = new StringBuilder()
                .Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"))
                .Append(" [").Append(logLevel).Append("] ")
                .Append(categoryName).Append(": ")
                .AppendLine(formatter(state, exception));

            if (exception != null)
            {
                builder.AppendLine(exception.ToString());
            }

            provider.Write(builder.ToString());
        }
    }
}
