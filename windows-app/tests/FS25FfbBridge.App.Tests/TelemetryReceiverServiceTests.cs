using System.Net;
using System.Net.Sockets;
using System.Text;
using FS25FfbBridge.App.Models;
using FS25FfbBridge.App.Services;

namespace FS25FfbBridge.App.Tests;

public sealed class TelemetryReceiverServiceTests
{
    private const string ValidPacket = """
        {
          "timestamp": 123456,
          "gameState": "mission",
          "isPlayerInVehicle": true,
          "vehicleName": "Tractor",
          "vehicleType": "tractor",
          "vehicleCategory": "TractorWheeled",
          "wheelTireTypes": "street,mud",
          "wheelTireProfile": "mixed",
          "speedKmh": 12.4,
          "steeringAngle": 0.13,
          "rpm": 850,
          "engineStarted": true,
          "mass": 6200,
          "totalMass": 8800,
          "isOnField": false
        }
        """;

    private const string ExtendedPacket = """
        {
          "timestamp": 123456,
          "gameState": "mission",
          "isPlayerInVehicle": true,
          "vehicleName": "Tractor",
          "vehicleType": "tractor",
          "vehicleCategory": "TractorWheeled",
          "wheelTireTypes": "street,mud",
          "wheelTireProfile": "mixed",
          "speedKmh": 12.4,
          "steeringAngle": 0.13,
          "rpm": 850,
          "engineStarted": true,
          "mass": 6200,
          "totalMass": 8800,
          "isOnField": true,
          "surfaceType": "field",
          "surfaceAttribute": 1,
          "groundWetness": 0.35,
          "rainScale": 0.2,
          "wheelSlip": 0.12,
          "maxWheelSlip": 0.24,
          "groundContactRatio": 1.0,
          "pitchDeg": 3.1,
          "rollDeg": -2.4,
          "yawRateDegPerSec": 8.5,
          "slopeDeg": 4.0,
          "localAccelerationX": 0.3,
          "localAccelerationY": 1.8,
          "localAccelerationZ": -0.6,
          "bumpImpulse": 0.42
        }
        """;

    [Fact]
    public async Task Receives_valid_udp_packet()
    {
        using var log = new AppLogService();
        using var receiver = new TelemetryReceiverService(log);
        var port = GetFreeUdpPort();
        var filePath = GetTempTelemetryPath();

        receiver.Start("127.0.0.1", port, 1000, filePath, includeDefaultFilePath: false);
        var stateTask = WaitForStateAsync(receiver, state =>
            state.Status == TelemetryStatus.Connected &&
            state.LastPacketSource.StartsWith("udp://", StringComparison.OrdinalIgnoreCase));

        await SendUdpAsync(port, ValidPacket);

        var state = await stateTask;
        Assert.Equal("Tractor", state.LastPacket?.VehicleName);
        Assert.Equal("TractorWheeled", state.LastPacket?.VehicleCategory);
        Assert.Null(state.LastPacket?.SurfaceType);
        Assert.StartsWith("Listening: udp://127.0.0.1:", state.UdpStatus);
    }

    [Fact]
    public async Task Receives_extended_telemetry_packet()
    {
        using var log = new AppLogService();
        using var receiver = new TelemetryReceiverService(log);
        var port = GetFreeUdpPort();
        var filePath = GetTempTelemetryPath();

        receiver.Start("127.0.0.1", port, 1000, filePath, includeDefaultFilePath: false);
        var stateTask = WaitForStateAsync(receiver, state =>
            state.Status == TelemetryStatus.Connected &&
            state.LastPacket?.SurfaceType == "field");

        await SendUdpAsync(port, ExtendedPacket);

        var packet = (await stateTask).LastPacket;
        Assert.Equal(0.35, packet?.GroundWetness);
        Assert.Equal(0.24, packet?.MaxWheelSlip);
        Assert.Equal("street,mud", packet?.WheelTireTypes);
        Assert.Equal("mixed", packet?.WheelTireProfile);
        Assert.Equal(-2.4, packet?.RollDeg);
        Assert.Equal(0.42, packet?.BumpImpulse);
    }

    [Fact]
    public async Task Reads_valid_file_packet()
    {
        using var log = new AppLogService();
        using var receiver = new TelemetryReceiverService(log);
        var port = GetFreeUdpPort();
        var filePath = GetTempTelemetryPath();

        receiver.Start("127.0.0.1", port, 1000, filePath, includeDefaultFilePath: false);
        var stateTask = WaitForStateAsync(receiver, state =>
            state.Status == TelemetryStatus.Connected &&
            state.LastPacketSource.StartsWith("file://", StringComparison.OrdinalIgnoreCase));

        await File.WriteAllTextAsync(filePath, ValidPacket);

        var state = await stateTask;
        Assert.Equal("Tractor", state.LastPacket?.VehicleName);
        Assert.StartsWith("Watching: file://", state.FileStatus);
    }

