using FS25FfbBridge.App.Models;
using FS25FfbBridge.App.Services;

namespace FS25FfbBridge.App.Tests;

public sealed class DirectInputFfbBackendTests
{
    [Fact]
    public void G27_like_device_prefers_x_axis_over_first_pedal_actuator()
    {
        var device = Device("Logitech G27 Racing Wheel USB");

        var selection = DirectInputFfbBackend.ResolvePrimaryFfbAxisOffset(
            device,
            [0, 4, 20, 24],
            [4],
            primaryFfbAxisOffset: null);

        Assert.Equal(0, selection.Offset);
        Assert.Equal("wheel-x", selection.Source);
    }

    [Fact]
    public void Configured_primary_axis_wins_when_axis_exists()
    {
        var device = Device("Generic FFB Wheel");

        var selection = DirectInputFfbBackend.ResolvePrimaryFfbAxisOffset(
            device,
            [0, 4, 20, 24],
            [4],
            primaryFfbAxisOffset: 0);

        Assert.Equal(0, selection.Offset);
        Assert.Equal("config", selection.Source);
    }

    [Fact]
    public void Invalid_configured_primary_axis_is_ignored()
    {
        var device = Device("Logitech G27 Racing Wheel USB");

        var selection = DirectInputFfbBackend.ResolvePrimaryFfbAxisOffset(
            device,
            [0, 4, 20, 24],
            [4],
            primaryFfbAxisOffset: 8);

        Assert.Equal(0, selection.Offset);
        Assert.Equal("wheel-x", selection.Source);
    }

    [Fact]
    public void Generic_non_wheel_keeps_first_valid_actuator()
    {
        var device = Device("Generic Game Controller", deviceType: "Gamepad");

        var selection = DirectInputFfbBackend.ResolvePrimaryFfbAxisOffset(
            device,
            [0, 8],
            [8],
            primaryFfbAxisOffset: null);

        Assert.Equal(8, selection.Offset);
        Assert.Equal("actuator", selection.Source);
    }

    private static DeviceInfo Device(string productName, string deviceType = "Driving")
    {
        return new DeviceInfo(
            StableId: $"{productName}:stable",
            InstanceGuid: Guid.NewGuid(),
            ProductGuid: Guid.NewGuid(),
            InstanceName: productName,
            ProductName: productName,
            DeviceType: deviceType,
            IsForceFeedbackCapable: true,
            Axes: [],
            SupportedEffects: []);
    }
}
