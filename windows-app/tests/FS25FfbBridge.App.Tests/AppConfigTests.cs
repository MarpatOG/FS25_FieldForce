using System.Text.Json;
using FS25FfbBridge.App.Models;

namespace FS25FfbBridge.App.Tests;

public sealed class AppConfigTests
{
    [Fact]
    public void Defaults_use_stronger_logitech_momo_mvp_profile()
    {
        var config = new AppConfig();

        Assert.Equal("Logitech MOMO Racing Wheel", config.DeviceProfileName);
        Assert.Equal(270, config.RotationDegrees);
        Assert.Equal(40, config.GlobalForceLimitPercent);
        Assert.Equal(35, config.DeviceForceLimitPercent);
        Assert.True(config.GameplayFfb.Enabled);
        Assert.True(config.GameplayFfb.SpeedSpring.Enabled);
        Assert.True(config.GameplayFfb.SpeedDamper.Enabled);
        Assert.True(config.GameplayFfb.SpeedSpring.StrengthPercent >= 80);
        Assert.True(config.GameplayFfb.SpeedDamper.StrengthPercent >= 80);
        Assert.True(config.GameplayFfb.EngineVibration.StrengthPercent >= 50);
    }

    [Fact]
    public void Effect_settings_round_trip_through_json()
    {
        var config = new AppConfig();
        config.GameplayFfb.SpeedSpring.StrengthPercent = 42;
        config.GameplayFfb.SurfaceFeedback.Enabled = false;

        var json = JsonSerializer.Serialize(config);
        var decoded = JsonSerializer.Deserialize<AppConfig>(json);

        Assert.NotNull(decoded);
        Assert.Equal(42, decoded!.GameplayFfb.SpeedSpring.StrengthPercent);
        Assert.False(decoded.GameplayFfb.SurfaceFeedback.Enabled);
    }
}
