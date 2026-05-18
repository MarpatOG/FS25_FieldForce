using FieldForce.App.Models;
using FieldForce.App.Services;
using Vortice.DirectInput;

namespace FieldForce.App.Tests;

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

    [Fact]
    public void Logitech_hid_guid_parses_vid_and_pid()
    {
        var productGuid = Guid.Parse("C29B046D-0000-0000-0000-504944564944");

        var parsed = DirectInputFfbBackend.TryGetDirectInputHidVidPid(productGuid, out var vendorId, out var productId);

        Assert.True(parsed);
        Assert.Equal(0x046D, vendorId);
        Assert.Equal(0xC29B, productId);
        Assert.True(DirectInputFfbBackend.IsLogitechDirectInputHid(productGuid));
    }

    [Fact]
    public void Logitech_vid_device_uses_x_axis_even_when_x_axis_is_not_enumerated()
    {
        var device = Device(
            "Logitech G27 Racing Wheel USB",
            productGuid: Guid.Parse("C29B046D-0000-0000-0000-504944564944"));

        var selection = DirectInputFfbBackend.ResolvePrimaryFfbAxisOffset(
            device,
            [4, 8, 12, 16, 24],
            [],
            primaryFfbAxisOffset: null);

        Assert.Equal(0, selection.Offset);
        Assert.Equal("logitech-vid-x", selection.Source);
    }

    [Fact]
    public void Logitech_vid_device_ignores_invalid_nonzero_config_and_uses_x_axis()
    {
        var device = Device(
            "Logitech G27 Racing Wheel USB",
            productGuid: Guid.Parse("C29B046D-0000-0000-0000-504944564944"));

        var selection = DirectInputFfbBackend.ResolvePrimaryFfbAxisOffset(
            device,
            [4, 8, 12, 16, 24],
            [],
            primaryFfbAxisOffset: 28);

        Assert.Equal(0, selection.Offset);
        Assert.Equal("logitech-vid-x", selection.Source);
    }

    [Fact]
    public void Explicit_effect_parameters_use_primary_axis()
    {
        var parameters = DirectInputFfbBackend.CreateBaseParametersForTesting(
            primaryAxisOffset: 0,
            TimeSpan.FromSeconds(1),
            direction: 10000,
            DirectInputFfbBackend.EffectAxisParameterMode.ExplicitPrimaryAxis);

        Assert.True(parameters.Flags.HasFlag(EffectFlags.ObjectOffsets));
        Assert.True(parameters.Flags.HasFlag(EffectFlags.Cartesian));
        Assert.Equal([0], parameters.Axes);
        Assert.Equal([10000], parameters.Directions);
    }

    [Fact]
    public void Implicit_effect_parameters_omit_axes_and_directions()
    {
        var parameters = DirectInputFfbBackend.CreateBaseParametersForTesting(
            primaryAxisOffset: 0,
            TimeSpan.FromSeconds(1),
            direction: 10000,
            DirectInputFfbBackend.EffectAxisParameterMode.ImplicitDeviceAxis);

        Assert.False(parameters.Flags.HasFlag(EffectFlags.ObjectOffsets));
        Assert.False(parameters.Flags.HasFlag(EffectFlags.Cartesian));
        Assert.Empty(parameters.Axes);
        Assert.Empty(parameters.Directions);
    }

    private static DeviceInfo Device(string productName, string deviceType = "Driving", Guid? productGuid = null)
    {
        return new DeviceInfo(
            StableId: $"{productName}:stable",
            InstanceGuid: Guid.NewGuid(),
            ProductGuid: productGuid ?? Guid.NewGuid(),
            InstanceName: productName,
            ProductName: productName,
            DeviceType: deviceType,
            IsForceFeedbackCapable: true,
            Axes: [],
            SupportedEffects: []);
    }
}
