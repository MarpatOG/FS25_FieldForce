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

    private static async Task SendUdpAsync(int port, string json)
    {
        using var udp = new UdpClient();
        var bytes = Encoding.UTF8.GetBytes(json);
        await udp.SendAsync(bytes, bytes.Length, new IPEndPoint(IPAddress.Loopback, port));
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