    [Fact]
    public async Task Keeps_last_valid_packet_after_invalid_json()
    {
        using var log = new AppLogService();
        using var receiver = new TelemetryReceiverService(log);
        var port = GetFreeUdpPort();
        var filePath = GetTempTelemetryPath();

        receiver.Start("127.0.0.1", port, 1000, filePath, includeDefaultFilePath: false);
        var validStateTask = WaitForStateAsync(receiver, state => state.LastPacket?.VehicleName == "Tractor");
        await SendUdpAsync(port, ValidPacket);
        await validStateTask;

        var invalidStateTask = WaitForStateAsync(receiver, state =>
            state.LastParseError is not null &&
            state.LastRawPacket.Contains("not-json", StringComparison.Ordinal));
        await SendUdpAsync(port, "{not-json");

        var state = await invalidStateTask;
        Assert.Equal("Tractor", state.LastPacket?.VehicleName);
        Assert.Contains("not-json", state.LastRawPacket, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Moves_from_waiting_to_connected_to_lost()
    {
        using var log = new AppLogService();
        using var receiver = new TelemetryReceiverService(log);
        var port = GetFreeUdpPort();
        var filePath = GetTempTelemetryPath();

        receiver.Start("127.0.0.1", port, 250, filePath, includeDefaultFilePath: false);
        var connectedTask = WaitForStateAsync(receiver, state => state.Status == TelemetryStatus.Connected);
        await SendUdpAsync(port, ValidPacket);
        await connectedTask;

        var lostState = await WaitForStateAsync(receiver, state => state.Status == TelemetryStatus.Lost, TimeSpan.FromSeconds(3));
        Assert.Equal(TelemetryStatus.Lost, lostState.Status);
        Assert.Equal("Tractor", lostState.LastPacket?.VehicleName);
    }

    [Fact]
    public async Task Reports_udp_bind_error_without_throwing()
    {
        var port = GetFreeUdpPort();
        using var blocker = new UdpClient(new IPEndPoint(IPAddress.Loopback, port));
        using var log = new AppLogService();
        using var receiver = new TelemetryReceiverService(log);
        var filePath = GetTempTelemetryPath();

        receiver.Start("127.0.0.1", port, 1000, filePath, includeDefaultFilePath: false);

        var state = await WaitForStateAsync(receiver, state => state.UdpStatus.StartsWith("Bind failed:", StringComparison.Ordinal));
        Assert.Equal(TelemetryStatus.Waiting, state.Status);
        Assert.Contains("UDP bind failed", state.LastTransportError, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Udp_burst_reports_high_packet_rate_and_throttles_ui_state()
    {
        using var log = new AppLogService();
        using var receiver = new TelemetryReceiverService(log);
        var port = GetFreeUdpPort();
        var filePath = GetTempTelemetryPath();
        var uiEvents = 0;
        var ffbEvents = 0;
        TelemetryReceiverState? latestFfbState = null;

        receiver.StateChanged += state =>
        {
            Interlocked.Increment(ref uiEvents);
        };
        receiver.FfbStateChanged += state =>
        {
            Interlocked.Increment(ref ffbEvents);
            latestFfbState = state;
        };
        receiver.Start("127.0.0.1", port, 1000, filePath, includeDefaultFilePath: false, uiRefreshMs: 100);

        for (var i = 0; i < 125; i++)
        {
            await SendUdpAsync(port, ValidPacket);
            await Task.Yield();
        }

        await WaitUntilAsync(() => Volatile.Read(ref ffbEvents) >= 120);
        Assert.NotNull(latestFfbState);
        Assert.InRange(latestFfbState!.PacketRate, 110, 140);
        Assert.InRange(Volatile.Read(ref uiEvents), 1, 30);
        Assert.True(Volatile.Read(ref ffbEvents) >= 110);
    }

    [Fact]
    public async Task High_rate_ffb_event_arrives_for_valid_udp_packet()
    {
        using var log = new AppLogService();
        using var receiver = new TelemetryReceiverService(log);
        var port = GetFreeUdpPort();
        var filePath = GetTempTelemetryPath();

        receiver.Start("127.0.0.1", port, 1000, filePath, includeDefaultFilePath: false);
        var ffbTask = WaitForFfbStateAsync(receiver, state =>
            state.Status == TelemetryStatus.Connected &&
            state.LastPacketSource.StartsWith("udp://", StringComparison.OrdinalIgnoreCase));

        await SendUdpAsync(port, ValidPacket);

        var state = await ffbTask;
        Assert.Equal("Tractor", state.LastPacket?.VehicleName);
    }

    [Fact]
    public async Task File_fallback_does_not_replace_fresh_udp_source()
    {
        using var log = new AppLogService();
        using var receiver = new TelemetryReceiverService(log);
        var port = GetFreeUdpPort();
        var filePath = GetTempTelemetryPath();

        receiver.Start("127.0.0.1", port, 1000, filePath, includeDefaultFilePath: false, uiRefreshMs: 50);
        var udpTask = WaitForStateAsync(receiver, state =>
            state.LastPacketSource.StartsWith("udp://", StringComparison.OrdinalIgnoreCase));
        await SendUdpAsync(port, PacketWithVehicleName("Udp Tractor"));
        await udpTask;

        await File.WriteAllTextAsync(filePath, PacketWithVehicleName("File Tractor"));
        await Task.Delay(250);

        var state = await WaitForStateAsync(receiver, state => state.LastPacket?.VehicleName == "Udp Tractor");
        Assert.StartsWith("udp://", state.LastPacketSource, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("Udp Tractor", state.LastPacket?.VehicleName);
    }

    [Fact]
    public async Task File_fallback_works_when_udp_bind_fails()
    {
        var port = GetFreeUdpPort();
        using var blocker = new UdpClient(new IPEndPoint(IPAddress.Loopback, port));
        using var log = new AppLogService();
        using var receiver = new TelemetryReceiverService(log);
        var filePath = GetTempTelemetryPath();

        receiver.Start("127.0.0.1", port, 1000, filePath, includeDefaultFilePath: false);
        var stateTask = WaitForStateAsync(receiver, state =>
            state.Status == TelemetryStatus.Connected &&
            state.LastPacketSource.StartsWith("file://", StringComparison.OrdinalIgnoreCase));

        await File.WriteAllTextAsync(filePath, ValidPacket);

        var state = await stateTask;
        Assert.Equal("Tractor", state.LastPacket?.VehicleName);
        Assert.StartsWith("Bind failed:", state.UdpStatus);
    }

    [Fact]
    public async Task Lost_timeout_publishes_ffb_lost_state()
    {
        using var log = new AppLogService();
        using var receiver = new TelemetryReceiverService(log);
        var port = GetFreeUdpPort();
        var filePath = GetTempTelemetryPath();

        receiver.Start("127.0.0.1", port, 250, filePath, includeDefaultFilePath: false, uiRefreshMs: 50);
        var connectedTask = WaitForFfbStateAsync(receiver, state => state.Status == TelemetryStatus.Connected);
        await SendUdpAsync(port, ValidPacket);
        await connectedTask;

        var lostState = await WaitForFfbStateAsync(receiver, state => state.Status == TelemetryStatus.Lost, TimeSpan.FromSeconds(3));
        Assert.Equal(TelemetryStatus.Lost, lostState.Status);
        Assert.Equal("Tractor", lostState.LastPacket?.VehicleName);
    }

    private static async Task<TelemetryReceiverState> WaitForStateAsync(
        TelemetryReceiverService receiver,
        Func<TelemetryReceiverState, bool> predicate,
        TimeSpan? timeout = null)
    {
        var tcs = new TaskCompletionSource<TelemetryReceiverState>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var cts = new CancellationTokenSource(timeout ?? TimeSpan.FromSeconds(2));
        using var registration = cts.Token.Register(() => tcs.TrySetException(new TimeoutException("Timed out waiting for telemetry state.")));

        void Handler(TelemetryReceiverState state)
        {
            if (predicate(state))
            {
                tcs.TrySetResult(state);
            }
        }

        receiver.StateChanged += Handler;
        try
        {
            return await tcs.Task;
        }
        finally
        {
            receiver.StateChanged -= Handler;
        }
    }

    private static async Task<TelemetryReceiverState> WaitForFfbStateAsync(
        TelemetryReceiverService receiver,
        Func<TelemetryReceiverState, bool> predicate,
        TimeSpan? timeout = null)
    {
        var tcs = new TaskCompletionSource<TelemetryReceiverState>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var cts = new CancellationTokenSource(timeout ?? TimeSpan.FromSeconds(2));
        using var registration = cts.Token.Register(() => tcs.TrySetException(new TimeoutException("Timed out waiting for telemetry FFB state.")));

        void Handler(TelemetryReceiverState state)
        {
            if (predicate(state))
            {
                tcs.TrySetResult(state);
            }
        }

        receiver.FfbStateChanged += Handler;
        try
        {
            return await tcs.Task;
        }
        finally
        {
            receiver.FfbStateChanged -= Handler;
        }
    }

    private static async Task WaitUntilAsync(Func<bool> predicate, TimeSpan? timeout = null)
    {
        var stopAt = DateTimeOffset.UtcNow + (timeout ?? TimeSpan.FromSeconds(2));
        while (DateTimeOffset.UtcNow < stopAt)
        {
            if (predicate())
            {
                return;
            }

            await Task.Delay(10);
        }

        throw new TimeoutException("Timed out waiting for condition.");
    }

    private static async Task SendUdpAsync(int port, string json)
    {
        using var udp = new UdpClient();
        var bytes = Encoding.UTF8.GetBytes(json);
        await udp.SendAsync(bytes, bytes.Length, new IPEndPoint(IPAddress.Loopback, port));
    }

    private static string PacketWithVehicleName(string vehicleName)
    {
        return ValidPacket.Replace("\"vehicleName\": \"Tractor\"", $"\"vehicleName\": \"{vehicleName}\"", StringComparison.Ordinal);
    }

    private static int GetFreeUdpPort()
    {
        using var udp = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        return ((IPEndPoint)udp.Client.LocalEndPoint!).Port;
    }

    private static string GetTempTelemetryPath()
    {
        var directory = Path.Combine(Path.GetTempPath(), "FS25FfbBridge.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return Path.Combine(directory, "telemetry.json");
    }
}
