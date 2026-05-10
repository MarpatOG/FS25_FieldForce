namespace FS25FfbBridge.App.Models;

public sealed record TelemetryReceiverState(
    TelemetryStatus Status,
    TelemetryPacketV1? LastPacket,
    string LastRawPacket,
    double PacketRate,
    TimeSpan? LastPacketAge,
    string? LastParseError,
    string Endpoint,
    string UdpStatus,
    string FileStatus,
    string LastPacketSource,
    string? LastTransportError);
