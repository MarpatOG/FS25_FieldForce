using System.Collections.ObjectModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace FieldForce.App.Services;

public sealed class AppLogService : ILogEventSink, IDisposable
{
    private const int MaxEntries = 300;
    private readonly Logger _logger;
    private readonly Dictionary<string, AppLogEntry> _eventIndex = new(StringComparer.Ordinal);
    private bool _disposed;

    public AppLogService()
    {
        LogPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "FieldForce",
            "logs",
            "fieldforce-.log");

        Directory.CreateDirectory(Path.GetDirectoryName(LogPath)!);

        _logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.Sink(this)
            .WriteTo.File(LogPath, rollingInterval: RollingInterval.Day, retainedFileCountLimit: 14)
            .CreateLogger();
    }

    public ObservableCollection<string> Entries { get; } = [];
    public ObservableCollection<AppLogEntry> EventEntries { get; } = [];
    public string LogPath { get; }

    public void Emit(LogEvent logEvent)
    {
        var time = logEvent.Timestamp.ToString("HH:mm:ss");
        var level = logEvent.Level.ToString();
        var summary = logEvent.RenderMessage();
        var details = logEvent.Exception?.ToString() ?? "";
        var line = $"{time} [{level}] {summary}";
        var eventKey = $"{level}|{summary}|{details}";
        Dispatcher.UIThread.Post(() =>
        {
            Entries.Insert(0, line);
            while (Entries.Count > MaxEntries)
            {
                Entries.RemoveAt(Entries.Count - 1);
            }

            if (_eventIndex.TryGetValue(eventKey, out var existing))
            {
                existing.Touch(time);
                EventEntries.Move(EventEntries.IndexOf(existing), 0);
            }
            else
            {
                var entry = new AppLogEntry(eventKey, time, level, summary, string.IsNullOrWhiteSpace(details) ? line : details);
                _eventIndex[eventKey] = entry;
                EventEntries.Insert(0, entry);
            }

            while (EventEntries.Count > MaxEntries)
            {
                var removed = EventEntries[^1];
                EventEntries.RemoveAt(EventEntries.Count - 1);
                _eventIndex.Remove(removed.Key);
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

public sealed partial class AppLogEntry : ObservableObject
{
    public AppLogEntry(string key, string time, string level, string summary, string details)
    {
        Key = key;
        Time = time;
        Level = level;
        Summary = summary;
        Details = details;
    }

    [ObservableProperty]
    private string _time;

    [ObservableProperty]
    private int _count = 1;

    public string Key { get; }
    public string Level { get; }
    public string Summary { get; }
    public string Details { get; }
    public string CountText => Count > 1 ? $"x{Count}" : "";

    public void Touch(string time)
    {
        Time = time;
        Count++;
        OnPropertyChanged(nameof(CountText));
    }
}
