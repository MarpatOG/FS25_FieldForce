using System.Globalization;
using System.Text;
using System.Text.Json;
using FS25FfbBridge.App.Models;

namespace FS25FfbBridge.App.Services;

public sealed class TelemetryCaptureLogService : IDisposable
{
    private readonly AppLogService _log;
    private readonly object _lock = new();
    private StreamWriter? _ndjsonWriter;
    private StreamWriter? _csvWriter;
    private int _sampleCount;

    public TelemetryCaptureLogService(AppLogService log)
    {
        _log = log;
        CaptureDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "FS25FFBBridge",
            "captures");
    }

    public string CaptureDirectory { get; }
    public string? CurrentNdjsonPath { get; private set; }
    public string? CurrentCsvPath { get; private set; }
    public bool IsRecording { get; private set; }
    public int SampleCount => _sampleCount;

    public string Start()
    {
        lock (_lock)
        {
            if (IsRecording && CurrentNdjsonPath is not null)
            {
                return CurrentNdjsonPath;
            }

            Directory.CreateDirectory(CaptureDirectory);
            DeletePreviousCaptures();
            var stamp = DateTimeOffset.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
            CurrentNdjsonPath = Path.Combine(CaptureDirectory, $"telemetry-{stamp}.ndjson");
            CurrentCsvPath = Path.Combine(CaptureDirectory, $"telemetry-{stamp}.csv");
            _sampleCount = 0;

            _ndjsonWriter = new StreamWriter(CurrentNdjsonPath, append: false, new UTF8Encoding(false));
            _csvWriter = new StreamWriter(CurrentCsvPath, append: false, new UTF8Encoding(false));
            _csvWriter.WriteLine(string.Join(",", CsvColumns));
            IsRecording = true;
            _log.Information("Telemetry capture started: {Path}", CurrentNdjsonPath);
            return CurrentNdjsonPath;
        }
    }

    public void Stop()
    {
        lock (_lock)
        {
            if (!IsRecording)
            {
                return;
            }

            DisposeWriters();
            IsRecording = false;
            _log.Information("Telemetry capture stopped: {Samples} samples, {Path}", _sampleCount, CurrentNdjsonPath);
        }
    }

    public void Record(TelemetryReceiverState state)
    {
        if (!IsRecording || string.IsNullOrWhiteSpace(state.LastRawPacket))
        {
            return;
        }

        lock (_lock)
        {
            if (!IsRecording || _ndjsonWriter is null || _csvWriter is null)
            {
                return;
            }

            try
            {
                _sampleCount++;
                var recordedAt = DateTimeOffset.UtcNow;
                _ndjsonWriter.WriteLine(CreateNdjsonLine(recordedAt, state));
                _csvWriter.WriteLine(CreateCsvLine(recordedAt, state));

                if (_sampleCount % 32 == 0)
                {
                    _ndjsonWriter.Flush();
                    _csvWriter.Flush();
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, "Telemetry capture write failed");
                Stop();
            }
        }
    }

    public void Dispose()
    {
        Stop();
    }

    private void DisposeWriters()
    {
        _ndjsonWriter?.Flush();
        _ndjsonWriter?.Dispose();
        _csvWriter?.Flush();
        _csvWriter?.Dispose();
        _ndjsonWriter = null;
        _csvWriter = null;
    }

    private void DeletePreviousCaptures()
    {
        foreach (var path in Directory.EnumerateFiles(CaptureDirectory, "telemetry-*.*"))
        {
            var extension = Path.GetExtension(path);
            if (!string.Equals(extension, ".ndjson", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(extension, ".csv", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            try
            {
                File.Delete(path);
            }
            catch (Exception ex)
            {
                _log.Warning("Could not delete previous telemetry capture {Path}: {Error}", path, ex.Message);
            }
        }
    }

    private static string CreateNdjsonLine(DateTimeOffset recordedAt, TelemetryReceiverState state)
    {
        using var rawDocument = JsonDocument.Parse(state.LastRawPacket);
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            writer.WriteString("recordedAtUtc", recordedAt);
            writer.WriteString("source", state.LastPacketSource);
            writer.WritePropertyName("packet");
            rawDocument.RootElement.WriteTo(writer);
            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private static string CreateCsvLine(DateTimeOffset recordedAt, TelemetryReceiverState state)
    {
        var packet = state.LastPacket;
        var values = new string?[]
        {
            recordedAt.ToString("O", CultureInfo.InvariantCulture),
            state.LastPacketSource,
            packet?.Frame?.Sequence?.ToString(CultureInfo.InvariantCulture),
            packet?.Frame?.TimestampMs?.ToString(CultureInfo.InvariantCulture),
            packet?.VehicleName,
            packet?.VehicleCategory,
            Format(packet?.SpeedKmh),
            Format(packet?.SteeringAngle),
            Format(packet?.SteeringRate),
            Format(packet?.PitchDeg),
            Format(packet?.RollDeg),
            Format(packet?.SlopeDeg),
            Format(packet?.LocalAccelerationX),
            Format(packet?.LocalAccelerationY),
            Format(packet?.LocalAccelerationZ),
            Format(packet?.WheelSlip),
            Format(packet?.MaxWheelSlip),
            Format(packet?.GroundContactRatio),
            Format(packet?.SuspensionImpulse),
            Format(packet?.VerticalImpactImpulse),
            Format(packet?.LeftSuspensionImpulse),
            Format(packet?.RightSuspensionImpulse),
            Format(packet?.LandingImpulse),
            Format(packet?.CollisionImpulse),
            Format(packet?.LongitudinalJerkImpulse),
            Format(packet?.Throttle),
            Format(packet?.Brake),
            Format(packet?.Rpm),
            packet?.SurfaceType,
            Format(packet?.GroundWetness),
            Format(packet?.RainScale)
        };

        return string.Join(",", values.Select(EscapeCsv));
    }

    private static string Format(double? value)
    {
        return value is null ? "" : value.Value.ToString("G17", CultureInfo.InvariantCulture);
    }

    private static string EscapeCsv(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return "";
        }

        return value.Contains('"', StringComparison.Ordinal) ||
            value.Contains(',', StringComparison.Ordinal) ||
            value.Contains('\n', StringComparison.Ordinal) ||
            value.Contains('\r', StringComparison.Ordinal)
            ? $"\"{value.Replace("\"", "\"\"", StringComparison.Ordinal)}\""
            : value;
    }

    private static readonly string[] CsvColumns =
    [
        "recordedAtUtc",
        "source",
        "frameSequence",
        "timestampMs",
        "vehicleName",
        "vehicleCategory",
        "speedKmh",
        "steeringAngle",
        "steeringRate",
        "pitchDeg",
        "rollDeg",
        "slopeDeg",
        "localAccelerationX",
        "localAccelerationY",
        "localAccelerationZ",
        "wheelSlipAvg",
        "wheelSlipMax",
        "groundContactRatio",
        "suspensionImpulse",
        "verticalImpactImpulse",
        "leftSuspensionImpulse",
        "rightSuspensionImpulse",
        "landingImpulse",
        "collisionImpulse",
        "longitudinalJerkImpulse",
        "throttle",
        "brake",
        "rpm",
        "surfaceType",
        "groundWetness",
        "rainScale"
    ];
}
