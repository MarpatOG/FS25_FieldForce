using System.Collections.ObjectModel;
using Avalonia.Threading;
using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace FS25FfbBridge.App.Services;

public sealed class AppLogService : ILogEventSink, IDisposable
{
    private const int MaxEntries = 300;
    private readonly Logger _logger;
    private bool _disposed;

    public AppLogService()
    {
        LogPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "FS25FFBBridge",
            "logs",
            "bridge-.log");

        Directory.CreateDirectory(Path.GetDirectoryName(LogPath)!);

        _logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.Sink(this)
            .WriteTo.File(LogPath, rollingInterval: RollingInterval.Day, retainedFileCountLimit: 14)
            .CreateLogger();
    }

    public ObservableCollection<string> Entries { get; } = [];
    public string LogPath { get; }

    public void Emit(LogEvent logEvent)
    {
        var line = $"{logEvent.Timestamp:HH:mm:ss} [{logEvent.Level}] {logEvent.RenderMessage()}";
        Dispatcher.UIThread.Post(() =>
        {
            Entries.Insert(0, line);
            while (Entries.Count > MaxEntries)
            {
                Entries.RemoveAt(Entries.Count - 1);
            }
        });
    }

    public void Information(string messageTemplate, params object?[] propertyValues) => _logger.Information(messageTemplate, propertyValues);
    public void Warning(string messageTemplate, params object?[] propertyValues) => _logger.Warning(messageTemplate, propertyValues);
    public void Error(Exception exception, string messageTemplate, params object?[] propertyValues) => _logger.Error(exception, messageTemplate, propertyValues);

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _logger.Dispose();
    }
}
