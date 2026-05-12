using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using FS25FfbBridge.App.Models;

namespace FS25FfbBridge.App.Services;

public sealed class TelemetryReceiverService : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };
    private static readonly HashSet<string> AllowedTopLevelKeys = new(StringComparer.Ordinal)
    {
        "protocol",
        "frame",
        "game",
        "player",
        "vehicle",
        "controls",
        "motion",
        "steering",
        "engine",
        "transmission",
        "events",
        "wheels",
        "suspension",
        "surface",
        "environment",
        "attachments",
        "collisions",
        "diagnostics"
    };

    private readonly AppLogService _log;
    private readonly ConcurrentQueue<DateTimeOffset> _packetTimes = new();
    private readonly object _stateLock = new();
    private CancellationTokenSource? _cts;
    private UdpClient? _udpClient;
    private System.Threading.Timer? _statusTimer;
    private IReadOnlyList<string> _fileCandidates = [];
    private string? _activeFilePath;
    private DateTime _lastFileWriteTimeUtc;
    private TelemetryPacketV1? _lastPacket;
    private DateTimeOffset? _lastPacketAt;
    private string _lastRawPacket = "";
    private string? _lastParseError;
    private string _udpStatus = "Not started";
    private string _fileStatus = "Not started";
    private string _lastPacketSource = "none";
    private string? _lastTransportError;
    private TelemetryStatus _status = TelemetryStatus.Waiting;
    private int _lostTimeoutMs;
    private int _uiRefreshMs;
    private bool _udpBindFailed;
    private bool _loggedConnected;

    public TelemetryReceiverService(AppLogService log)
    {
        _log = log;
    }

    public event Action<TelemetryReceiverState>? StateChanged;
    public event Action<TelemetryReceiverState>? FfbStateChanged;

    public string Endpoint { get; private set; } = "127.0.0.1:34325";
    public int FfbUpdateRateHz { get; private set; } = 125;

    public void Start(
        string host,
        int port,
        int lostTimeoutMs,
        string? filePath = null,
        bool includeDefaultFilePath = true,
        int ffbUpdateRateHz = 125,
        int uiRefreshMs = 100)
    {
        Stop();

        _lostTimeoutMs = Math.Max(250, lostTimeoutMs);
        FfbUpdateRateHz = Math.Clamp(ffbUpdateRateHz, 1, 1000);
        _uiRefreshMs = Math.Clamp(uiRefreshMs, 20, 1000);
        _cts = new CancellationTokenSource();
        _packetTimes.Clear();
        _lastPacket = null;
        _lastPacketAt = null;
        _lastRawPacket = "";
        _lastParseError = null;
        _udpStatus = "Not started";
        _fileStatus = "Not started";
        _lastPacketSource = "none";
        _lastTransportError = null;
        _udpBindFailed = false;
        _loggedConnected = false;
        _status = TelemetryStatus.Waiting;

        var address = ParseAddress(host);
        var udpEndpoint = $"udp://{address}:{port}";
        _fileCandidates = GetTelemetryFileCandidates(filePath, includeDefaultFilePath);
        _activeFilePath = null;
        _lastFileWriteTimeUtc = DateTime.MinValue;
        _fileStatus = $"Waiting: {FormatFileCandidates(_fileCandidates)}";
        Endpoint = $"{udpEndpoint} | {FormatFileCandidates(_fileCandidates)}";
        _statusTimer = new System.Threading.Timer(_ => PublishState(), null, TimeSpan.Zero, TimeSpan.FromMilliseconds(_uiRefreshMs));

        try
        {
            _udpClient = new UdpClient(new IPEndPoint(address, port));
            _udpStatus = $"Listening: {udpEndpoint}";
            _ = Task.Run(() => ReceiveLoopAsync(_cts.Token));
        }
        catch (SocketException ex)
        {
            _udpStatus = $"Bind failed: {udpEndpoint}";
            _lastTransportError = $"UDP bind failed on {udpEndpoint}: {ex.SocketErrorCode}";
            _udpBindFailed = true;
            _log.Error(ex, "Telemetry UDP bind failed on {Endpoint}", udpEndpoint);
            PublishState();
        }
        catch (Exception ex)
        {
            _udpStatus = $"Bind failed: {udpEndpoint}";
            _lastTransportError = $"UDP bind failed on {udpEndpoint}: {ex.Message}";
            _udpBindFailed = true;
            _log.Error(ex, "Telemetry UDP bind failed on {Endpoint}", udpEndpoint);
            PublishState();
        }

        _ = Task.Run(() => FileLoopAsync(_cts.Token));
        _log.Information("Telemetry receiver started on {Endpoint}", Endpoint);
    }

    public void Stop()
    {
        try
        {
            _cts?.Cancel();
            _udpClient?.Close();
            _udpClient?.Dispose();
            _statusTimer?.Dispose();
        }
        catch (Exception ex)
        {
            _log.Error(ex, "Telemetry receiver stop failed");
        }
        finally
        {
            _udpClient = null;
            _cts?.Dispose();
            _cts = null;
            _statusTimer = null;
        }
    }

    public void Dispose()
    {
        Stop();
    }

    private async Task ReceiveLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                if (_udpClient is null)
                {
                    return;
                }

                var result = await _udpClient.ReceiveAsync(cancellationToken);
                var raw = Encoding.UTF8.GetString(result.Buffer).Trim();
                HandlePacket(raw, $"udp://{result.RemoteEndPoint}", isUdp: true);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (ObjectDisposedException)
            {
                return;
            }
            catch (Exception ex)
            {
                _log.Error(ex, "Telemetry receive loop error");
                await Task.Delay(250, cancellationToken);
            }
        }
    }

    private async Task FileLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var filePath = FindExistingTelemetryFile();
                if (string.IsNullOrWhiteSpace(filePath))
                {
                    _fileStatus = $"Waiting: no telemetry file found ({FormatFileCandidates(_fileCandidates)})";
                }
                else
                {
                    if (!string.Equals(_activeFilePath, filePath, StringComparison.OrdinalIgnoreCase))
                    {
                        _activeFilePath = filePath;
                        _lastFileWriteTimeUtc = DateTime.MinValue;
                    }

                    _fileStatus = $"Watching: file://{filePath}";
                    var writeTime = File.GetLastWriteTimeUtc(filePath);
                    if (writeTime > _lastFileWriteTimeUtc)
                    {
                        var raw = await ReadTelemetryFileWithRetryAsync(filePath, cancellationToken);
                        if (!string.IsNullOrWhiteSpace(raw))
                        {
                            _lastFileWriteTimeUtc = writeTime;
                            HandlePacket(raw.Trim(), $"file://{filePath}", isUdp: false);
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception ex)
            {
                _fileStatus = $"Read error: {ex.Message}";
                _lastTransportError = $"Telemetry file read failed: {ex.Message}";
                _log.Error(ex, "Telemetry file receive error");
            }

            await Task.Delay(100, cancellationToken);
        }
    }

    private static async Task<string> ReadTelemetryFileWithRetryAsync(string path, CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 3; attempt++)
        {
            try
            {
                return await File.ReadAllTextAsync(path, cancellationToken);
            }
            catch (IOException) when (attempt < 2)
            {
                await Task.Delay(20, cancellationToken);
            }
        }

        return await File.ReadAllTextAsync(path, cancellationToken);
    }

    private void HandlePacket(string raw, string source, bool isUdp)
    {
        if (!isUdp)
        {
            lock (_stateLock)
            {
                if (!CanAcceptFilePacket())
                {
                    return;
                }
            }
        }

        try
        {
            ValidateTopLevelContract(raw);
            var packet = JsonSerializer.Deserialize<TelemetryPacketV1>(raw, JsonOptions);
            if (packet is null)
            {
                throw new JsonException("Packet decoded as null.");
            }
            packet.ValidateContract();

            TelemetryReceiverState state;
            var publishUiImmediately = false;
            lock (_stateLock)
            {
                if (!isUdp && !CanAcceptFilePacket())
                {
                    return;
                }

                _lastRawPacket = raw;
                _lastPacketAt = DateTimeOffset.UtcNow;
                if (isUdp)
                {
                    _packetTimes.Enqueue(_lastPacketAt.Value);
                }

                TrimPacketTimes();
                _lastPacket = packet;
                _lastParseError = null;
                _lastPacketSource = source;
                publishUiImmediately = SetStatus(TelemetryStatus.Connected);
                state = CreateState();
            }

            if (!_loggedConnected)
            {
                _loggedConnected = true;
                _log.Information("Telemetry connected from {Source}", source);
            }

            FfbStateChanged?.Invoke(state);
            if (publishUiImmediately)
            {
                StateChanged?.Invoke(state);
            }
        }
        catch (Exception ex) when (ex is JsonException or InvalidDataException)
        {
            lock (_stateLock)
            {
                _lastRawPacket = raw;
                _lastParseError = ex.Message;
            }

            _log.Warning("Telemetry parse error: {Error}", ex.Message);
            PublishState();
        }
    }

    private static void ValidateTopLevelContract(string raw)
    {
        using var document = JsonDocument.Parse(raw);
        if (document.RootElement.ValueKind != JsonValueKind.Object)
        {
            throw new JsonException("Telemetry packet root must be a JSON object.");
        }

        foreach (var property in document.RootElement.EnumerateObject())
        {
            if (!AllowedTopLevelKeys.Contains(property.Name))
            {
                throw new InvalidDataException($"Unsupported telemetry top-level field '{property.Name}'.");
            }
        }
    }

    private void PublishState()
    {
        TelemetryReceiverState state;
        var publishFfb = false;
        lock (_stateLock)
        {
            if (_lastPacketAt is null)
            {
                SetStatus(TelemetryStatus.Waiting);
            }
            else
            {
                var age = DateTimeOffset.UtcNow - _lastPacketAt.Value;
                publishFfb = SetStatus(age.TotalMilliseconds > _lostTimeoutMs ? TelemetryStatus.Lost : TelemetryStatus.Connected) &&
                    _status == TelemetryStatus.Lost;
            }

            TrimPacketTimes();
            state = CreateState();
        }

        if (publishFfb)
        {
            FfbStateChanged?.Invoke(state);
        }

        StateChanged?.Invoke(state);
    }

    private TelemetryReceiverState CreateState()
    {
        return new TelemetryReceiverState(
            _status,
            _lastPacket,
            _lastRawPacket,
            _packetTimes.Count,
            _lastPacketAt is null ? null : DateTimeOffset.UtcNow - _lastPacketAt.Value,
            _lastParseError,
            Endpoint,
            _udpStatus,
            _fileStatus,
            _lastPacketSource,
            _lastTransportError);
    }

    private bool SetStatus(TelemetryStatus status)
    {
        if (_status == status)
        {
            return false;
        }

        _status = status;
        _log.Information("Telemetry status changed: {Status}", status);
        return true;
    }

    private void TrimPacketTimes()
    {
        var threshold = DateTimeOffset.UtcNow.AddSeconds(-1);
        while (_packetTimes.TryPeek(out var packetTime) && packetTime < threshold)
        {
            _packetTimes.TryDequeue(out _);
        }
    }

    private bool CanAcceptFilePacket()
    {
        if (_udpBindFailed)
        {
            return true;
        }

        if (_lastPacketAt is null)
        {
            return true;
        }

        if (!_lastPacketSource.StartsWith("udp://", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return DateTimeOffset.UtcNow - _lastPacketAt.Value > TimeSpan.FromMilliseconds(_lostTimeoutMs);
    }

    private static IPAddress ParseAddress(string host)
    {
        return IPAddress.TryParse(host, out var address) ? address : IPAddress.Loopback;
    }

    private string? FindExistingTelemetryFile()
    {
        foreach (var path in _fileCandidates)
        {
            if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
            {
                return path;
            }
        }

        return null;
    }

    private static IReadOnlyList<string> GetTelemetryFileCandidates(string? explicitFilePath, bool includeDefaultFilePath)
    {
        var paths = new List<string>();
        if (!string.IsNullOrWhiteSpace(explicitFilePath))
        {
            paths.Add(explicitFilePath);
        }

        if (!includeDefaultFilePath)
        {
            return paths;
        }

        var defaultPath = GetDefaultTelemetryFilePath();
        if (!paths.Any(path => string.Equals(path, defaultPath, StringComparison.OrdinalIgnoreCase)))
        {
            paths.Add(defaultPath);
        }

        return paths;
    }

    private static string FormatFileCandidates(IReadOnlyList<string> paths)
    {
        return string.Join(" | ", paths.Select(path => $"file://{path}"));
    }

    public static string GetDefaultTelemetryFilePath()
    {
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "My Games",
            "FarmingSimulator2025",
            "modSettings",
            "FS25_RealFfbTelemetry",
            "telemetry.json");
    }
}
